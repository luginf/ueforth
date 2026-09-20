#! /usr/bin/env ueforth

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

\ A minimal drum machine with SDL2: 4 drums (kick, snare, hat, tom) on 16 steps.
\ The drums are made of noise and waves, so no sound files are needed.
\ Everything can be done with the mouse alone:
\   click / drag   switch steps on and off (the first cell you click decides)
\   play button    play / stop
\   - and +        tempo -1 / +1 bpm with the left button, -5 / +5 with the
\                  right button (hold to repeat)
\   colored pads   play a drum live (the squares on the left of the rows)
\   CLR            clear the pattern
\   X              quit
\ and with the keyboard:
\   space          play / stop        up / down   tempo, 5 bpm at a time
\                                      (the mouse wheel too)
\   1 2 3 4        play a drum live   c           clear
\   esc            quit
\ Row colors: red kick, orange snare, yellow hat, cyan tom.

sdl2

320 constant field-w   124 constant field-h
20 constant grid-x   40 constant grid-y   18 constant pitch   16 constant cell-size
16 constant steps   4 constant tracks

create pattern steps tracks * allot   pattern steps tracks * erase
create flash tracks cells allot       flash tracks cells erase
create was 256 allot                  was 256 erase
create track-colors red , orange , yellow , cyan ,

120 value bpm
0 value playing   0 value step   0 value next-at
-1 value hover   0 value painting   0 value paint   0 value was-btn
-1 value pad-hover   -1 value btn-hover   -1 value held-btn   0 value repeat-at

( ---- Drums: volume, wave, pitch fall in percent, frequency, milliseconds ---- )
: drum { vol w fall freq ms }
  vol to beep-volume   w to wave   fall to pitch-drop   freq ms hit ;
: kick    9000 1 75 150 130 drum ;
: snare   5000 3 0 200 110 drum   2500 1 30 220 70 drum ;
: hat     2000 3 0 200 35 drum ;
: tom     6000 1 50 200 180 drum ;
create drums ' kick , ' snare , ' hat , ' tom ,

: trigger ( track -- )
  ticks over cells flash + !
  cells drums + @ execute ;

( ---- Pattern ---- )
: row! ( a n track -- )   ( x switches a step on, anything else off )
  { track }  steps min 0 ?do
    dup i + c@ [char] x = if 1 else 0 then   track steps * i + pattern + c!
  loop drop ;

: new-pattern
  s" x.....x...x....." 0 row!
  s" ....x.......x..." 1 row!
  s" x.x.x.x.x.x.x.x." 2 row!
  s" ..............x." 3 row! ;

( ---- Sequencer ---- )
: step-ms ( -- ms ) 15000 bpm / ;   ( a step is a sixteenth note )
: tempo ( n -- ) bpm + 40 max 240 min to bpm ;
: tempo-up 5 tempo ;
: tempo-down -5 tempo ;
: clear-all pattern steps tracks * erase ;
: toggle-play
  playing 0= to playing
  playing if ticks to next-at   steps 1 - to step then ;
: play-step
  tracks 0 do  i steps * step + pattern + c@ if i trigger then  loop ;
: sequence
  playing 0= if exit then
  ticks { now }
  now next-at - 0< if exit then
  now next-at - 200 > if now to next-at then
  step 1+ steps mod to step   play-step
  step-ms +to next-at ;

( ---- Keys and mouse ---- )
: edge? ( k -- f ) dup pressed? swap 255 and was + c@ 0= and ;
: remember ( k -- ) dup pressed? swap 255 and was + c! ;
: remember-keys
  key-space remember  key-up remember  key-down remember
  [char] c remember   tracks 0 do [char] 1 i + remember loop ;

: handle-keys
  key-space edge? if toggle-play then
  key-up edge? if tempo-up then
  key-down edge? if tempo-down then
  wheel 5 * tempo
  [char] c edge? if clear-all then
  tracks 0 do [char] 1 i + edge? if i trigger then loop
  remember-keys ;

: cell-at ( x y -- i )   ( step under a pixel, or -1 outside the grid )
  grid-y - dup 0< if 2drop -1 exit then
  pitch / dup tracks >= if 2drop -1 exit then
  swap grid-x - dup 0< if 2drop -1 exit then
  pitch / dup steps >= if 2drop -1 exit then
  swap steps * + ;

( The buttons of the top bar: play/stop, tempo -, tempo +, clear, quit )
create btn-x 8 , 44 , 98 , 140 , 288 ,
create btn-w 24 , 20 , 20 , 30 , 24 ,
create btn-actions ' toggle-play , ' tempo-down , ' tempo-up , ' clear-all , ' quit! ,
5 constant buttons   5 constant btn-y   20 constant btn-h

: inside? { x y w h }   ( is the mouse in this rectangle? )
  mouse-x x -   dup 0 >= swap w < and
  mouse-y y -   dup 0 >= swap h < and
  and ;

: button-at ( -- id )   ( button under the mouse, or -1 )
  -1 { found }
  buttons 0 ?do
    i cells btn-x + @  btn-y  i cells btn-w + @  btn-h  inside? if i to found then
  loop
  found ;

: pad-at ( -- track )   ( drum pad under the mouse, or -1 )
  mouse-x 18 < 0= if -1 exit then
  mouse-y grid-y - dup 0< if drop -1 exit then
  pitch / dup tracks >= if drop -1 then ;

