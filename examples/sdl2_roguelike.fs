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

\ A minimal roguelike with SDL2 and a tile sheet: random rooms and corridors,
\ line of sight, monsters that chase you, potions and stairs down. One turn
\ per move. The 8x8 tiles come from examples/sdl2_tiles.png (a row of 8 tiles:
\ wall, floor, stairs, potion, player, monster, skull, heart), drawn with
\ sdl2-image; run it from the ueforth directory or from examples/. It also
\ runs in the browser (web/sdl2_roguelike.html). examples/make_sdl2_tiles.fs
\ draws the sheet (make_sdl2_tiles.py is the same in Python), edit it to
\ change the tiles.
\   arrows or w a s d    move; walk into a monster to hit it
\   space or .           wait a turn (or start again after dying)
\   click                step toward the tile you click
\   m                    music on or off
\   esc                  quit
\ Bottom bar: your health (hearts), the stairs icon and the depth.
\ Green potions heal, the blue stairs lead one level deeper, the screen flashes
\ red when you are hurt. Tiles you saw but cannot see now are drawn darker.
\ Needs libSDL2 and libSDL2_image on Linux.
\
\ Music: two short tunes written in ABC notation (the text format of
\ abcnotation.com) below, played by sdl2-abc. On Linux they are played as MIDI
\ through SDL2_mixer, with the soundfont sdl2_roguelike.sf2 if you put one
\ beside this file (any General MIDI .sf2 or .sf3, renamed), else the one of
\ the system. In the browser a simple synthesizer plays them. If nothing can
\ play them the game stays silent.

sdl2
sdl2-image
sdl2-mixer
sdl2-abc

40 constant map-w   24 constant map-h   8 constant tile   map-w map-h * constant area
create map area allot      ( 0 wall, 1 floor, 2 stairs, 3 potion )
create seen area allot     ( explored )
create vis area allot      ( in view now )
create room-cx 12 cells allot   create room-cy 12 cells allot
create mons-data 24 3 * cells allot   ( x y hp of each monster )
create was 256 allot   was 256 erase

0 value rooms   0 value mons
0 value px   0 value py   0 value hp   10 constant max-hp
0 value level   0 value dead   0 value flash   -1 value music-on
0 value rng   0 value repeat-at   0 value was-click

( ---- Music, in ABC notation ---- )
: theme r~ X:1
T:Crypt
M:4/4
L:1/8
Q:1/4=88
K:Am
%%MIDI program 46
"Am"A,2 [A,C]2 E2 [A,C]2 | "F"F,2 [F,A,]2 C2 [F,A,]2 |
"G"G,2 [G,B,]2 D2 [G,B,]2 | "E"E,2 [E,^G,]2 B,2 [E,^G,]2 |
A2 c2 e2 c2 | f2 a2 c'2 a2 | g2 e2 d2 B2 | A6 z2 |
~ ;
: dirge r~ X:2
T:Dirge
M:4/4
L:1/4
Q:1/4=72
K:Am
%%MIDI program 48
c B A ^G | A3 z | [A,,E,A,]4 |
~ ;

