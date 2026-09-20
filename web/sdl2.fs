\ Copyright 2026 luginf
\
\ Licensed under the Apache License, Version 2.0 (the "License");
\ you may not use this file except in compliance with the License.
\ You may obtain a copy of the License at
\
\     http://www.apache.org/licenses/LICENSE-2.0
\
\ Unless required by applicable law or agreed to in writing, software
\ distributed under the License is distributed on an "AS IS" BASIS,
\ WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
\ See the License for the specific language governing permissions and
\ limitations under the License.

( Lazy load the sdl2 short words for the Web: canvas, keys, mouse, beeps )
\
\ Same words and stack effects as posix/sdl2.fs, so a game written with
\ the short words (see examples/sdl2_breakout.fs) runs unchanged in both.
\ Only the short words exist here, not the raw SDL_ names nor sdl2-image,
\ sdl2-ttf and sdl2-mixer. See web/SDL2.md.
\ NOTE: the block below is a string, so it must not contain a vertical bar,
\ in Forth or in the JavaScript (write a ternary instead of a logical or).
\ A tilde ends each JavaScript part.

: sdl2 r|

forth also web also internals
vocabulary sdl2   also sdl2 definitions

( ---- State shared by the words, and the event handlers ---- )
r~
(function() {
  var S = context.sdl = {
    open: false, w: 0, h: 0, zoom: 1, overlay: null, canvas: null, ctx: null,
    style: 'rgb(0,0,0)', r: 0, g: 0, b: 0, a: 255,
    down: new Uint8Array(256), hit: new Uint8Array(256), snap: new Uint8Array(256),
    mx: 0, my: 0, wheel: 0, pwheel: 0, lastKey: 0, quit: 0, frame: false,
    ac: null, beepEnd: 0, hitEnd: 0, nodes: [], noise: null, cursorHidden: false
  };
  S.setColor = function(r, g, b, a) {
    S.r = r & 255; S.g = g & 255; S.b = b & 255; S.a = a & 255;
    S.style = 'rgba(' + S.r + ',' + S.g + ',' + S.b + ',' + (S.a / 255) + ')';
    if (S.ctx) { S.ctx.fillStyle = S.style; }
  };
  S.layout = function() {
    if (!S.open) { return; }
    var s = Math.min(S.zoom, window.innerWidth / S.w, window.innerHeight / S.h);
    S.canvas.style.width = (S.w * s) + 'px';
    S.canvas.style.height = (S.h * s) + 'px';
  };
  S.ensureAudio = function() {
    if (S.ac) {
      if (S.ac.state === 'suspended') { S.ac.resume(); }
      return;
    }
    var AC = window.AudioContext ? window.AudioContext : window.webkitAudioContext;
    if (AC) { S.ac = new AC(); }
  };
  S.silence = function() {
    for (var i = 0; i < S.nodes.length; ++i) {
      try { S.nodes[i].stop(); } catch (e) {}
    }
    S.nodes = [];
    S.beepEnd = 0; S.hitEnd = 0;
  };
  S.makeSource = function(w, freq, t) {
    var ac = S.ac;
    var src;
    if (w === 3) {
      if (!S.noise) {
        S.noise = ac.createBuffer(1, ac.sampleRate, ac.sampleRate);
        var data = S.noise.getChannelData(0);
        for (var i = 0; i < data.length; ++i) { data[i] = Math.random() * 2 - 1; }
      }
      src = ac.createBufferSource();
      src.buffer = S.noise; src.loop = true;
    } else {
      src = ac.createOscillator();
      src.type = w === 1 ? 'triangle' : (w === 2 ? 'sawtooth' : 'square');
      src.frequency.setValueAtTime(freq, t);
    }
    return src;
  };
  S.track = function(src) {
    src.onended = function() {
      var k = S.nodes.indexOf(src);
      if (k >= 0) { S.nodes.splice(k, 1); }
    };
    S.nodes.push(src);
  };
  S.images = [null];
  S.loadImage = function(name) {
    var rec = {img: new Image(), state: 0, w: 0, h: 0, tint: 16777215, alpha: 255,
               cache: {}, retried: false};
    var slash = name.lastIndexOf('/');
    rec.img.onload = function() {
      rec.w = rec.img.naturalWidth; rec.h = rec.img.naturalHeight; rec.state = 1;
    };
    rec.img.onerror = function() {
      if (!rec.retried && slash >= 0) {
        rec.retried = true;
        rec.img.src = name.substring(slash + 1);
      } else {
        rec.state = -1;
      }
    };
    rec.img.src = name;
    S.images.push(rec);
    return S.images.length - 1;
  };
  S.tinted = function(rec) {
    if (rec.tint === 16777215) { return rec.img; }
    var c = rec.cache[rec.tint];
    if (!c) {
      c = document.createElement('canvas');
      c.width = rec.w; c.height = rec.h;
      var g = c.getContext('2d');
      g.drawImage(rec.img, 0, 0);
      g.globalCompositeOperation = 'multiply';
      g.fillStyle = 'rgb(' + ((rec.tint >> 16) & 255) + ',' + ((rec.tint >> 8) & 255) +
                    ',' + (rec.tint & 255) + ')';
      g.fillRect(0, 0, rec.w, rec.h);
      g.globalCompositeOperation = 'destination-in';
      g.drawImage(rec.img, 0, 0);
      rec.cache[rec.tint] = c;
    }
    return c;
  };
  S.drawImage = function(h, sx, sy, sw, sh, x, y, w, hh) {
    var rec = S.images[h];
    if (!rec) { return; }
    if (rec.state !== 1) { return; }
    if (!S.ctx) { return; }
    var g = S.ctx;
    var was = g.globalAlpha;
    g.imageSmoothingEnabled = false;
    g.globalAlpha = rec.alpha / 255;
    g.drawImage(S.tinted(rec), sx, sy, sw, sh, x, y, w, hh);
    g.globalAlpha = was;
  };
  S.close = function() {
    if (S.overlay && S.overlay.parentNode) { S.overlay.parentNode.removeChild(S.overlay); }
    S.silence();
    S.open = false; S.overlay = null; S.canvas = null; S.ctx = null;
    S.down.fill(0); S.hit.fill(0); S.snap.fill(0);
    if (document.fullscreenElement) { document.exitFullscreen(); }
  };
  S.pos = function(e) {
    var r = S.canvas.getBoundingClientRect();
    S.mx = Math.floor((e.clientX - r.left) * S.w / r.width);
    S.my = Math.floor((e.clientY - r.top) * S.h / r.height);
  };
  function keyCode(e) {
    var k = e.key;
    if (k.length === 1) { return k.toLowerCase().charCodeAt(0) & 255; }
    var m = {Enter: 13, Escape: 27, Tab: 9, Backspace: 8,
             ArrowLeft: 208, ArrowRight: 207, ArrowUp: 210, ArrowDown: 209};
    return m[k] === undefined ? 0 : m[k];
  }
  function ours(e) {
    return S.open && !e.ctrlKey && !e.metaKey && !e.altKey && !/^F[0-9]+$/.test(e.key);
  }
  if (!globalObj.write) {
    window.addEventListener('keydown', function(e) {
      if (!S.open) { return; }
      S.ensureAudio();
      if (!ours(e)) { return; }
      var c = keyCode(e);
      if (c) {
        if (!S.down[c]) { S.hit[c] = 1; }
        S.down[c] = 1; S.lastKey = c;
      }
      e.preventDefault(); e.stopImmediatePropagation();
    }, true);
    window.addEventListener('keyup', function(e) {
      if (!ours(e)) { return; }
      var c = keyCode(e);
      if (c) { S.down[c] = 0; }
      e.preventDefault(); e.stopImmediatePropagation();
    }, true);
    window.addEventListener('keypress', function(e) {
      if (!ours(e)) { return; }
      e.preventDefault(); e.stopImmediatePropagation();
    }, true);
    window.addEventListener('blur', function() { S.down.fill(0); });
    window.addEventListener('resize', function() { S.layout(); });
  }
})();
~ jseval

