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

( Tests of sdl2_image.fs, sdl2_ttf.fs and sdl2_mixer.fs )
\
\ Needs libSDL2_image, libSDL2_ttf and libSDL2_mixer. Run headless with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ueforth posix/sdl2_media_tests.fs

needs ../common/testing.fs

sdl2-image
sdl2-ttf
sdl2-mixer
sdl2 definitions

( Sound and image files are generated into /tmp by the tests )
: bmp-name s" /tmp/ueforth_sdl2_test.bmp" ;
: wav-name s" /tmp/ueforth_sdl2_test.wav" ;
: font-name s" /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" ;

( 2x2 pixel 24 bit BMP: red green on top, blue white below )
create bmp 70 allot
: bmp-build
  bmp 70 erase
  $4d42 bmp w!   70 bmp 2 + l!   54 bmp 10 + l!
  40 bmp 14 + l!   2 bmp 18 + l!   2 bmp 22 + l!
  1 bmp 26 + w!   24 bmp 28 + w!   16 bmp 34 + l!
  ( bottom row first, each pixel stored as b g r, rows padded to 8 bytes )
  $ff bmp 54 + c!   0 bmp 55 + c!   0 bmp 56 + c!
  $ff bmp 57 + c!   $ff bmp 58 + c!   $ff bmp 59 + c!
  0 bmp 62 + c!   0 bmp 63 + c!   $ff bmp 64 + c!
  0 bmp 65 + c!   $ff bmp 66 + c!   0 bmp 67 + c!
  bmp 70 bmp-name dump-file
;

( 0.1 second of 440 Hz square wave, 16 bit mono 22050 Hz )
4410 constant wav-data-size
create wav 44 wav-data-size + allot
: wav-build
  wav 44 wav-data-size + erase
  s" RIFF" wav swap cmove   36 wav-data-size + wav 4 + l!
  s" WAVEfmt " wav 8 + swap cmove   16 wav 16 + l!
  1 wav 20 + w!   1 wav 22 + w!   22050 wav 24 + l!   44100 wav 28 + l!
  2 wav 32 + w!   16 wav 34 + w!
  s" data" wav 36 + swap cmove   wav-data-size wav 40 + l!
  wav-data-size 2 / 0 do
    i 25 / 1 and if 8000 else -8000 then  wav 44 + i 2* + w!
  loop
  wav 44 wav-data-size + wav-name dump-file
;

create region 64 24 * 4 * allot
: region-nonblack ( -- n )
  0 0 64 24 rect!
  renderer rect SDL_PIXELFORMAT_ARGB8888 region 256 SDL_RenderReadPixels ?sdl
  0 64 24 * 0 do region i 4* + ul@ $ffffff and if 1+ then loop ;

: test-image
  bmp-build
  64 48 screen
  black color cls
  bmp-name load-image { img }
  img image-size 2 =assert 2 =assert
  img 0 0 draw
  0 0 pixel@ $ff0000 =assert
  1 0 pixel@ $00ff00 =assert
  0 1 pixel@ $0000ff =assert
  1 1 pixel@ $ffffff =assert
  img 10 10 8 8 draw-size
  12 12 pixel@ $ff0000 =assert
  16 12 pixel@ $00ff00 =assert
  12 16 pixel@ $0000ff =assert
  16 16 pixel@ $ffffff =assert
  img 1 0 1 1 30 30 4 4 draw-part
  31 31 pixel@ $00ff00 =assert
  img free-image
  close-screen
;

: test-image-missing
  64 48 screen
  s" /nonexistent/nothing.png" ['] load-image catch
  if 2drop else drop 0 assert then
  expect-reset  ( the error message was printed on purpose )
  close-screen
;

: test-image-beside   ( a name with missing directories is tried again without them )
  bmp-build
  bmp 70 s" ueforth_sdl2_test_here.bmp" dump-file
  64 48 screen
  s" no-such-dir/ueforth_sdl2_test_here.bmp" load-image { img }
  img image-size 2 =assert 2 =assert
  img free-image
  s" ueforth_sdl2_test_here.bmp" delete-file drop
  close-screen
;

: test-text
  64 48 screen
  black color cls
  font-name 16 load-font { f }
  f font
  s" Hi" text-size 0 >assert 0 >assert
  white color  2 2 s" Hi" text
  region-nonblack 10 >assert
  black color cls
  region-nonblack 0 =assert
  1 2 s" " text
  region-nonblack 0 =assert
  f free-font
  close-screen
;

: test-sound
  wav-build
  64 48 screen
  wav-name load-sound { snd }
  sounds-playing 0 =assert
  snd play
  sounds-playing 0 >assert
  stop-sounds
  sounds-playing 0 =assert
  64 sound-volume
  snd free-sound
  close-screen
;

: test-music
  wav-build
  64 48 screen
  wav-name load-music { mus }
  music? 0= assert
  mus music
  music? assert
  stop-music
  music? 0= assert
  64 music-volume
  mus free-music
  close-screen
;

: test-mixer-closed-with-screen
  wav-build
  64 48 screen
  wav-name load-sound free-sound
  mixer-open? assert
  close-screen
  mixer-open? 0= assert
;

forth definitions
sdl2
run-tests
bye
