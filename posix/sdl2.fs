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

( Lazy load bindings for SDL2: window, drawing, input, timing, beeps )
\
\ Usage:
\   sdl2                     \ load; sdl2 becomes the vocabulary on top and the
\                            \ one your own words are defined in
\   320 240 screen           \ open a window
\   red color  10 10 50 50 box  flip
\
\ The vocabulary holds the raw C names (SDL_Init, SDL_RenderFillRect, ...)
\ and short words on top of them (screen, color, box, line, flip, ...).
\ Only libSDL2 is needed here. See sdl2_image.fs, sdl2_ttf.fs, sdl2_mixer.fs.
\ NOTE: the block below is a string, so it must not contain a vertical bar.

: sdl2 r|

forth also posix also structures
vocabulary sdl2   also sdl2 definitions

z" libSDL2-2.0.so.0" shared-library sdl

( Raw bindings, same names and argument order as the C API )
z" SDL_Init" 1 sdl SDL_Init ( n -- n )
z" SDL_InitSubSystem" 1 sdl SDL_InitSubSystem ( n -- n )
z" SDL_Quit" 0 sdl sdl-shutdown ( -- void )   \ not SDL_Quit: same name as the SDL_QUIT event, case is ignored
z" SDL_GetError" 0 sdl SDL_GetError ( -- z )
z" SDL_SetHint" 2 sdl SDL_SetHint ( z z -- n )
z" SDL_GetTicks" 0 sdl SDL_GetTicks ( -- n )
z" SDL_Delay" 1 sdl SDL_Delay ( n -- void )
z" SDL_CreateWindow" 6 sdl SDL_CreateWindow ( z x y w h flags -- a )
z" SDL_DestroyWindow" 1 sdl SDL_DestroyWindow ( a -- void )
z" SDL_SetWindowTitle" 2 sdl SDL_SetWindowTitle ( a z -- void )
z" SDL_SetWindowFullscreen" 2 sdl SDL_SetWindowFullscreen ( a n -- n )
z" SDL_ShowCursor" 1 sdl SDL_ShowCursor ( n -- n )
z" SDL_CreateRenderer" 3 sdl SDL_CreateRenderer ( a n n -- a )
z" SDL_DestroyRenderer" 1 sdl SDL_DestroyRenderer ( a -- void )
z" SDL_RenderSetLogicalSize" 3 sdl SDL_RenderSetLogicalSize ( a n n -- n )
z" SDL_SetRenderDrawColor" 5 sdl SDL_SetRenderDrawColor ( a r g b a -- n )
z" SDL_SetRenderDrawBlendMode" 2 sdl SDL_SetRenderDrawBlendMode ( a n -- n )
z" SDL_RenderClear" 1 sdl SDL_RenderClear ( a -- n )
z" SDL_RenderPresent" 1 sdl SDL_RenderPresent ( a -- void )
z" SDL_RenderDrawPoint" 3 sdl SDL_RenderDrawPoint ( a x y -- n )
z" SDL_RenderDrawLine" 5 sdl SDL_RenderDrawLine ( a x y x y -- n )
z" SDL_RenderDrawRect" 2 sdl SDL_RenderDrawRect ( a a -- n )
z" SDL_RenderFillRect" 2 sdl SDL_RenderFillRect ( a a -- n )
z" SDL_RenderCopy" 4 sdl SDL_RenderCopy ( a a a a -- n )
z" SDL_RWFromFile" 2 sdl SDL_RWFromFile ( z z -- a )
z" SDL_RenderReadPixels" 5 sdl SDL_RenderReadPixels ( a a n a n -- n )
z" SDL_CreateTexture" 5 sdl SDL_CreateTexture ( a n n n n -- a )
z" SDL_CreateTextureFromSurface" 2 sdl SDL_CreateTextureFromSurface ( a a -- a )
z" SDL_UpdateTexture" 4 sdl SDL_UpdateTexture ( a a a n -- n )
z" SDL_QueryTexture" 5 sdl SDL_QueryTexture ( a a a a a -- n )
z" SDL_SetTextureColorMod" 4 sdl SDL_SetTextureColorMod ( a r g b -- n )
z" SDL_SetTextureAlphaMod" 2 sdl SDL_SetTextureAlphaMod ( a n -- n )
z" SDL_SetTextureBlendMode" 2 sdl SDL_SetTextureBlendMode ( a n -- n )
z" SDL_DestroyTexture" 1 sdl SDL_DestroyTexture ( a -- void )
z" SDL_FreeSurface" 1 sdl SDL_FreeSurface ( a -- void )
z" SDL_PollEvent" 1 sdl SDL_PollEvent ( a -- n )
z" SDL_WaitEvent" 1 sdl SDL_WaitEvent ( a -- n )
z" SDL_PushEvent" 1 sdl SDL_PushEvent ( a -- n )
z" SDL_GetMouseState" 2 sdl SDL_GetMouseState ( a a -- n )
z" SDL_GetKeyboardState" 1 sdl SDL_GetKeyboardState ( a -- a )
z" SDL_OpenAudioDevice" 5 sdl SDL_OpenAudioDevice ( z n a a n -- n )
z" SDL_CloseAudioDevice" 1 sdl SDL_CloseAudioDevice ( n -- void )
z" SDL_PauseAudioDevice" 2 sdl SDL_PauseAudioDevice ( n n -- void )
z" SDL_QueueAudio" 3 sdl SDL_QueueAudio ( n a n -- n )
z" SDL_GetQueuedAudioSize" 1 sdl SDL_GetQueuedAudioSize ( n -- n )
z" SDL_ClearQueuedAudio" 1 sdl SDL_ClearQueuedAudio ( n -- void )