( ---- Screen ---- )
1 value zoom   0 value width   0 value height

JSWORD: js-screen { w h z }
  var S = context.sdl;
  if (S.open) { S.close(); }
  S.w = w; S.h = h; S.zoom = z < 1 ? 1 : z;
  var o = document.createElement('div');
  o.style.cssText = 'position:fixed;left:0;top:0;width:100%;height:100%;' +
      'background:#000;z-index:100000;display:flex;align-items:center;' +
      'justify-content:center;touch-action:none;user-select:none;';
  var c = document.createElement('canvas');
  c.width = w; c.height = h;
  c.style.cssText = 'display:block;image-rendering:pixelated;background:#000;';
  o.appendChild(c);
  o.style.cursor = S.cursorHidden ? 'none' : '';
  o.onpointermove = function(e) { S.pos(e); };
  o.onpointerdown = function(e) {
    S.pos(e); S.ensureAudio();
    var k = 255 - e.button;
    if (!S.down[k]) { S.hit[k] = 1; }
    S.down[k] = 1;
    o.setPointerCapture(e.pointerId);
    e.preventDefault();
  };
  o.onpointerup = function(e) {
    S.pos(e);
    S.down[255 - e.button] = 0;
    e.preventDefault();
  };
  o.onwheel = function(e) {
    S.pwheel += e.deltaY < 0 ? 1 : (e.deltaY > 0 ? -1 : 0);
    e.preventDefault();
  };
  o.oncontextmenu = function(e) { e.preventDefault(); };
  document.body.appendChild(o);
  S.overlay = o; S.canvas = c; S.ctx = c.getContext('2d');
  S.open = true; S.quit = 0; S.mx = 0; S.my = 0; S.frame = false;
  S.setColor(0, 0, 0, 255);
  S.ctx.fillRect(0, 0, w, h);
  S.layout();
