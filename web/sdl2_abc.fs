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

( Lazy load tunes in ABC notation, played with WebAudio )
\
\ The same words as posix/sdl2_abc.fs. There is no MIDI synthesizer or
\ soundfont in a browser: the notes are played by simple oscillators (the
\ triangle wave, or the one chosen with wave), one voice per note.
\ NOTE: the block below is a string, so it must not contain a vertical bar.

also internals
\ sdl2 is looked up when this runs, by evaluate: written directly it would be the
\ loader that existed when this was compiled, which would load everything again.
: sdl2-abc
  s" sdl2" evaluate
  abc-parser evaluate
  r|
sdl2 definitions also web also abc-int

JSWORD: js-tune-setup { }
  var S = context.sdl;
  S.tune = []; S.tuneNodes = []; S.tuneTimer = 0; S.tuneUntil = 0;
  S.tuneLoop = 0; S.tuneTotal = 0; S.tuneW = 1; S.tuneVol = 2500;
  S.tuneNote = function(key, t, d) {
    var ac = S.ac;
    var freq = 440 * Math.pow(2, (key - 69) / 12);
    var g = ac.createGain();
    var v = S.tuneVol / 32768;
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(v, t + 0.01);
    g.gain.linearRampToValueAtTime(0, t + d);
    g.connect(ac.destination);
    var src = S.makeSource(S.tuneW, freq, t);
    src.connect(g);
    S.track(src);
    S.tuneNodes.push(src);
    src.start(t); src.stop(t + d + 0.02);
  };
  S.tuneRun = function() {
    S.ensureAudio();
    var ac = S.ac;
    if (ac) {
      if (ac.state === 'running') {
        var t0 = ac.currentTime + 0.05;
        for (var i = 0; i < S.tune.length; ++i) {
          var n = S.tune[i];
          S.tuneNote(n[0], t0 + n[1] / 1000, n[2] / 1000);
        }
        S.tuneUntil = t0 + S.tuneTotal / 1000;
      }
    }
    if (S.tuneLoop) { S.tuneTimer = setTimeout(S.tuneRun, S.tuneTotal); }
  };
  S.tuneRestart = function() {   // the sound was just allowed: start a waiting loop now
    if (S.tuneLoop) {
      clearTimeout(S.tuneTimer);
      S.tuneRun();
    }
  };
  S.tuneStop = function() {
    S.tuneLoop = 0;
    clearTimeout(S.tuneTimer);
    for (var i = 0; i < S.tuneNodes.length; ++i) {
      try { S.tuneNodes[i].stop(); } catch (e) {}
    }
    S.tuneNodes = []; S.tuneUntil = 0;
  };
~
js-tune-setup
JSWORD: js-tune-clear { }
  context.sdl.tune = [];
~
JSWORD: js-tune-add { key at ms }
  context.sdl.tune.push([key, at, ms]);
~
JSWORD: js-tune-run { lp total w vol }
  var S = context.sdl;
  S.tuneStop();
  S.tuneLoop = lp; S.tuneTotal = total; S.tuneW = w; S.tuneVol = vol;
  S.tuneRun();
~
JSWORD: tune-stop { }
  context.sdl.tuneStop();
~
JSWORD: tune-playing? { -- f }
  var S = context.sdl;
  if (S.tuneLoop) { return -1; }
  return (S.ac && S.tuneUntil > S.ac.currentTime) ? -1 : 0;
~

: ticks>ms ( ticks -- ms ) 125 * tune-bpm / ;
: tune-send ( loop -- )
  js-tune-clear
  n-notes 0 ?do  i nk@  i nt@ ticks>ms  i nd@ ticks>ms  js-tune-add  loop
  tune-ms  wave 1 = wave 0 = or if 1 else wave then  tune-level  js-tune-run ;
: tune-play ( -- ) 0 tune-send ;
: tune-loop ( -- ) -1 tune-send ;
: abc-play ( a n -- ) abc tune-play ;
: abc-loop ( a n -- ) abc tune-loop ;

previous previous
  | evaluate ;
previous