( Constants )
$00000010 constant SDL_INIT_AUDIO
$00000020 constant SDL_INIT_VIDEO
$00004000 constant SDL_INIT_EVENTS
$00000004 constant SDL_WINDOW_SHOWN
$00000020 constant SDL_WINDOW_RESIZABLE
$00001001 constant SDL_WINDOW_FULLSCREEN_DESKTOP
$2FFF0000 constant SDL_WINDOWPOS_CENTERED
$00000001 constant SDL_RENDERER_SOFTWARE
$00000002 constant SDL_RENDERER_ACCELERATED
$00000004 constant SDL_RENDERER_PRESENTVSYNC
0 constant SDL_BLENDMODE_NONE
1 constant SDL_BLENDMODE_BLEND
2 constant SDL_BLENDMODE_ADD
$16362004 constant SDL_PIXELFORMAT_ARGB8888
$0100 constant SDL_QUIT
$0300 constant SDL_KEYDOWN
$0301 constant SDL_KEYUP
$0400 constant SDL_MOUSEMOTION
$0401 constant SDL_MOUSEBUTTONDOWN
$0402 constant SDL_MOUSEBUTTONUP
$0403 constant SDL_MOUSEWHEEL
$8010 constant AUDIO_S16SYS

( Field offsets inside an SDL_Event, a 56 byte union )
create ev 64 allot
: ev-type ( -- n ) ev ul@ ;

( Small helpers )
: sdl-error ( -- a n ) SDL_GetError z>s ;
: ?sdl ( n -- ) sign-extend 0< if sdl-error type cr -1 throw then ;
: 8bit ( n -- n ) $ff and ;

( ---- Window and renderer ---- )
0 value window-handle
0 value renderer
0 value width
0 value height
1 value zoom
0 value quitting
0 value audio-dev
0 value close-hook   ( optional xt run by close-screen, used by the mixer )

: screen-open? ( -- f ) renderer 0<> ;

: close-screen ( -- )
  renderer if renderer SDL_DestroyRenderer drop 0 to renderer then
  window-handle if window-handle SDL_DestroyWindow drop 0 to window-handle then
  close-hook if close-hook execute then
  audio-dev if audio-dev SDL_CloseAudioDevice drop 0 to audio-dev then
  sdl-shutdown drop
;

: make-renderer ( -- a )
  window-handle -1 SDL_RENDERER_ACCELERATED SDL_RENDERER_PRESENTVSYNC or
    SDL_CreateRenderer dup if exit then drop
  window-handle -1 SDL_RENDERER_SOFTWARE SDL_CreateRenderer
;

