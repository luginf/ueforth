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

\ Small SDL2 tour: window, shapes, keyboard, mouse and beeps.
\   arrows  move the red square
\   click   moves the white ring
\   esc     quits (or close the window)
\ Needs libSDL2 (sudo apt install libsdl2-2.0-0).

sdl2

320 value w   240 value h
w 2 / value px   h 2 / value py       ( the square )
60 value bx   40 value by             ( the ball )
2 value vx    2 value vy
w 2 / value cx   h 2 / value cy       ( the ring )

: bounce ( -- ) 660 30 beep ;

: move-ball
  vx +to bx   vy +to by
  bx 6 < bx w 6 - > or if vx negate to vx bounce then
  by 6 < by h 6 - > or if vy negate to vy bounce then
;

: move-square
  key-left pressed? if -3 +to px then
  key-right pressed? if 3 +to px then
  key-up pressed? if -3 +to py then
  key-down pressed? if 3 +to py then
  px 0 max w 16 - min to px
  py 0 max h 16 - min to py
;

: follow-click
  LEFT-BUTTON pressed? if mouse-x to cx  mouse-y to cy then ;

: draw-all
  $202040 color cls
  red color     px py 16 16 box
  yellow color  bx by 6 disc
  white color   cx cy 12 circle
  gray color    0 0 w h frame
  flip
;

: step ( -- )
  events
  key-esc pressed? if quit! then
  move-square move-ball follow-click
  draw-all
  dt 16 < if 16 dt - delay then
;

: run ( -- ) begin step quit? until close-screen ;

3 to zoom
w h screen
s" uEforth SDL2 demo" title
run
bye
