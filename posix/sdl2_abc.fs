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

( Lazy load tunes in ABC notation, played by the SDL2_mixer MIDI synthesizer )
\
\ Usage (after sdl2-mixer, which this loads if needed):
\   sdl2-abc
\   s" gm.sf2" soundfont?  drop      \ optional: a soundfont beside the program
\   r~ X:1
\   K:C
\   CDEF GABc ~ abc-play             \ or abc-loop, tune-stop
\ The tune is turned into a standard MIDI file in /tmp and played by
\ SDL2_mixer through FluidSynth, so it needs a soundfont: the one you gave
\ with soundfont, else the system's (fluid-soundfont-gm on Debian and Ubuntu).
\ NOTE: the block below is a string, so it must not contain a vertical bar.

also internals
\ The names sdl2 and sdl2-mixer are looked up when this runs, by evaluate: written
\ directly they would be the loaders that existed when this was compiled.
: sdl2-abc
  s" sdl2" evaluate
  s" defined? load-music" evaluate 0= if s" sdl2-mixer" evaluate then
  s" sdl2" evaluate
  abc-parser evaluate
  r|
sdl2 definitions also abc-int also posix

( ---- The tune as a standard MIDI file: one track, one channel ---- )
create smf 32768 allot   0 value smf-n   0 value smf-t   0 value smf-len
: sb, ( byte -- ) smf smf-n + c!   1 +to smf-n ;
: sbe32, ( n -- )
  dup 24 rshift $ff and sb,   dup 16 rshift $ff and sb,
  dup 8 rshift $ff and sb,   $ff and sb, ;
: (vlq) ( n flag -- )   ( a variable length number: high groups first, flagged )
  over 127 > if over 7 rshift 128 recurse then
  swap $7f and or sb, ;
: vlq, ( n -- ) 0 (vlq) ;

: emit-group ( first -- next )   ( the notes that start together: all on, then all off )
  { first }
  first nt@ { s }   first nd@ { d }
  first { last }
  begin last n-notes < if last nt@ s = else 0 then while 1 +to last repeat
  s smf-t - { delta }
  last first ?do  delta vlq,  $90 sb,  i nk@ sb,  90 sb,  0 to delta  loop
  d to delta
  last first ?do  delta vlq,  $80 sb,  i nk@ sb,  0 sb,  0 to delta  loop
  s d + to smf-t
  last ;

: build-smf ( -- )
  0 to smf-n   0 to smf-t
  77 sb, 84 sb, 104 sb, 100 sb,   6 sbe32,   0 sb, 0 sb,   0 sb, 1 sb,   1 sb, $e0 sb,
  77 sb, 84 sb, 114 sb, 107 sb,   smf-n to smf-len   0 sbe32,
  0 vlq,  $ff sb, $51 sb, 3 sb,
  60000000 tune-bpm /  dup 16 rshift $ff and sb,  dup 8 rshift $ff and sb,  $ff and sb,
  0 vlq,  $c0 sb,  tune-program $7f and sb,
  0 { gi }
  begin gi n-notes < while gi emit-group to gi repeat
  0 vlq,  $ff sb, $2f sb, 0 sb,
  smf-n smf-len - 4 -  smf-len smf + { at }  { n }
  n 24 rshift $ff and at c!   n 16 rshift $ff and at 1+ c!
  n 8 rshift $ff and at 2 + c!   n $ff and at 3 + c! ;

: tune-midi ( -- a n )   ( the tune as the bytes of a MIDI file )
  build-smf smf smf-n ;
: tune-save ( a n -- )   ( writes the tune to a .mid file )
  { na nn }  tune-midi na nn dump-file ;

( ---- Playing it ---- )
0 value tune-music
: tune-stop ( -- )
  stop-music
  tune-music if tune-music free-music 0 to tune-music then ;
\ The MIDI file goes to a temporary file of its own (made by mkstemps, so it
\ cannot clash with another user or another run), and is deleted once loaded.
z" mkstemps" 2 sysfunc c-mkstemps ( z n -- n )
z" close" 1 sysfunc c-close ( n -- n )
create tmpl 64 allot
0 value tune-code
: tune-path ( -- a n )   ( a new empty file in /tmp )
  z" /tmp/ueforth-tune-XXXXXX.mid" tmpl 29 cmove
  tmpl 4 c-mkstemps sign-extend dup 0 < if
    drop ." cannot make a temporary file in /tmp" cr -1 throw
  then
  c-close drop
  tmpl 28 ;
: tune-load ( -- )
  tune-stop
  tune-path { a n }
  tune-midi a n dump-file
  a n ['] load-music catch
  dup to tune-code if  2drop  a n delete-file drop  tune-code throw  then
  to tune-music
  a n delete-file drop ;
: tune-play ( -- ) tune-load tune-music music-once ;
: tune-loop ( -- ) tune-load tune-music music ;
: tune-playing? ( -- f ) music? ;
: abc-play ( a n -- ) abc tune-play ;
: abc-loop ( a n -- ) abc tune-loop ;

previous previous
  | evaluate ;
previous