~
: screen ( w h -- ) 2dup to height to width  zoom js-screen ;

JSWORD: close-screen { }
  context.sdl.close();
~
JSWORD: screen-open? { -- f }
  return context.sdl.open ? -1 : 0;
~
JSWORD: title { a n }
  document.title = GetString(a, n);
~
JSWORD: fullscreen { f }
  var S = context.sdl;
  if (!S.open) { return; }
  if (f) {
    S.overlay.requestFullscreen();
  } else if (document.fullscreenElement) {
    document.exitFullscreen();
  }
~
JSWORD: cursor { f }
  var S = context.sdl;
  S.cursorHidden = !f;
  if (S.overlay) { S.overlay.style.cursor = f ? '' : 'none'; }
~

( ---- Colors ---- )
JSWORD: rgba { r g b a }
  context.sdl.setColor(r, g, b, a);
~
: rgb ( r g b -- ) 255 rgba ;
JSWORD: color { c }
  context.sdl.setColor((c >> 16) & 255, (c >> 8) & 255, c & 255, 255);
~
JSWORD: alpha { a }
  var S = context.sdl;
  S.setColor(S.r, S.g, S.b, a);
~

$000000 constant black    $ffffff constant white
$ff0000 constant red      $00c000 constant green
$0000ff constant blue     $ffff00 constant yellow
$00ffff constant cyan     $ff00ff constant magenta
$ff8000 constant orange   $800080 constant purple
$ff80c0 constant pink     $804000 constant brown
$808080 constant gray

( ---- Drawing ---- )
JSWORD: cls { }
  var S = context.sdl;
  if (!S.open) { return; }
  S.ctx.save();
  S.ctx.globalCompositeOperation = 'copy';
  S.ctx.fillRect(0, 0, S.w, S.h);
  S.ctx.restore();
~
JSWORD: dot { x y }
  var S = context.sdl;
  if (S.open) { S.ctx.fillRect(x, y, 1, 1); }
~
JSWORD: line { x1 y1 x2 y2 }
  var S = context.sdl;
  if (!S.open) { return; }
  var dx = Math.abs(x2 - x1), sx = x1 < x2 ? 1 : -1;
  var dy = -Math.abs(y2 - y1), sy = y1 < y2 ? 1 : -1;
  var err = dx + dy;
  for (;;) {
    S.ctx.fillRect(x1, y1, 1, 1);
    if (x1 === x2 && y1 === y2) { break; }
    var e2 = 2 * err;
    if (e2 >= dy) { err += dy; x1 += sx; }
    if (e2 <= dx) { err += dx; y1 += sy; }
  }
