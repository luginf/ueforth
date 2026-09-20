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

( Tests of sdl2.fs )
\
\ Needs libSDL2, so this is not part of the default build. Run headless with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ueforth posix/sdl2_tests.fs

needs ../common/testing.fs

sdl2 definitions

( Inject a fake event, as if the user had done it )
create pushed 64 allot
: push-event ( a b type -- ) pushed 64 erase  pushed l!  pushed 16 + l!  pushed 20 + l!
                             pushed SDL_PushEvent ?sdl ;
: push-key ( sym scancode type -- ) push-event ;
: push-mouse ( x y type button -- ) pushed 64 erase  pushed 16 + c!  pushed l!
                                    pushed 24 + l!  pushed 20 + l!
                                    pushed SDL_PushEvent ?sdl ;

: test-screen
  64 48 screen
  width 64 =assert   height 48 =assert
  screen-open? assert
  close-screen
  screen-open? 0= assert
;

: test-reopen
  32 32 screen   16 16 screen
  width 16 =assert
  close-screen
;

: test-box
  64 48 screen
  black color cls
  red color 10 10 20 20 box
  15 15 pixel@ $ff0000 =assert
  5 5 pixel@ 0 =assert
  40 40 pixel@ 0 =assert
  close-screen
;

: test-rgb-and-named-colors
  64 48 screen
  0 255 0 rgb  0 0 8 8 box
  4 4 pixel@ $00ff00 =assert
  blue color  0 0 8 8 box
  4 4 pixel@ $0000ff =assert
  $102030 color  0 0 8 8 box
  4 4 pixel@ $102030 =assert
  white color  0 0 8 8 box
  4 4 pixel@ $ffffff =assert
  close-screen
;

: test-alpha-blend
  64 48 screen
  black color cls
  white color 128 alpha  0 0 8 8 box
  4 4 pixel@ $ff and dup 120 >assert 136 <assert
  close-screen
;

: test-dot-and-line
  64 48 screen
  black color cls
  white color  3 3 dot
  3 3 pixel@ $ffffff =assert
  4 3 pixel@ 0 =assert
  0 10 20 10 line
  0 10 pixel@ $ffffff =assert
  20 10 pixel@ $ffffff =assert
  10 10 pixel@ $ffffff =assert
  10 11 pixel@ 0 =assert
  close-screen
;

: test-frame
  64 48 screen
  black color cls
  white color  10 10 10 10 frame
  10 10 pixel@ $ffffff =assert
  19 19 pixel@ $ffffff =assert
  15 15 pixel@ 0 =assert
  close-screen
;

: test-disc
  64 48 screen
  black color cls
  yellow color  30 24 8 disc
  30 24 pixel@ $ffff00 =assert
  30 17 pixel@ $ffff00 =assert
  38 24 pixel@ $ffff00 =assert
  22 24 pixel@ $ffff00 =assert
  40 24 pixel@ 0 =assert
  30 14 pixel@ 0 =assert
  close-screen
;

: test-circle
  64 48 screen
  black color cls
  white color  30 24 8 circle
  38 24 pixel@ $ffffff =assert
  22 24 pixel@ $ffffff =assert
  30 16 pixel@ $ffffff =assert
  30 32 pixel@ $ffffff =assert
  30 24 pixel@ 0 =assert
  close-screen
;

: test-keys
  64 48 screen
  events
  [char] a pressed? 0= assert
  [char] a 4 SDL_KEYDOWN push-key   events
  [char] a pressed? assert
  [char] A pressed? assert
  [char] a last-key =assert
  [char] a 4 SDL_KEYUP push-key   events
  [char] a pressed? 0= assert
  close-screen
;