: screen ( w h -- )
  screen-open? if close-screen then
  to height to width
  SDL_INIT_VIDEO SDL_INIT_EVENTS or SDL_Init ?sdl
  z" uEforth" SDL_WINDOWPOS_CENTERED SDL_WINDOWPOS_CENTERED
    width zoom * height zoom * SDL_WINDOW_SHOWN SDL_WINDOW_RESIZABLE or
    SDL_CreateWindow dup 0= if sdl-error type cr -1 throw then
  to window-handle
  make-renderer dup 0= if sdl-error type cr -1 throw then to renderer
  renderer width height SDL_RenderSetLogicalSize drop
  renderer SDL_BLENDMODE_BLEND SDL_SetRenderDrawBlendMode drop
  0 to quitting
;

: title ( a n -- ) s>z window-handle swap SDL_SetWindowTitle drop ;
: fullscreen ( f -- )
  window-handle swap if SDL_WINDOW_FULLSCREEN_DESKTOP else 0 then
  SDL_SetWindowFullscreen drop
;
: cursor ( f -- ) SDL_ShowCursor drop ;

( ---- Colors and drawing ---- )
0 value pen-r   0 value pen-g   0 value pen-b   255 value pen-a
0 value pen   ( packed for SDL_Color: r + g<<8 + b<<16 + a<<24 )

: rgba ( r g b a -- )
  8bit to pen-a   8bit to pen-b   8bit to pen-g   8bit to pen-r
  pen-r pen-g 8 lshift or pen-b 16 lshift or pen-a 24 lshift or to pen
  renderer pen-r pen-g pen-b pen-a SDL_SetRenderDrawColor drop
;
: rgb ( r g b -- ) 255 rgba ;
: color ( $rrggbb -- )
  dup 16 rshift 8bit   over 8 rshift 8bit   rot 8bit   rgb
;
: alpha ( a -- ) { a } pen-r pen-g pen-b a rgba ;

$000000 constant black    $ffffff constant white
$ff0000 constant red      $00c000 constant green
$0000ff constant blue     $ffff00 constant yellow
$00ffff constant cyan     $ff00ff constant magenta
$ff8000 constant orange   $800080 constant purple
$ff80c0 constant pink     $804000 constant brown
$808080 constant gray

: cls ( -- ) renderer SDL_RenderClear drop ;
: dot ( x y -- ) renderer -rot SDL_RenderDrawPoint drop ;
: line ( x1 y1 x2 y2 -- )
  { x1 y1 x2 y2 } renderer x1 y1 x2 y2 SDL_RenderDrawLine drop ;

create rect 4 cells allot   ( SDL_Rect: int x, y, w, h )
: rect-at! ( x y w h a -- ) { a } a 12 + l!  a 8 + l!  a 4 + l!  a l! ;
: rect! ( x y w h -- ) rect rect-at! ;
: box ( x y w h -- ) rect! renderer rect SDL_RenderFillRect drop ;
: frame ( x y w h -- ) rect! renderer rect SDL_RenderDrawRect drop ;

: hline ( x y w -- ) 1 rect! renderer rect SDL_RenderFillRect drop ;

0 value span-x
: span-step ( i r -- )
  dup * { r2 }  ( i on stack )
  begin span-x dup * over dup * + r2 > while -1 +to span-x repeat drop
;
: disc ( cx cy r -- )
  { cx cy r }
  r to span-x
  r 1+ 0 ?do
    i r span-step
    cx span-x - cy i + span-x 2* 1+ hline
    i if cx span-x - cy i - span-x 2* 1+ hline then
  loop
;
: dots4 { cx cy a b }
  cx a + cy b + dot   cx a - cy b + dot
  cx a + cy b - dot   cx a - cy b - dot
;
: circle ( cx cy r -- )
  { cx cy r }
  r to span-x
  r 1+ 0 ?do
    i r span-step
    cx cy span-x i dots4
    cx cy i span-x dots4
  loop
;

create px 4 allot
: pixel@ ( x y -- $rrggbb ) ( read back one pixel of the screen )
  1 1 rect!  renderer rect SDL_PIXELFORMAT_ARGB8888 px 4 SDL_RenderReadPixels ?sdl
  px ul@ $ffffff and ;

