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

( Tests of the ABC reader, the MIDI file and the soundfont words )
\
\ Needs libSDL2_mixer, so this is not part of the default build. Run with:
\   SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy ueforth posix/sdl2_abc_tests.fs

needs ../common/testing.fs

sdl2 sdl2-mixer sdl2-abc
sdl2 definitions also abc-int

: ev ( i -- key start ticks ) dup nk@ swap dup nt@ swap nd@ ;
: =ev ( i key start ticks -- )
  { i k s d }  i nk@ k =assert   i nt@ s =assert   i nd@ d =assert ;

: tune-plain r~ X:1
T:Plain "a chord symbol" !fermata!
K:C
L:1/4
CDEF ~ ;
: test-plain
  tune-plain abc
  tune-notes 4 =assert
  0 60 0 480 =ev   1 62 480 480 =ev   2 64 960 480 =ev   3 65 1440 480 =ev
  tune-ms 2000 =assert
  tune-bpm 120 =assert   tune-program 0 =assert ;

: tune-key r~ K:G
L:1/8
F2 ^F f, F' =F | F ~ ;
: test-key-and-octaves
  tune-key abc
  0 66 0 480 =ev      ( F is sharp in G major )
  1 66 480 240 =ev
  2 66 720 240 =ev    ( f, is F in the octave of the capitals )
  3 78 960 240 =ev    ( F' is an octave up )
  4 65 1200 240 =ev   ( = cancels the sharp )
  5 66 1440 240 =ev ; ( a bar line restores the key )

: tune-bar r~ L:1/4
_B B | B ~ ;
: test-accidental-lasts-the-bar
  tune-bar abc
  0 70 0 480 =ev   1 70 480 480 =ev   2 71 960 480 =ev ;

: tune-lengths r~ L:1/4
C/2 D3/2 E// z F2 G/ ~ ;
: test-lengths-and-rests
  tune-lengths abc
  0 60 0 240 =ev   1 62 240 720 =ev   2 64 960 120 =ev
  3 65 1560 960 =ev   4 67 2520 240 =ev ;

: tune-chords r~ L:1/4
[CEG]2 [C2E2G2] [CE]/2 ~ ;
: test-chords
  tune-chords abc
  tune-notes 8 =assert
  0 60 0 960 =ev   1 64 0 960 =ev   2 67 0 960 =ev
  3 60 960 960 =ev
  6 60 1920 240 =ev   7 64 1920 240 =ev ;

: tune-ties r~ L:1/4
C-C D-E C>D E<F ~ ;
: test-ties-and-broken-rhythm
  tune-ties abc
  0 60 0 960 =ev   1 62 960 480 =ev   2 64 1440 480 =ev
  3 60 1920 720 =ev   4 62 2640 240 =ev   5 64 2880 240 =ev   6 65 3120 720 =ev ;

: tune-tuplet r~ L:1/8
(3CDE F ~ ;
: test-tuplet
  tune-tuplet abc
  0 60 0 160 =ev   1 62 160 160 =ev   2 64 320 160 =ev   3 65 480 240 =ev ;

: tune-fields r~ M:3/4
Q:1/4=90
K:Dm
%%MIDI program 24
w: some lyrics
A B c d | ^A B ~ ;
: test-fields
  tune-fields abc
  tune-bpm 90 =assert   tune-program 24 =assert
  0 69 0 240 =ev
  1 70 240 240 =ev    ( B flat: D minor has one flat )
  4 70 960 240 =ev    ( ^A is A sharp )
  5 70 1200 240 =ev ; ( and B stays flat )

: tune-meter r~ M:6/8
Q:3/8=60
K:F
B ~ ;
: test-meter-and-tempo
  tune-meter abc
  tune-bpm 90 =assert   ( a dotted quarter at 60 is a quarter at 90 )
  0 70 0 240 =ev ;

: tune-bare-tempo r~ Q:150
L:1/4
C ~ ;
: test-tempo-without-a-note-value
  tune-bare-tempo abc  tune-bpm 150 =assert  0 60 0 480 =ev ;

: tune-short r~ M:2/4
K:C
C ~ ;
: test-short-meter-means-sixteenths
  tune-short abc  0 60 0 120 =ev ;

: tune-junk r~ X:1
K:C
% a comment
"C" C {gg} !trill! D ~ ;
: test-decorations-are-skipped
  tune-junk abc
  tune-notes 2 =assert   0 60 0 240 =ev   1 62 240 240 =ev ;

: test-midi-file
  tune-plain abc
  tune-midi { a n }
  a 4 s" MThd" str= assert
  a 8 + c@ 0 =assert   a 9 + c@ 0 =assert   ( format 0 )
  a 12 + c@ 1 =assert   a 13 + c@ $e0 =assert   ( 480 ticks per quarter )
  a 14 + 4 s" MTrk" str= assert
  a 18 + c@ 24 lshift  a 19 + c@ 16 lshift +  a 20 + c@ 8 lshift +  a 21 + c@ +
  n 22 - =assert ;   ( the track length is what follows )

: test-midi-notes
  tune-plain abc
  tune-midi { a n }
  a 22 + c@ 0 =assert   a 23 + c@ $ff =assert   a 24 + c@ $51 =assert   ( 120 bpm: 500000 us )
  a 26 + c@ 7 =assert   a 27 + c@ $a1 =assert   a 28 + c@ $20 =assert
  a 29 + c@ 0 =assert   a 30 + c@ $c0 =assert   ( program change )
  a 32 + c@ 0 =assert   a 33 + c@ $90 =assert   a 34 + c@ 60 =assert
  a 36 + c@ $83 =assert   a 37 + c@ $60 =assert   a 38 + c@ $80 =assert   ( 480 ticks later )
  a n + 3 - c@ $ff =assert   ( the end of the track )
  a n + 2 - c@ $2f =assert ;

: test-more-than-a-thousand-notes
  1024 0 ?do s" C" abc loop
  s" CDEF" abc  tune-notes 4 =assert ;

: test-file-lookup
  s" x" s" ueforth_abc_test_here.sf2" dump-file
  s" no/such/dir/ueforth_abc_test_here.sf2" find-file assert 2drop
  s" no/such/dir/ueforth_abc_missing.sf2" find-file 0= assert 2drop
  s" no/such/dir/ueforth_abc_missing.sf2" soundfont? 0= assert
  s" ueforth_abc_test_here.sf2" delete-file drop ;

: test-save-midi
  tune-plain abc
  s" ueforth_abc_test_here.mid" tune-save
  s" ueforth_abc_test_here.mid" file-exists? assert
  s" ueforth_abc_test_here.mid" delete-file drop ;

: test-plays
  64 48 screen
  tune-plain abc  tune-play
  tune-playing? assert
  tune-stop
  tune-playing? 0= assert
  tune-plain abc-loop  tune-playing? assert
  tune-stop
  close-screen ;

forth definitions
sdl2
run-tests
bye