~
JSWORD: box { x y w h }
  var S = context.sdl;
  if (S.open) { S.ctx.fillRect(x, y, w, h); }
~
JSWORD: frame { x y w h }
  var S = context.sdl;
  if (!S.open) { return; }
  if (w <= 0) { return; }
  if (h <= 0) { return; }
  var c = S.ctx;
  c.fillRect(x, y, w, 1);
  if (h > 1) { c.fillRect(x, y + h - 1, w, 1); }
  if (h > 2) {
    c.fillRect(x, y + 1, 1, h - 2);
    if (w > 1) { c.fillRect(x + w - 1, y + 1, 1, h - 2); }
  }
~
JSWORD: hline { x y w }
  var S = context.sdl;
  if (S.open) { S.ctx.fillRect(x, y, w, 1); }
~
JSWORD: disc { cx cy r }
  var S = context.sdl;
  if (!S.open) { return; }
  var x = r;
  for (var i = 0; i <= r; ++i) {
    while (x * x + i * i > r * r) { --x; }
    S.ctx.fillRect(cx - x, cy + i, 2 * x + 1, 1);
    if (i !== 0) { S.ctx.fillRect(cx - x, cy - i, 2 * x + 1, 1); }
  }
~
JSWORD: circle { cx cy r }
  var S = context.sdl;
  if (!S.open) { return; }
  var c = S.ctx;
  var x = r;
  function dots(a, b) {
    c.fillRect(cx + a, cy + b, 1, 1); c.fillRect(cx - a, cy + b, 1, 1);
    c.fillRect(cx + a, cy - b, 1, 1); c.fillRect(cx - a, cy - b, 1, 1);
  }
  for (var i = 0; i <= r; ++i) {
    while (x * x + i * i > r * r) { --x; }
    dots(x, i); dots(i, x);
  }
~
JSWORD: pixel@ { x y -- rgb }
  var S = context.sdl;
  if (!S.open) { return 0; }
  var d = S.ctx.getImageData(x, y, 1, 1).data;
  return (d[0] << 16) + (d[1] << 8) + d[2];
~

( ---- Timing: flip waits for the next animation frame ---- )
JSWORD: ticks { -- ms }
  return Math.floor(performance.now());
~
: delay ( ms -- ) ms ;
0 value dt   0 value last-flip
JSWORD: frame-start { }
  var S = context.sdl;
  S.frame = false;
  requestAnimationFrame(function() { S.frame = true; });
~
JSWORD: frame-ready? { -- f }
  return context.sdl.frame ? -1 : 0;
~
: flip ( -- )
  frame-start   begin yield frame-ready? until
  ticks dup last-flip - to dt   to last-flip ;

( ---- Input ---- )
255 constant LEFT-BUTTON
254 constant MIDDLE-BUTTON
253 constant RIGHT-BUTTON
208 constant key-left    207 constant key-right
210 constant key-up      209 constant key-down
32 constant key-space    13 constant key-enter    27 constant key-esc
9 constant key-tab       8 constant key-backspace

JSWORD: events { }
  var S = context.sdl;
  for (var i = 0; i < 256; ++i) {
    S.snap[i] = (S.down[i] + S.hit[i]) > 0 ? 1 : 0;
  }
  S.hit.fill(0);
  S.wheel = S.pwheel; S.pwheel = 0;
~
JSWORD: pressed? { k -- f }
  if (k >= 65 && k <= 90) { k += 32; }
  return context.sdl.snap[k & 255] ? -1 : 0;
~
JSWORD: quit? { -- f }
  return context.sdl.quit ? -1 : 0;
~
JSWORD: quit! { }
  context.sdl.quit = 1;
~
JSWORD: last-key { -- n }
  return context.sdl.lastKey;
~
JSWORD: mouse-x { -- n }
  return context.sdl.mx;
~
JSWORD: mouse-y { -- n }
  return context.sdl.my;
~
JSWORD: wheel { -- n }
  return context.sdl.wheel;
~