( ---- Files: a name is also looked for beside the program ---- )
: base-name ( a n -- a' n' )   ( a file name without its directories )
  { a n }  0 { cut }
  n 0 ?do a i + c@ [char] / = if i 1+ to cut then loop
  a cut +  n cut - ;

( ---- Timing ---- )
: ticks ( -- ms ) SDL_GetTicks $ffffffff and ;
0 value audio-hook   ( optional xt run by flip and delay to keep hits playing )
: run-audio-hook ( -- ) audio-hook if audio-hook execute then ;
: delay ( ms -- )   ( waits, and keeps hits playing meanwhile )
  ticks + { end }
  begin
    run-audio-hook
    end ticks -  dup 0 >
  while  10 min SDL_Delay drop  repeat
  drop
;
0 value last-flip
0 value dt   ( milliseconds taken by the last frame )

: flip ( -- )
  renderer SDL_RenderPresent drop
  run-audio-hook
  ticks { now }
  now last-flip - to dt   now to last-flip
;

( ---- Input ---- )
255 constant LEFT-BUTTON
254 constant MIDDLE-BUTTON
253 constant RIGHT-BUTTON
208 constant key-left    207 constant key-right
210 constant key-up      209 constant key-down
32 constant key-space    13 constant key-enter    27 constant key-esc
9 constant key-tab       8 constant key-backspace

0 value mouse-x   0 value mouse-y
0 value last-key
0 value wheel
create keys 256 allot   keys 256 erase

: key-index ( sym scancode -- n )
  swap dup $40000000 and if drop $7f and 128 + else nip $ff and then ;
: key-event ( f -- )
  ev 20 + sl@ ev 16 + sl@ key-index { f k }
  f k keys + c!
  f if k to last-key then
;
: button-event ( f -- )
  256 ev 16 + c@ - keys + c!
  ev 20 + sl@ to mouse-x   ev 24 + sl@ to mouse-y
;
: handle-event ( -- )
  ev-type
  dup SDL_QUIT = if drop -1 to quitting exit then
  dup SDL_KEYDOWN = if drop 1 key-event exit then
  dup SDL_KEYUP = if drop 0 key-event exit then
  dup SDL_MOUSEMOTION = if drop
    ev 20 + sl@ to mouse-x   ev 24 + sl@ to mouse-y exit then
  dup SDL_MOUSEBUTTONDOWN = if drop 1 button-event exit then
  dup SDL_MOUSEBUTTONUP = if drop 0 button-event exit then
  SDL_MOUSEWHEEL = if ev 20 + sl@ +to wheel then
;
: events ( -- )
  0 to wheel
  begin ev SDL_PollEvent sign-extend while handle-event repeat
;
: wait ( -- ) ev SDL_WaitEvent sign-extend if handle-event then ;
: quit? ( -- f ) quitting 0<> ;
: quit! ( -- ) -1 to quitting ;
: fold-case ( k -- k ) dup 65 >= over 90 <= and if 32 + then ;
: pressed? ( k -- f ) fold-case $ff and keys + c@ 0<> ;

( ---- Beeps: square, triangle, saw and noise waves through a queue ---- )
44100 constant sample-rate
0 value wave       ( 0 square, 1 triangle, 2 saw, 3 noise )
3000 value beep-volume  ( 0 to 32767 )
create audio-spec 32 allot

( Voices of the hits, see below. Fields, in cells: phase, frequency, samples left,
  samples in all, wave, pitch drop, volume )
8 constant #voices
7 cells constant voice-size
create voices #voices voice-size * allot
: voice ( i -- a ) voice-size * voices + ;
: clear-voices ( -- ) voices #voices voice-size * erase ;

: audio-open? ( -- f ) audio-dev 0<> ;
: audio-open ( -- )
  audio-open? if exit then
  SDL_INIT_AUDIO SDL_InitSubSystem sign-extend 0< if exit then
  audio-spec 32 erase
  sample-rate audio-spec l!
  AUDIO_S16SYS audio-spec 4 + w!
  1 audio-spec 6 + c!
  1024 audio-spec 8 + w!
  0 0 audio-spec 0 0 SDL_OpenAudioDevice $ffffffff and to audio-dev
  audio-open? if clear-voices audio-dev 0 SDL_PauseAudioDevice drop then
;