: test-special-keys
  64 48 screen
  $40000050 80 SDL_KEYDOWN push-key   events
  key-left pressed? assert
  key-right pressed? 0= assert
  $4000004f 79 SDL_KEYDOWN push-key   events
  key-right pressed? assert
  32 44 SDL_KEYDOWN push-key   events
  key-space pressed? assert
  27 41 SDL_KEYDOWN push-key   events
  key-esc pressed? assert
  close-screen
;

: test-mouse
  64 48 screen
  10 20 SDL_MOUSEMOTION 0 push-mouse   events
  mouse-x 10 =assert   mouse-y 20 =assert
  LEFT-BUTTON pressed? 0= assert
  11 21 SDL_MOUSEBUTTONDOWN 1 push-mouse   events
  LEFT-BUTTON pressed? assert
  RIGHT-BUTTON pressed? 0= assert
  11 21 SDL_MOUSEBUTTONUP 1 push-mouse   events
  LEFT-BUTTON pressed? 0= assert
  close-screen
;

: test-quit
  64 48 screen
  events
  quit? 0= assert
  0 0 SDL_QUIT push-event   events
  quit? assert
  close-screen
;

: test-timing
  ticks { t0 }
  20 delay
  ticks t0 - 15 >assert
  64 48 screen
  flip flip
  dt 0 >= assert
  close-screen
;

: test-beep
  64 48 screen
  audio-open
  audio-open? if
    beeping? 0= assert
    440 200 beep
    beeping? assert
    silence
    beeping? 0= assert
    1 to wave  880 50 beep  2 to wave  880 50 beep  3 to wave  880 50 beep
    beeping? assert
    silence
    0 to wave
  then
  close-screen
;

: reset-hits   0 to wave   0 to pitch-drop   3000 to beep-volume   clear-voices ;

create hit-buf 4096 2* allot
: peak ( a n -- max )   ( largest absolute sample of n starting at a )
  0 { a n m }  n 0 ?do a i 2* + sw@ abs m max to m loop  m ;
: crossings ( a n -- k )   ( how many times the wave changes sign )
  0 { a n k }  n 1 - 0 ?do
    a i 2* + sw@ 0<  a i 1+ 2* + sw@ 0<  <> if k 1+ to k then
  loop  k ;

: test-hit-one
  reset-hits
  2 to wave  440 100 start-voice
  voices-active 1 =assert
  1000 hit-buf render
  wave 2 =assert
  hit-buf 1000 peak dup 2900 >assert 3001 <assert
  reset-hits
;

: test-hit-mix
  reset-hits
  440 100 start-voice   440 100 start-voice
  voices-active 2 =assert
  1000 hit-buf render
  hit-buf 1000 peak 5000 >assert
  reset-hits
;

: test-hit-decay-and-end
  reset-hits
  440 100 start-voice
  4096 hit-buf render
  hit-buf 100 peak
  hit-buf 3996 2* + 100 peak   ( the end of the buffer, 9% of the volume left )
  5 * swap <assert
  voices-active 1 =assert
  400 hit-buf render
  voices-active 0 =assert
  reset-hits
;

: test-hit-pitch-drop
  reset-hits
  100 to pitch-drop  400 200 start-voice
  4096 hit-buf render
  hit-buf 1024 crossings
  hit-buf 3072 2* + 1024 crossings
  >assert
  reset-hits
;

: test-hit-voice-limit
  reset-hits
  12 0 do 440 100 start-voice loop
  voices-active 8 =assert
  reset-hits
;

: test-hit-noise-and-silence
  reset-hits
  3 to wave  200 100 start-voice
  1000 hit-buf render
  hit-buf 1000 peak 1000 >assert
  audio-open
  audio-open? if silence voices-active 0 =assert beeping? 0= assert then
  reset-hits
;

: test-hit-plays
  64 48 screen  audio-open
  audio-open? if
    reset-hits
    440 100 hit
    voices-active 1 =assert
    beeping? assert
    200 delay
    voices-active 0 =assert
    silence
  then
  close-screen
;

forth definitions
sdl2
run-tests
bye
