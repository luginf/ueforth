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

\ Draws examples/sdl2_tiles.png, the tile sheet of sdl2_roguelike.fs.
\
\ The tiles are written below as text, 8 letters by 8 lines each, and each
\ letter is looked up in the palette ('.' is transparent). Edit them and run
\     out/posix/ueforth examples/make_sdl2_tiles.fs
\ from the ueforth directory to write the sheet again: a 64x8 PNG holding the
\ 8 tiles side by side, in the order they are listed. No SDL is needed: the
\ PNG is written by hand (CRC-32, Adler-32, and a "stored" zlib block, so the
\ file is not compressed, which is fine for 2 KB).
\ examples/make_sdl2_tiles.py does the same in Python, to compare the two: it
\ writes the same pixels, in 352 bytes since zlib is there to compress them.

( ---- Palette ---- )
: letters s" abcFfCDGgHwyYkrRWsS" ;
create colors
  $2a2a48 , $4a4a78 , $6a6aa8 , $181c30 , $2c3050 , $60e0ff , $2a90b0 ,
  $40ff80 , $20a050 , $c0ffd0 , $a08060 , $ffe060 , $c09030 , $302010 ,
  $ff5050 , $a02020 , $ffffff , $e0e0e0 , $9090a0 ,

: color-of ( char -- $rrggbb )
  letters { c a n }
  -1 { found }
  n 0 ?do a i + c@ c = if i to found then loop
  found 0< if ." unknown letter " c emit cr -1 throw then
  found cells colors + @ ;

( ---- The tiles ---- )
create art 512 allot   0 value art-n
: row ( a n -- ) { a n }   a art art-n + n cmove   n +to art-n ;

\ 0 wall
s" bbbbabbb" row   s" bcbbabcb" row   s" aaaaaaaa" row   s" bbabbbbb" row
s" bcbabbcb" row   s" aaaaaaaa" row   s" bbbbabbb" row   s" bcbbabcb" row
\ 1 floor
s" FFFFFFFF" row   s" FFFFFFFF" row   s" FFFfFFFF" row   s" FFFFFFFF" row
s" FFFFFFFF" row   s" FFFFFfFF" row   s" FFFFFFFF" row   s" FfFFFFFF" row
\ 2 stairs, on a floor
s" CCCCFFFF" row   s" CCCCFFFF" row   s" DDCCCCFF" row   s" DDCCCCFF" row
s" FFDDCCCC" row   s" FFDDCCCC" row   s" FFFFDDCC" row   s" FFFFDDCC" row
\ 3 potion
s" ...ww..." row   s" ...ww..." row   s" ..gGGg.." row   s" .gGGGGg." row
s" .gGHGGg." row   s" .gGGGGg." row   s" ..gGGg.." row   s" ........" row
\ 4 player
s" ..yyyy.." row   s" ..ykky.." row   s" ...YY..." row   s" .yyyyyy." row
s" y.yyyy.y" row   s" ..yYYy.." row   s" ..y..y.." row   s" .YY..YY." row
\ 5 monster
s" ..rrrr.." row   s" .rrrrrr." row   s" rWRrrRWr" row   s" rrrrrrrr" row
s" rrrrrrrr" row   s" .rrrrrr." row   s" .rRrrRr." row   s" r......r" row
\ 6 skull
s" ..ssss.." row   s" .ssssss." row   s" sskssksS" row   s" sskssksS" row
s" .ssssss." row   s" ..skks.." row   s" ..s.s.s." row   s" ........" row
\ 7 heart
s" .rr.rr.." row   s" rrrrrrR." row   s" rrrrrrR." row   s" rrrrrrR." row
s" .rrrrR.." row   s" ..rrR..." row   s" ...R...." row   s" ........" row

( ---- Pixels: 8 rows of a filter byte then 64 RGBA pixels ---- )
create raw 2056 allot   0 value raw-n
: r, ( byte -- ) raw raw-n + c!   1 +to raw-n ;

: pixel, ( char -- )
  dup [char] . = if drop 0 r, 0 r, 0 r, 0 r, exit then
  color-of { c }
  c 16 rshift $ff and r,   c 8 rshift $ff and r,   c $ff and r,   255 r, ;

: make-raw
  0 to raw-n
  8 0 do                     \ rows, k
    0 r,                     \ PNG filter: none
    8 0 do                   \ tiles, j
      8 0 do                 \ pixels of the row, i
        k 8 *  j 64 * +  i +  art + c@ pixel,
      loop
    loop
  loop ;

( ---- PNG file ---- )
create png 4096 allot   0 value png-n
: b, ( byte -- ) png png-n + c!   1 +to png-n ;
: be32, ( n -- )
  dup 24 rshift $ff and b,   dup 16 rshift $ff and b,
  dup 8 rshift $ff and b,   $ff and b, ;
: le16, ( n -- ) dup $ff and b,   8 rshift $ff and b, ;
: bytes, ( a n -- ) 0 ?do dup i + c@ b, loop drop ;

0 value crc
: crc-byte ( byte -- )
  crc xor to crc
  8 0 do
    crc 1 and if crc 1 rshift $edb88320 xor else crc 1 rshift then to crc
  loop ;
: crc32 ( a n -- crc )
  $ffffffff to crc
  0 ?do dup i + c@ crc-byte loop drop
  crc $ffffffff xor $ffffffff and ;

0 value ada   0 value adb
: adler32 ( a n -- x )
  1 to ada   0 to adb
  0 ?do
    dup i + c@ ada + 65521 mod to ada
    adb ada + 65521 mod to adb
  loop drop
  adb 16 lshift ada or ;

0 value chunk-start
: chunk-open ( len tag-a -- )   ( the 4 letters of the tag are in the CRC )
  swap be32,   png-n to chunk-start   4 bytes, ;
: chunk-close ( -- )
  png chunk-start +  png-n chunk-start -  crc32 be32, ;

: make-png
  0 to png-n
  $89 b, [char] P b, [char] N b, [char] G b, $0d b, $0a b, $1a b, $0a b,
  13 s" IHDR" drop chunk-open
    64 be32, 8 be32, 8 b, 6 b, 0 b, 0 b, 0 b,       \ 64x8, 8 bit RGBA
  chunk-close
  raw-n 11 + s" IDAT" drop chunk-open
    $78 b, $01 b,                                     \ zlib header
    1 b, raw-n le16, raw-n invert $ffff and le16,     \ one stored block
    raw raw-n bytes,
    raw raw-n adler32 be32,
  chunk-close
  0 s" IEND" drop chunk-open chunk-close ;

: out-name ( -- a n )   ( written in examples/ when run from the ueforth directory )
  s" examples/make_sdl2_tiles.fs" file-exists?
  if s" examples/sdl2_tiles.png" else s" sdl2_tiles.png" then ;

make-raw
make-png
png png-n out-name dump-file
." wrote " out-name type ."  (" png-n . ." bytes)" cr
bye