0 value seed
: noise-sample ( -- n ) seed 1103515245 * 12345 + $7fffffff and to seed
                        seed 16 rshift 32767 and ;

: wave-sample ( phase -- n )    ( phase is 0 to 65535, result -32768 to 32767 )
  wave 1 = if dup 32768 < if 2* else 65535 swap - 2* then 32768 - exit then
  wave 2 = if 32768 - exit then
  wave 3 = if drop noise-sample 16384 - 2* exit then
  32768 < if 32767 else -32767 then
;

: fill-beep { freq n buf }
  0 { phase }
  n 0 ?do
    phase wave-sample beep-volume * 15 arshift
    i n 441 - > if n i - * 441 / then
    buf i 2* + w!
    freq 65536 * sample-rate / +to phase   phase $ffff and to phase
  loop
;
: beep ( freq ms -- )
  audio-open
  audio-open? 0= if 2drop exit then
  sample-rate * 1000 / { n }  { freq }
  n 2* allocate throw { buf }
  freq n buf fill-beep
  audio-dev buf n 2* SDL_QueueAudio drop
  buf free drop
;

( ---- Hits: percussive sounds that overlap, mixed by software ---- )
\ A beep waits for the ones before it. A hit starts at once and plays over the
\ others (8 at a time, the oldest is cut), fading out linearly, and its pitch can
\ fall while it plays. The mix is queued a few milliseconds ahead by flip, delay
\ and hit itself, so call flip or delay regularly.
0 value pitch-drop   ( percent the pitch falls during a hit, 0 to 100 )
2205 constant latency   ( samples kept queued, 50 ms )
create mix 4096 cells allot
create mixed 4096 2* allot

: left@ ( i -- n ) voice 2 cells + @ ;
: voices-active ( -- n ) 0 #voices 0 ?do i left@ 0<> if 1+ then loop ;
: voice-free ( -- i )   ( an idle voice, or else the one closest to its end )
  0 { best }
  #voices 1 ?do i left@ best left@ < if i to best then loop
  best
;
: pitch-at ( freq drop elapsed total -- hz )   ( frequency part way through a hit )
  */ 100 swap - swap 100 */
;

: mix-voice { v n }   ( adds up to n samples of a voice to mix )
  v @ { phase }   v 1 cells + @ { freq }   v 2 cells + @ { left }
  v 3 cells + @ { total }   v 5 cells + @ { drop% }   v 6 cells + @ { vol }
  left n min { m }
  wave { keep }   v 4 cells + @ to wave
  m 0 ?do
    phase wave-sample vol * 15 arshift
    left i - 256 * total / *  8 arshift
    mix i cells + +!
    freq drop% total left - i + total pitch-at  65536 * sample-rate / +to phase
    phase $ffff and to phase
  loop
  keep to wave
  phase v !   left m - v 2 cells + !
;
: render { n buf }   ( mixes the voices into n samples of buf )
  mix n cells erase
  #voices 0 ?do i voice n mix-voice loop
  n 0 ?do mix i cells + @ 32767 min -32768 max buf i 2* + w! loop
;
: pump ( -- )   ( tops the queue up with the mix of the voices )
  audio-open? 0= if exit then
  voices-active 0= if exit then
  latency audio-dev SDL_GetQueuedAudioSize $ffffffff and 2/ - { n }
  n 1 < if exit then
  n mixed render
  audio-dev mixed n 2* SDL_QueueAudio drop
;
' pump to audio-hook

: start-voice ( freq ms -- )
  sample-rate * 1000 / { n }  { freq }
  voice-free voice { v }
  0 v !   freq v 1 cells + !   n v 2 cells + !   n v 3 cells + !
  wave v 4 cells + !   pitch-drop v 5 cells + !   beep-volume v 6 cells + !
;
: hit ( freq ms -- )
  audio-open
  audio-open? 0= if 2drop exit then
  start-voice pump
;

: silence ( -- )
  audio-open? if audio-dev SDL_ClearQueuedAudio drop clear-voices then ;
: beeping? ( -- f )
  audio-open? if
    audio-dev SDL_GetQueuedAudioSize 0<>  voices-active 0<>  or
  else 0 then
;

previous previous
sdl2 definitions
| evaluate ;