( ---- Beeps are scheduled one after the other, hits play over each other ---- )
0 value wave   3000 value beep-volume
JSWORD: js-beep { freq ms w vol }
  var S = context.sdl;
  S.ensureAudio();
  var ac = S.ac;
  if (!ac) { return; }
  if (ac.state !== 'running') { return; }
  var t = Math.max(ac.currentTime, S.beepEnd);
  if (t - ac.currentTime > 2) { return; }
  var d = ms / 1000;
  var g = ac.createGain();
  g.gain.setValueAtTime(vol / 32768, t);
  g.gain.setValueAtTime(vol / 32768, t + Math.max(0, d - 0.01));
  g.gain.linearRampToValueAtTime(0, t + d);
  g.connect(ac.destination);
  var src = S.makeSource(w, freq, t);
  src.connect(g);
  S.track(src);
  src.start(t); src.stop(t + d);
  S.beepEnd = t + d;
~
: beep ( freq ms -- ) wave beep-volume js-beep ;
0 value pitch-drop
JSWORD: js-hit { freq ms w vol fall }
  var S = context.sdl;
  S.ensureAudio();
  var ac = S.ac;
  if (!ac) { return; }
  if (ac.state !== 'running') { return; }
  if (S.nodes.length > 24) { return; }
  var t = ac.currentTime;
  var d = ms / 1000;
  var g = ac.createGain();
  g.gain.setValueAtTime(vol / 32768, t);
  g.gain.linearRampToValueAtTime(0, t + d);
  g.connect(ac.destination);
  var src = S.makeSource(w, freq, t);
  if (w !== 3) {
    src.frequency.linearRampToValueAtTime(Math.max(1, freq * (100 - fall) / 100), t + d);
  }
  src.connect(g);
  S.track(src);
  src.start(t); src.stop(t + d);
  S.hitEnd = Math.max(S.hitEnd, t + d);
~
: hit ( freq ms -- ) wave beep-volume pitch-drop js-hit ;
JSWORD: silence { }
  context.sdl.silence();
~
JSWORD: beeping? { -- f }
  var S = context.sdl;
  return (S.ac && Math.max(S.beepEnd, S.hitEnd) > S.ac.currentTime) ? -1 : 0;
~
JSWORD: audio-open { }
  context.sdl.ensureAudio();
~
JSWORD: audio-open? { -- f }
  var S = context.sdl;
  return (S.ac && S.ac.state === 'running') ? -1 : 0;
~

( ---- Images, the words of sdl2-image ---- )
\ The browser loads a file in the background: load-image waits for it. A name
\ with directories that is not found is tried again without them, as on Linux.
JSWORD: js-load-image { a n -- h }
  return context.sdl.loadImage(GetString(a, n));
~
JSWORD: image-state { h -- n }
  var r = context.sdl.images[h];
  return r ? r.state : -1;
~
: load-image ( a n -- img )
  js-load-image { h }
  begin h image-state 0= while yield repeat
  h image-state 0< if s" image not found" type cr -1 throw then
  h ;
JSWORD: free-image { h }
  var S = context.sdl;
  if (S.images[h]) { S.images[h] = null; }
~
JSWORD: image-w { h -- n }
  var r = context.sdl.images[h];
  return r ? r.w : 0;
~
JSWORD: image-h { h -- n }
  var r = context.sdl.images[h];
  return r ? r.h : 0;
~
: image-size ( img -- w h ) dup image-w swap image-h ;
JSWORD: draw-part { img sx sy sw sh x y w h }
  context.sdl.drawImage(img, sx, sy, sw, sh, x, y, w, h);
~
JSWORD: draw-size { img x y w h }
  var S = context.sdl;
  var r = S.images[img];
  if (r) { S.drawImage(img, 0, 0, r.w, r.h, x, y, w, h); }
~
JSWORD: draw { img x y }
  var S = context.sdl;
  var r = S.images[img];
  if (r) { S.drawImage(img, 0, 0, r.w, r.h, x, y, r.w, r.h); }
~
JSWORD: image-alpha { img a }
  var r = context.sdl.images[img];
  if (r) { r.alpha = a & 255; }
~
JSWORD: image-tint { img c }
  var r = context.sdl.images[img];
  if (r) { r.tint = c & 16777215; }
~

previous previous previous
sdl2 definitions
| evaluate ;
: sdl2-image ( -- ) sdl2 ;   ( the image words are part of sdl2 here )