: no-music ( a n -- )   ." (no music: the tune could not be played)" cr 2drop ;
: start-theme  music-on if theme ['] abc-loop catch if no-music then then ;
: start-dirge  music-on if dirge ['] abc-play catch if no-music then then ;
: toggle-music
  music-on if 0 to music-on tune-stop else -1 to music-on start-theme then ;

( ---- Random numbers and map access ---- )
: rnd ( n -- 0..n-1 )
  rng 1103515245 * 12345 + $7fffffff and dup to rng   16 rshift swap mod ;

: in-map? ( x y -- f )
  dup 0 >= swap map-h < and  swap dup 0 >= swap map-w < and  and ;
: tile@ ( x y -- n ) 2dup in-map? if map-w * + map + c@ else 2drop 0 then ;
: tile! ( n x y -- ) map-w * + map + c! ;
: carve ( x y -- ) 1 -rot tile! ;
: visible? ( x y -- f ) map-w * + vis + c@ 0<> ;
: dir ( n -- -1 | 0 | 1 ) dup 0< if drop -1 exit then 0 > if 1 else 0 then ;

( ---- Monsters ---- )
: mon ( i -- a ) 3 cells * mons-data + ;
: mx ( i -- x ) mon @ ;         : my ( i -- y ) mon cell+ @ ;
: mhp ( i -- hp ) mon 2 cells + @ ;
: mx! ( x i -- ) mon ! ;        : my! ( y i -- ) mon cell+ ! ;
: mhp! ( hp i -- ) mon 2 cells + ! ;

: mon-at ( x y -- i | -1 )   ( the living monster on a tile )
  { x y }  -1 { found }
  mons 0 ?do
    i mx x =  i my y = and  i mhp 0 > and if i to found then
  loop
  found ;

( ---- Building a level ---- )
: dig-room { x y w h }
  h 0 do w 0 do  x i + y j + carve  loop loop ;
: dig-h ( x1 x2 y -- ) { x1 x2 y }
  x1 x2 max 1+   x1 x2 min ?do i y carve loop ;
: dig-v ( y1 y2 x -- ) { y1 y2 x }
  y1 y2 max 1+   y1 y2 min ?do x i carve loop ;

: add-room
  6 rnd 4 + { w }   4 rnd 3 + { h }
  map-w w - 2 - rnd 1 + { x }   map-h h - 2 - rnd 1 + { y }
  x y w h dig-room
  x w 2 / + { cx }   y h 2 / + { cy }
  rooms 0 > if
    rooms 1 - cells room-cx + @   cx   rooms 1 - cells room-cy + @   dig-h
    rooms 1 - cells room-cy + @   cy   cx   dig-v
  then
  cx rooms cells room-cx + !   cy rooms cells room-cy + !
  1 +to rooms ;

: free? { x y }   ( a plain floor tile with nobody on it )
  x y tile@ 1 =   x y mon-at 0<  and   x px = y py = and 0=  and ;
: random-floor ( -- x y )
  begin  map-w rnd map-h rnd 2dup free? 0=  while  2drop  repeat ;

: add-monster
  random-floor  mons my!  mons mx!
  2 rnd 2 + level 3 / +  mons mhp!
  1 +to mons ;

: far-room ( -- i )   ( the room whose center is farthest from the first one )
  0 { best }   0 { far }
  rooms 1 ?do
    i cells room-cx + @ room-cx @ - abs   i cells room-cy + @ room-cy @ - abs +
    dup far > if to far i to best else drop then
  loop
  best ;

: new-level
  map area erase   seen area erase   vis area erase
  0 to rooms   0 to mons
  6 rnd 6 + 0 do add-room loop
  far-room dup cells room-cx + @  swap cells room-cy + @   2 -rot tile!
  room-cx @ to px   room-cy @ to py
  level 3 + 12 min 0 do add-monster loop
  2 rnd 2 + 0 do random-floor 3 -rot tile! loop ;

( ---- Line of sight ---- )
: clear-line? { x0 y0 x1 y1 }   ( no wall between two tiles )
  x1 x0 - { dx }   y1 y0 - { dy }
  dx abs dy abs max { n }
  n 2 < if -1 exit then
  -1 { ok }
  n 1 ?do
    x0 dx i * n / +   y0 dy i * n / +   tile@ 0= if 0 to ok then
  loop
  ok ;

: see-cell { x y }
  x y in-map? 0= if exit then
  x px - dup *   y py - dup * +   64 > if exit then
  px py x y clear-line? if
    1 x y map-w * + vis + c!   1 x y map-w * + seen + c!
  then ;

: (update-fov) ( -- )
  vis area erase
  17 0 do 17 0 do  px i + 8 -  py j + 8 -  see-cell  loop loop ;

( ---- Turns ---- )
: hurt ( n -- )
  negate +to hp   6 to flash   120 100 beep
  hp 1 < if 1 to dead   start-dirge then ;

: mon-step { i nx ny }   ( moves a monster when the way is free )
  nx ny tile@ 0= if 0 exit then
  nx px = ny py = and if 0 exit then
  nx ny mon-at 0< 0= if 0 exit then
  nx i mx!   ny i my!   -1 ;

: monster-act { i }
  i mhp 1 < if exit then
  i mx { x }   i my { y }
  x y visible? 0= if exit then
  px x - { dx }   py y - { dy }
  dx abs dy abs + 1 = if  2 rnd 1 + level 4 / +  hurt exit then
  dx abs dy abs >= if
    i  x dx dir +  y  mon-step 0= if i x  y dy dir +  mon-step drop then
  else
    i  x  y dy dir +  mon-step 0= if i  x dx dir +  y  mon-step drop then
  then ;

: monsters-act   mons 0 ?do dead 0= if i monster-act then loop ;

: start-level
  new-level (update-fov) ;

: new-game
  1 to level   max-hp to hp   0 to dead   0 to flash
  ticks to rng   start-level   start-theme ;

: next-level
  1 +to level   hp 3 + max-hp min to hp   start-level
  440 80 beep   660 120 beep ;

: attack ( i -- )
  { i }  3 rnd 1 + { d }
  i mhp d - i mhp!
  i mhp 1 < if 300 60 beep else 200 40 beep then ;

: try-step { dx dy }   ( -- acted? )
  px dx + { nx }   py dy + { ny }
  nx ny mon-at dup 0< 0= if attack -1 exit then drop
  nx ny tile@ 0= if 0 exit then
  nx to px   ny to py
  px py tile@ 3 = if
    1 px py tile!   hp 5 + max-hp min to hp   600 80 beep
  then
  px py tile@ 2 = if next-level 0 exit then
  -1 ;

: act ( dx dy -- )   ( one turn: the player, then the monsters )
  try-step if (update-fov) monsters-act then ;

( ---- Keys and mouse ---- )
: edge? ( k -- f ) dup pressed? swap 255 and was + c@ 0= and ;
: note-key ( k -- ) dup pressed? swap 255 and was + c! ;
: note-keys
  key-left note-key  key-right note-key  key-up note-key  key-down note-key
  key-space note-key  [char] . note-key  [char] m note-key
  [char] a note-key  [char] d note-key  [char] w note-key  [char] s note-key ;

: key-step? ( k -- f )   ( pressed now, or held long enough to repeat )
  dup edge? if drop ticks 250 + to repeat-at -1 exit then
  pressed? 0= if 0 exit then
  ticks repeat-at - 0< if 0 exit then
  ticks 90 + to repeat-at -1 ;

: handle-keys
  [char] m edge? if toggle-music then
  dead if
    key-space edge? if new-game then
    note-keys exit
  then
  key-left key-step?   [char] a key-step? or  if -1 0 act then
  key-right key-step?  [char] d key-step? or  if 1 0 act then
  key-up key-step?     [char] w key-step? or  if 0 -1 act then
  key-down key-step?   [char] s key-step? or  if 0 1 act then
  key-space key-step?  [char] . key-step? or  if 0 0 act then
  note-keys ;

: click-step ( -- )
  mouse-y tile / map-h >= if exit then
  mouse-x tile / px - { dx }   mouse-y tile / py - { dy }
  dx abs dy abs >= if dx dir 0 else 0 dy dir then act ;

: handle-mouse
  LEFT-BUTTON pressed? { down }
  down was-click 0= and if  dead if new-game else click-step then  then
  down to was-click ;

( ---- Drawing ---- )
0 value tiles   ( the tile sheet: wall floor stairs potion player monster skull heart )
: shade ( $rrggbb -- ) tiles swap image-tint ;   ( multiplies the colors of the next tiles )
: put-big { n x y size }   ( tile n of the sheet, scaled to size pixels )
  tiles  n tile *  0  tile tile  x y size size  draw-part ;
: put-tile ( n x y -- ) tile put-big ;

create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
: draw-digit { d x y s }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;
: draw-number { n x y s }   ( one or two digits )
  n 10 / dup if  x y s draw-digit  else drop then
  n 10 mod  x 4 s * +  y s draw-digit ;

: draw-tile { x y }
  x y map-w * + { k }
  k seen + c@ 0= if exit then
  k vis + c@ { v }
  v if $ffffff else $707090 then shade
  x tile *  y tile * { sx sy }
  x y tile@ { t }
  t 0= if 0 sx sy put-tile exit then
  1 sx sy put-tile
  t 2 = if 2 sx sy put-tile then
  t 3 = if 3 sx sy put-tile then ;

: draw-map   map-h 0 do map-w 0 do  i j draw-tile  loop loop ;

: draw-monsters
  $ffffff shade
  mons 0 ?do
    i mhp 0 > if
      i mx i my visible? if 5 i mx tile *  i my tile * put-tile then
    then
  loop ;

: draw-player
  flash 0 > if $ff6060 else $ffffff then shade
  4 px tile *  py tile * put-tile ;

: draw-bar
  $0c0e18 color   0 map-h tile *  320 16 box
  max-hp 0 do
    i hp < if $ffffff else $402020 then shade   7  4 i 9 * +  196  put-tile
  loop
  $ffffff shade   2 248 196 put-tile
  white color   level 260 196 2 draw-number ;

: draw-end
  red color   110 alpha   0 0 320 map-h tile * box
  $ffffff shade   6 136 50 48 put-big
  white color   level  level 10 < if 151 else 139 then  108 6 draw-number ;

: draw-all
  $000000 color cls
  draw-map draw-monsters draw-player draw-bar
  flash 0 > if red color 60 alpha 0 0 320 map-h tile * box then
  dead if draw-end then
  flip ;

: step ( -- )
  events
  key-esc pressed? if quit! then
  handle-keys handle-mouse
  flash 0 > if -1 +to flash then
  draw-all
  dt 16 < if 16 dt - delay then ;

: run ( -- ) begin step quit? until tiles free-image close-screen ;

." arrows or wasd: move and hit  space: wait  click: step  m: music  esc: quit" cr
3 to zoom
s" sdl2_roguelike.sf2" soundfont? drop
60 music-volume
320 208 screen
s" Roguelike" title
s" examples/sdl2_tiles.png" load-image to tiles
new-game
run
bye