: mouse-btn ( -- n )   ( 1 while the left button is down, 2 for the right one, else 0 )
  LEFT-BUTTON pressed? if 1 exit then
  RIGHT-BUTTON pressed? if 2 exit then
  0 ;

: run-tempo ( id b -- )   ( button 1 lowers, 2 raises; 1 bpm, or 5 with the right button )
  2 = if 5 else 1 then
  swap 1 = if negate then tempo ;

: press-button ( id b -- )   ( b is the mouse button that pressed it )
  { id b }
  id to held-btn   ticks 400 + to repeat-at
  id 1 = id 2 = or if id b run-tempo exit then   ( the tempo buttons take both )
  b 1 = if id cells btn-actions + @ execute then ;

: repeat-button ( b -- )   ( the tempo buttons repeat while held )
  held-btn 1 <  held-btn 2 > or if drop exit then
  btn-hover held-btn <> if drop exit then
  ticks repeat-at - 0< if drop exit then
  held-btn swap run-tempo   80 +to repeat-at ;

: handle-mouse
  mouse-x mouse-y cell-at to hover
  pad-at to pad-hover
  button-at to btn-hover
  mouse-btn { b }
  b 0= if 0 to painting   -1 to held-btn then
  b was-btn 0= and { fresh }   ( the button that was just pressed, or 0 )
  fresh if btn-hover 0 >= if btn-hover b press-button then then
  fresh 1 = if pad-hover 0 >= if pad-hover trigger then then
  fresh 1 =   hover 0 >= and if
    hover pattern + c@ 0= 1 and to paint
    paint if hover steps / trigger then
    -1 to painting
  then
  painting hover 0 >= and if paint hover pattern + c! then
  b if b repeat-button then
  b to was-btn ;

( ---- Drawing ---- )
create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
  $7927 , $4927 , $7bf5 , $5aad ,   ( C L R X )

: draw-digit { d x y s }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;

: draw-bpm { x y s }   ( three digits, without a leading zero )
  bpm 100 /   dup if x y s draw-digit else drop then
  bpm 10 / 10 mod   x 4 s * +  y s draw-digit
  bpm 10 mod   x 8 s * +  y s draw-digit ;

: cell-x ( col -- x ) pitch * grid-x + ;
: cell-y ( row -- y ) pitch * grid-y + ;
: track-color ( track -- $rrggbb ) cells track-colors + @ ;
: here? ( col -- f ) step = playing and ;

: cell-color ( track col -- $rrggbb )
  { t c }
  t steps * c + pattern + c@
  if   c here? if white else t track-color then
  else t steps * c + hover = if $3a4680
       else c 4 / 2 mod if $1c2440 else $28325a then then
  then ;

: draw-grid
  tracks 0 do
    steps 0 do
      j i cell-color color
      i cell-x  j cell-y  cell-size cell-size box
    loop
  loop ;

: draw-chips   ( a pad per drum, white for a moment when it plays )
  tracks 0 do
    ticks flash i cells + @ - 120 < if white else i track-color then color
    4 i cell-y 3 +  10 10 box
    i pad-hover = if white color  2 i cell-y 1 +  14 14 frame then
  loop ;

: draw-beats
  steps 0 do
    i here? if white else i 4 mod 0= if gray else $28325a then then color
    i cell-x 6 +  31  4 4 box
  loop ;

: button-color ( id -- $rrggbb )
  dup btn-hover = if
    LEFT-BUTTON pressed? if drop $5a6aa0 else drop $3a4680 then exit
  then
  0= playing and if $1c7c1c else $28325a then ;

: icon-play { x w }   ( a triangle, or a square while playing )
  x w 12 - 2 / + { cx }
  playing if
    white color   cx 1 + btn-y 5 +  10 10 box
  else
    green color
    12 0 do  cx  btn-y 4 + i +  i 6 < if i 1+ else 12 i - then 2 *  hline  loop
  then ;
: icon-minus ( x w -- ) 10 - 2 / +  btn-y 9 +  10 2 box ;
: icon-plus ( x w -- )
  2dup icon-minus   2 - 2 / +  btn-y 5 +  2 10 box ;
: icon-clear ( x w -- )
  22 - 2 / + { gx }
  10 gx btn-y 5 + 2 draw-digit
  11 gx 8 + btn-y 5 + 2 draw-digit
  12 gx 16 + btn-y 5 + 2 draw-digit ;
: icon-quit ( x w -- ) 6 - 2 / +  13 swap btn-y 5 + 2 draw-digit ;

: draw-button { id }
  id cells btn-x + @ { x }   id cells btn-w + @ { w }
  id button-color color   x btn-y w btn-h box
  white color
  id 0 = if x w icon-play then
  id 1 = if x w icon-minus then
  id 2 = if x w icon-plus then
  id 3 = if x w icon-clear then
  id 4 = if x w icon-quit then ;

: draw-buttons   buttons 0 do i draw-button loop ;

: draw-all
  $101830 color cls
  draw-buttons
  white color   71 10 2 draw-bpm
  draw-beats draw-grid draw-chips
  flip ;

: frame-step ( -- )
  events
  key-esc pressed? if quit! then
  handle-keys handle-mouse sequence draw-all
  dt 8 < if 8 dt - delay then ;

: run ( -- ) begin frame-step quit? until close-screen ;

." click: steps and buttons  space: play/stop  up/down: tempo  1-4: drums  c: clear  esc: quit" cr
3 to zoom
field-w field-h screen
s" Drum machine" title
new-pattern
run
bye
