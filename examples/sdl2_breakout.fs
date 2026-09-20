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

\ Breakout with SDL2. Only libSDL2 is needed (no fonts, images or sound files).
\   left / right  move the paddle, or move the mouse
\   space / click launch the ball, or start again after the end
\   esc           quit (or close the window)
\ The score is drawn with a tiny 3x5 pixel font, and the sounds are beeps.
\ Screen at the end: red tint = game over, green tint = you cleared the wall.

sdl2

320 constant field-w   240 constant field-h
10 constant cols   5 constant rows
32 constant cell-w   14 constant cell-h   30 constant top

create bricks cols rows * allot
create row-colors red , orange , yellow , green , cyan ,

0 value bricks-left   0 value score   3 value lives
0 value state          ( 0 waiting, 1 playing, 2 game over, 3 won )

48 constant paddle-w   6 constant paddle-h   224 constant paddle-y
0 value paddle-x
0 value last-mouse-x
6 constant ball-size
0 value ball-x   0 value ball-y   0 value ball-vx   0 value ball-vy

( ---- Bricks ---- )
: row-color ( row -- $rrggbb ) cells row-colors + @ ;

: brick-at ( x y -- a )   ( brick cell under a pixel, or 0 outside the wall )
  top - dup 0< if 2drop 0 exit then
  cell-h / dup rows >= if 2drop 0 exit then
  swap dup 0< if 2drop 0 exit then
  cell-w / dup cols >= if 2drop 0 exit then
  swap cols * + bricks + ;

: hit-brick? ( x y -- f )   ( break the brick at a pixel, if any )
  brick-at dup 0= if exit then
  dup c@ 0= if drop 0 exit then
  dup bricks - cols / 60 * 300 + 40 beep
  0 swap c!   -1 +to bricks-left   10 +to score   -1 ;

: reset-bricks
  bricks cols rows * 1 fill
  cols rows * to bricks-left ;

( ---- Ball and paddle ---- )
: park-ball
  paddle-x paddle-w 2 / + ball-size 2 / - to ball-x
  paddle-y ball-size - to ball-y
  0 to ball-vx   0 to ball-vy ;

: new-game
  reset-bricks   0 to score   3 to lives
  field-w paddle-w - 2 / to paddle-x
  park-ball   0 to state ;

: launch   2 to ball-vx   -3 to ball-vy   1 to state ;

: fire? ( -- f ) key-space pressed? LEFT-BUTTON pressed? or ;

: move-paddle
  mouse-x last-mouse-x <> if
    mouse-x paddle-w 2 / - to paddle-x   mouse-x to last-mouse-x
  then
  key-left pressed? if -5 +to paddle-x then
  key-right pressed? if 5 +to paddle-x then
  paddle-x 0 max field-w paddle-w - min to paddle-x ;

: hit-x? ( -- f )
  ball-x   ball-vx 0 > if ball-size + then
  ball-y ball-size 2 / +   hit-brick? ;

: hit-y? ( -- f )
  ball-x ball-size 2 / +
  ball-y   ball-vy 0 > if ball-size + then   hit-brick? ;

: move-ball-x
  ball-vx +to ball-x
  hit-x? if ball-vx negate to ball-vx then
  ball-x 0< if 0 to ball-x   ball-vx abs to ball-vx   330 20 beep then
  ball-x ball-size + field-w > if
    field-w ball-size - to ball-x   ball-vx abs negate to ball-vx   330 20 beep
  then ;

: on-paddle? ( -- f )
  ball-vy 0 >
  ball-y ball-size + paddle-y >= and
  ball-y paddle-y paddle-h + < and
  ball-x ball-size + paddle-x > and
  ball-x paddle-x paddle-w + < and ;

: bounce-paddle
  paddle-y ball-size - to ball-y
  ball-vy abs negate to ball-vy
  ball-x ball-size 2 / + paddle-x paddle-w 2 / + -   6 /   -4 max 4 min
  dup 0= if drop ball-vx 0< if -1 else 1 then then
  to ball-vx
  220 30 beep ;

: lose-life
  -1 +to lives
  lives 0= if
    2 to state   392 150 beep  330 150 beep  262 300 beep
  else
    park-ball   0 to state   150 250 beep
  then ;

: move-ball-y
  ball-vy +to ball-y
  hit-y? if ball-vy negate to ball-vy then
  ball-y 0< if 0 to ball-y   ball-vy abs to ball-vy   330 20 beep then
  on-paddle? if bounce-paddle then
  ball-y field-h > if lose-life then ;

: win   3 to state   523 100 beep  659 100 beep  784 100 beep  1047 300 beep ;

: update
  move-paddle
  state 0 = if
    park-ball   fire? if launch then exit
  then
  state 1 = if
    move-ball-x   state 1 = if move-ball-y then
    bricks-left 0= state 1 = and if win then exit
  then
  fire? if new-game then ;

( ---- Drawing ---- )
create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,

: draw-digit { d x y s }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;

: draw-number { n x y s }   ( four digits, each 3*s wide plus s of space )
  4 0 do
    n 10 mod   x 3 i - s 4 * * +   y s draw-digit
    n 10 / to n
  loop ;

: draw-bricks
  cols rows * 0 do
    i bricks + c@ if
      i cols / row-color color
      i cols mod cell-w * 1+   i cols / cell-h * top + 1+
        cell-w 2 -   cell-h 2 -   box
    then
  loop ;

: draw-lives
  lives 0 ?do   gray color   field-w 12 - i 10 * -  6  6 6 box   loop ;

: draw-end
  state 2 = if red else green then color   90 alpha
  0 0 field-w field-h box
  white color   score  field-w 60 - 2 /  100  4  draw-number ;

: draw-all
  $101830 color cls
  draw-bricks
  white color   paddle-x paddle-y paddle-w paddle-h box
  yellow color  ball-x ball-y ball-size ball-size box
  white color   score 6 6 2 draw-number
  draw-lives
  state 2 >= if draw-end then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  update draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until close-screen ;

3 to zoom
field-w field-h screen
s" Breakout" title
0 cursor
new-game
run
bye
