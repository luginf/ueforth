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

\ A minimal piano-roll sequencer with SDL2 and a MIDI soundfont: 4 tracks
\ (piano, bass, violin, flute), 16 steps per pattern. More patterns can be
\ added and chained into a song longer than 16 steps. The song strip only
\ shows as many steps as fit on screen; this small example does not scroll.
\ A note can be longer than one step (sixteenth, eighth, quarter, half or
\ whole), set with the note-length control; playing renders the whole song
\ as a standard MIDI file and plays it through SDL2_mixer and FluidSynth, so
\ it needs a soundfont (see sdl2_mixer.fs / SDL2.md). Clicking a note gives
\ an instant preview, through a small persistent FluidSynth of its own so it
\ uses the same instrument sound, falling back to the plain SDL2 beeper if
\ no soundfont can be found.
\ "SAV" exports the song as both a .mid and a .abc file.
\ Mouse:
\   grid            click sets or clears a note (drag to paint or erase)
\   piano keys      click to preview a pitch, without changing the pattern
\   track tabs      1 2 3 4, choose which track's roll is shown and edited
\   instrument -/+  the General MIDI program (0..127) of the current track,
\                   1 step with the left button, 10 with the right
\   note length -/+ how many steps a newly placed note lasts
\   pattern tabs    choose which pattern is shown and edited; + adds a new one
\   song strip      click a step to change which pattern plays there; + repeats
\                   the current pattern at the end of the song
\   play button     play / stop the whole song, from the start
\   - and +         tempo, 1 bpm with the left button, 5 with the right
\   SAV             writes sdl2_pianoroll_song.mid and .abc
\   X               quit
\ Keyboard: space play/stop, up/down tempo, 1-4 pick the track, esc quit.
\ Editing (a note, an instrument, a pattern, the song or the tempo) stops playback, since
\ the song is only rendered to sound when play starts: press play again to
\ hear the change.

sdl2
sdl2-mixer
s" sdl2_pianoroll.sf2" soundfont? drop   \ optional, a soundfont beside this file

( ---- sizes and the pattern bank ---- )
16 constant steps            4 constant tracks
8 constant max-patterns      32 constant max-song
18 constant roll-rows

create patterns  max-patterns tracks * steps * allot
patterns max-patterns tracks * steps * erase
create song max-song allot   song max-song erase
1 value song-len   1 value pattern-count
0 value cur-track   0 value cur-pattern
0 value playing     0 value song-pos   0 value pat-step   0 value next-at
120 value bpm

create track-program 0 , 32 , 40 , 73 ,      ( piano, bass, violin, flute )
create track-base 60 , 36 , 60 , 72 ,        ( middle note shown in the roll )
create track-colors cyan , orange , green , pink ,

( ---- notes: pat-addr picks a byte in patterns; 0 means no note.  Bit 7 (0x80)
  marks a "tie": the step continues the note that started earlier, rather than
  starting a new one; bits 0..6 hold the pitch, kept there too on a tie step
  so the byte alone always says what is sounding. ---- )
: pat-addr ( pat trk stp -- addr )
  { p t s } p tracks * t + steps * s + patterns + ;
: note@ ( pat trk stp -- n ) pat-addr c@ ;
: note! ( n pat trk stp -- )
  { n pat trk stp } n  pat trk stp pat-addr  c! ;
: note-pitch ( byte -- pitch ) $7f and ;
: note-tie? ( byte -- f ) $80 and 0<> ;
: note-span ( pat trk stp -- n )   ( 1 + how many further steps tie to it, bounded by the pattern )
  { pat trk stp } 1 { n }
  begin
    stp n + steps <   pat trk stp n + note@ note-tie?  and
  while 1 +to n repeat
  n ;

( ---- the tune as a standard MIDI file: one track, one channel per voice ---- )
create smf 65536 allot   0 value smf-n   0 value smf-t   0 value smf-len
: sb, ( byte -- ) smf smf-n + c!   1 +to smf-n ;
: sbe32, ( n -- )
  dup 24 rshift $ff and sb,   dup 16 rshift $ff and sb,
  dup 8 rshift $ff and sb,   $ff and sb, ;
: (vlq) ( n flag -- )
  over 127 > if over 7 rshift 128 recurse then
  swap $7f and or sb, ;
: vlq, ( n -- ) 0 (vlq) ;

120 constant step-ticks   100 constant note-ticks   ( 480 ticks per quarter )

( a note-off due per channel, fired once the step that reaches it comes up )
create pend-off-tick 4 cells allot
create pend-off-pitch 4 cells allot

0 value ev-nn   0 value ev-delta   0 value ev-due
: fire-offs ( pos -- )   ( pos: the tick this step starts at; fires any note-off due by the next one )
  { pos }
  tracks 0 do
    pend-off-tick i cells + @ to ev-due
    ev-due 0<>  ev-due pos step-ticks + <=  and if
      ev-due smf-t - to ev-delta
      ev-delta vlq,  $80 i + sb,  pend-off-pitch i cells + @ sb,  0 sb,   0 to ev-delta
      ev-due to smf-t
      0 pend-off-tick i cells + !
    then
  loop ;
: flush-offs ( -- )   ( fires whatever note-offs are still pending, at the very end )
  tracks 0 do
    pend-off-tick i cells + @ to ev-due
    ev-due 0<> if
      ev-due smf-t - to ev-delta
      ev-delta vlq,  $80 i + sb,  pend-off-pitch i cells + @ sb,  0 sb,   0 to ev-delta
      ev-due to smf-t
      0 pend-off-tick i cells + !
    then
  loop ;
0 value fo-b   0 value fo-span
: fire-ons ( pat step pos -- )
  { pat step pos }
  pos smf-t - to ev-delta
  tracks 0 do
    pat i step note@ to fo-b
    fo-b 0<>  fo-b note-tie? 0=  and if
      fo-b note-pitch to ev-nn
      ev-delta vlq,  $90 i + sb,  ev-nn sb,  90 sb,   0 to ev-delta
      pat i step note-span to fo-span
      pos fo-span 1- step-ticks * + note-ticks +  pend-off-tick i cells + !
      ev-nn pend-off-pitch i cells + !
    then
  loop
  pos to smf-t ;

0 value bs-pat   0 value bs-pos
: build-smf ( -- )
  0 to smf-n   0 to smf-t   0 to bs-pos
  pend-off-tick 4 cells erase   pend-off-pitch 4 cells erase
  77 sb, 84 sb, 104 sb, 100 sb,   6 sbe32,   0 sb, 0 sb,   0 sb, 1 sb,   1 sb, $e0 sb,
  77 sb, 84 sb, 114 sb, 107 sb,   smf-n to smf-len   0 sbe32,
  0 vlq,  $ff sb, $51 sb, 3 sb,
  60000000 bpm /  dup 16 rshift $ff and sb,  dup 8 rshift $ff and sb,  $ff and sb,
  tracks 0 do  0 vlq,  $c0 i + sb,  i cells track-program + @ $7f and sb,  loop
  song-len 0 do
    i song + c@ to bs-pat
    steps 0 do
      bs-pos fire-offs
      bs-pat i bs-pos fire-ons
      step-ticks +to bs-pos
    loop
  loop
  flush-offs
  0 vlq,  $ff sb, $2f sb, 0 sb,
  smf-n smf-len - 4 -  smf-len smf + { at }  { n }
  n 24 rshift $ff and at c!   n 16 rshift $ff and at 1+ c!
  n 8 rshift $ff and at 2 + c!   n $ff and at 3 + c! ;

( ---- the same song, in ABC notation: one voice per track, one bar per pattern.
  A held note is written once with a length multiplier (C4 = four 1/16s), the
  steps it ties over are skipped, so the bar still totals 16 sixteenths. ---- )
create abctxt 65536 allot   0 value abc-n
: ac, ( char -- ) abctxt abc-n + c!  1 +to abc-n ;
: as, ( a n -- ) over + swap ?do i c@ ac, loop ;
: al, ( -- ) 10 ac, ;
: an, ( n -- )   ( decimal digits of a positive number )
  dup 9 > if dup 10 / recurse then   10 mod [char] 0 + ac, ;

create abc-letters char C , char D , char E , char F , char G , char A , char B ,
create abc-sharp   0 , 1 , 0 , 1 , 0 , 0 , 1 , 0 , 1 , 0 , 1 , 0 ,
create abc-letter  0 , 0 , 1 , 1 , 2 , 3 , 3 , 4 , 4 , 5 , 5 , 6 ,

0 value abc-oct   0 value abc-semi
: abc-split ( note -- )   ( splits into abc-semi 0..11 and abc-oct, 0 = the octave of ABC's "C" )
  60 -  to abc-semi   0 to abc-oct
  begin abc-semi 0< while 12 +to abc-semi  -1 +to abc-oct  repeat
  begin abc-semi 11 > while -12 +to abc-semi  1 +to abc-oct  repeat ;
: emit-note ( note len -- )
  { len }
  abc-split
  abc-semi cells abc-sharp + @ if [char] ^ ac, then
  abc-semi cells abc-letter + @ cells abc-letters + @ { ch }
  abc-oct 0 > if
    ch 32 + ac,   abc-oct 1 - 0 ?do [char] ' ac, loop
  else
    ch ac,   abc-oct negate 0 ?do [char] , ac, loop
  then
  len 1 > if len an, then ;

0 value at-pat   0 value at-nn   0 value at-span
: abc-track ( trk -- )
  { trk }
  s" V:" as,  trk 1+ an,  al,
  s" %%MIDI program " as,  trk cells track-program + @ an,  al,
  song-len 0 do
    i song + c@ to at-pat
    steps 0 do
      at-pat trk i note@ to at-nn
      at-nn 0<>  at-nn note-tie? 0=  and if
        at-pat trk i note-span to at-span
        at-nn note-pitch at-span emit-note
      else
        at-nn 0= if [char] z ac, then
      then
    loop
    [char] | ac,  al,
  loop ;
: build-abc ( -- )
  0 to abc-n
  s" X:1" as, al,
  s" T:ueforth piano roll" as, al,
  s" M:4/4" as, al,
  s" L:1/16" as, al,
  s" Q:1/4=" as,  bpm an,  al,
  tracks 0 do i abc-track loop ;

: save-song ( -- )
  build-smf   smf smf-n s" sdl2_pianoroll_song.mid" dump-file
  build-abc   abctxt abc-n s" sdl2_pianoroll_song.abc" dump-file
  cr ." saved sdl2_pianoroll_song.mid and sdl2_pianoroll_song.abc" cr ;

( ---- playing the song: MIDI through the soundfont ---- )
also posix   ( sysfunc, and the raw z/mkstemps/close/fluidsynth bindings below, live there )
z" mkstemps" 2 sysfunc c-mkstemps ( z n -- n )
z" close" 1 sysfunc c-close ( n -- n )
create tmpl 64 allot
0 value song-music   0 value song-code
: song-path ( -- a n )   ( a new empty file in /tmp, unique to this run )
  z" /tmp/ueforth-pianoroll-XXXXXX.mid" { z }
  z z>s nip 1+ { n }
  z tmpl n cmove
  tmpl 4 c-mkstemps sign-extend dup 0 < if
    drop ." cannot make a temporary file in /tmp" cr -1 throw
  then
  c-close drop
  tmpl n 1- ;
: song-stop ( -- )
  stop-music
  song-music if song-music free-music 0 to song-music then ;
: song-load ( -- )
  song-stop
  build-smf
  song-path { a n }
  smf smf-n a n dump-file
  a n ['] load-music catch
  dup to song-code if  2drop  a n delete-file drop  song-code throw  then
  to song-music
  a n delete-file drop ;
: song-play ( -- ) song-load song-music music ;
: stop-if-playing ( -- ) playing if song-stop 0 to playing then ;

: toggle-play
  playing if
    song-stop   0 to playing
  else
    song-play   -1 to playing
    0 to song-pos   0 to pat-step   ticks to next-at
  then ;
: step-ms ( -- ms ) 15000 bpm / ;   ( a 16th note; matches the MIDI tick timing )
: advance-playhead
  playing 0= if exit then
  ticks next-at - 0< if exit then
  ticks next-at - 200 > if ticks to next-at then
  pat-step 1+ steps mod to pat-step
  pat-step 0= if song-pos 1+ song-len mod to song-pos then
  step-ms +to next-at ;

( ---- tempo, clearing, patterns and the song arrangement ---- )
: tempo ( n -- )
  dup 0<> if stop-if-playing then
  bpm + 40 max 240 min to bpm ;
: tempo-up 5 tempo ;
: tempo-down -5 tempo ;

: instrument ( -- program ) cur-track cells track-program + @ ;
: instrument! ( program -- ) cur-track cells track-program + ! ;
: instrument-step ( d -- )
  stop-if-playing
  instrument + 0 max 127 min instrument! ;
: run-instrument ( id b -- )   ( id -2 lowers, -3 raises; 1 step, or 10 with the right button )
  2 = if 10 else 1 then
  swap -2 = if negate then instrument-step ;

( ---- the length (in steps) a newly placed note gets: a 16th, 8th, quarter, half or whole ---- )
create note-lens 1 , 2 , 4 , 8 , 16 ,
5 constant note-len-count
2 value note-len-idx   ( default: 4 steps, a quarter note )
: note-len ( -- n ) note-len-idx cells note-lens + @ ;
: note-len-step ( d -- )
  stop-if-playing
  note-len-idx + 0 max note-len-count 1- min to note-len-idx ;
: run-note-len ( id b -- )   ( id -4 lowers, -5 raises, either button, 1 step )
  drop  -4 = if -1 else 1 then note-len-step ;
: note-len-label ( -- a n )
  note-len-idx 0 = if s" SIXTEENTH" exit then
  note-len-idx 1 = if s" EIGHTH" exit then
  note-len-idx 2 = if s" QUARTER" exit then
  note-len-idx 3 = if s" HALF" exit then
  s" WHOLE" ;

( ---- the 128 General MIDI instrument names, for the display only ---- )
13 constant name-w
create gm-names 128 name-w * allot
gm-names 128 name-w * erase
: def-instr ( a n idx -- )
  { a n idx } a n name-w min { n2 }
  a  idx name-w * gm-names +  n2 cmove ;
s" Grand Piano" 0 def-instr   s" Bright Piano" 1 def-instr   s" El Grand Pno" 2 def-instr
s" Honky-Tonk" 3 def-instr   s" El Piano 1" 4 def-instr   s" El Piano 2" 5 def-instr
s" Harpsichord" 6 def-instr   s" Clavinet" 7 def-instr   s" Celesta" 8 def-instr
s" Glockenspiel" 9 def-instr   s" Music Box" 10 def-instr   s" Vibraphone" 11 def-instr
s" Marimba" 12 def-instr   s" Xylophone" 13 def-instr   s" Tubular Bell" 14 def-instr
s" Dulcimer" 15 def-instr   s" Drawbar Org" 16 def-instr   s" Perc Organ" 17 def-instr
s" Rock Organ" 18 def-instr   s" Church Organ" 19 def-instr   s" Reed Organ" 20 def-instr
s" Accordion" 21 def-instr   s" Harmonica" 22 def-instr   s" Tango Accrd" 23 def-instr
s" Nylon Gtr" 24 def-instr   s" Steel Gtr" 25 def-instr   s" Jazz Gtr" 26 def-instr
s" Clean Gtr" 27 def-instr   s" Muted Gtr" 28 def-instr   s" Overdrive Gt" 29 def-instr
s" Distortn Gtr" 30 def-instr   s" Gtr Harmonic" 31 def-instr   s" Acoustic Bs" 32 def-instr
s" Finger Bass" 33 def-instr   s" Pick Bass" 34 def-instr   s" Fretless Bs" 35 def-instr
s" Slap Bass 1" 36 def-instr   s" Slap Bass 2" 37 def-instr   s" Synth Bass 1" 38 def-instr
s" Synth Bass 2" 39 def-instr   s" Violin" 40 def-instr   s" Viola" 41 def-instr
s" Cello" 42 def-instr   s" Contrabass" 43 def-instr   s" Tremolo Str" 44 def-instr
s" Pizzicato" 45 def-instr   s" Harp" 46 def-instr   s" Timpani" 47 def-instr
s" Strings 1" 48 def-instr   s" Strings 2" 49 def-instr   s" Synth Str 1" 50 def-instr
s" Synth Str 2" 51 def-instr   s" Choir Aahs" 52 def-instr   s" Voice Oohs" 53 def-instr
s" Synth Voice" 54 def-instr   s" Orchestra Hit" 55 def-instr   s" Trumpet" 56 def-instr
s" Trombone" 57 def-instr   s" Tuba" 58 def-instr   s" Muted Trumpt" 59 def-instr
s" French Horn" 60 def-instr   s" Brass Sectn" 61 def-instr   s" Synth Brass1" 62 def-instr
s" Synth Brass2" 63 def-instr   s" Soprano Sax" 64 def-instr   s" Alto Sax" 65 def-instr
s" Tenor Sax" 66 def-instr   s" Baritone Sax" 67 def-instr   s" Oboe" 68 def-instr
s" English Horn" 69 def-instr   s" Bassoon" 70 def-instr   s" Clarinet" 71 def-instr
s" Piccolo" 72 def-instr   s" Flute" 73 def-instr   s" Recorder" 74 def-instr
s" Pan Flute" 75 def-instr   s" Blown Bottle" 76 def-instr   s" Shakuhachi" 77 def-instr
s" Whistle" 78 def-instr   s" Ocarina" 79 def-instr   s" Square Lead" 80 def-instr
s" Saw Lead" 81 def-instr   s" Calliope Ld" 82 def-instr   s" Chiff Lead" 83 def-instr
s" Charang Ld" 84 def-instr   s" Voice Lead" 85 def-instr   s" Fifths Lead" 86 def-instr
s" Bass+Lead" 87 def-instr   s" New Age Pad" 88 def-instr   s" Warm Pad" 89 def-instr
s" Polysynth Pd" 90 def-instr   s" Choir Pad" 91 def-instr   s" Bowed Pad" 92 def-instr
s" Metallic Pad" 93 def-instr   s" Halo Pad" 94 def-instr   s" Sweep Pad" 95 def-instr
s" Rain FX" 96 def-instr   s" Soundtrack" 97 def-instr   s" Crystal FX" 98 def-instr
s" Atmosphere" 99 def-instr   s" Brightness" 100 def-instr   s" Goblins FX" 101 def-instr
s" Echoes FX" 102 def-instr   s" Sci-Fi FX" 103 def-instr   s" Sitar" 104 def-instr
s" Banjo" 105 def-instr   s" Shamisen" 106 def-instr   s" Koto" 107 def-instr
s" Kalimba" 108 def-instr   s" Bagpipe" 109 def-instr   s" Fiddle" 110 def-instr
s" Shanai" 111 def-instr   s" Tinkle Bell" 112 def-instr   s" Agogo" 113 def-instr
s" Steel Drums" 114 def-instr   s" Woodblock" 115 def-instr   s" Taiko Drum" 116 def-instr
s" Melodic Tom" 117 def-instr   s" Synth Drum" 118 def-instr   s" Reverse Cym" 119 def-instr
s" Fret Noise" 120 def-instr   s" Breath Noise" 121 def-instr   s" Seashore" 122 def-instr
s" Bird Tweet" 123 def-instr   s" Telephone" 124 def-instr   s" Helicopter" 125 def-instr
s" Applause" 126 def-instr   s" Gunshot" 127 def-instr
: instrument-name ( -- a n ) instrument name-w * gm-names +  name-w ;

: clear-pattern
  stop-if-playing
  cur-pattern tracks * steps * patterns +  tracks steps *  erase ;

: pick-pattern ( n -- )   ( n = pattern-count means "new pattern" )
  dup pattern-count = if
    drop
    pattern-count max-patterns < if
      stop-if-playing
      pattern-count to cur-pattern
      1 +to pattern-count
      song-len max-song < if
        cur-pattern song song-len + c!
        1 +to song-len
      then
    then
  else
    to cur-pattern
  then ;

: pick-song-slot ( n -- )   ( n = song-len means "repeat the current pattern" )
  dup song-len = if
    drop
    song-len max-song < if
      stop-if-playing
      cur-pattern song song-len + c!
      1 +to song-len
    then
  else
    stop-if-playing
    dup song + dup c@ 1+ pattern-count mod swap c!  drop
  then ;

( ---- hit-testing: grid, piano keys, tabs, buttons ---- )
30 constant grid-x   86 constant grid-y
16 constant col-w    9 constant row-h
14 constant col-box  7 constant row-box

: row-at ( -- row )
  mouse-y grid-y - dup 0< if drop -1 exit then
  row-h / dup roll-rows >= if drop -1 then ;
: col-at ( -- col )
  mouse-x grid-x - dup 0< if drop -1 exit then
  col-w / dup steps >= if drop -1 then ;
: key-at ( -- row )
  mouse-x 8 < if -1 exit then
  mouse-x grid-x >= if -1 exit then
  row-at ;
: track-tab-at ( -- n )
  mouse-y 30 - dup 0< if drop -1 exit then
  14 < 0= if -1 exit then
  mouse-x grid-x - dup 0< if drop -1 exit then
  20 / dup tracks >= if drop -1 then ;
: pattern-tab-at ( -- n )
  mouse-y 48 - dup 0< if drop -1 exit then
  14 < 0= if -1 exit then
  mouse-x grid-x - dup 0< if drop -1 exit then
  20 / dup pattern-count > if drop -1 then ;
: song-tab-at ( -- n )
  mouse-y 66 - dup 0< if drop -1 exit then
  14 < 0= if -1 exit then
  mouse-x grid-x - dup 0< if drop -1 exit then
  20 / dup song-len > if drop -1 then ;

: inside? { x y w h }
  mouse-x x -   dup 0 >= swap w < and
  mouse-y y -   dup 0 >= swap h < and
  and ;

( The instrument, a General MIDI program 0..127, of the current track, next to its tabs )
116 constant instr-x   18 constant instr-w   166 constant instr-x2
: instr-minus-at ( -- f ) instr-x 30 instr-w 14 inside? ;
: instr-plus-at ( -- f ) instr-x2 30 instr-w 14 inside? ;
: instrument-btn-at ( -- id )   ( -2 the minus button, -3 the plus one, else 0 )
  instr-minus-at if -2 exit then
  instr-plus-at if -3 exit then
  0 ;

( The note length, next to the pattern tabs )
220 constant nlen-x   18 constant nlen-w   252 constant nlen-x2
: nlen-minus-at ( -- f ) nlen-x 48 nlen-w 14 inside? ;
: nlen-plus-at ( -- f ) nlen-x2 48 nlen-w 14 inside? ;
: note-len-btn-at ( -- id )   ( -4 the minus button, -5 the plus one, else 0 )
  nlen-minus-at if -4 exit then
  nlen-plus-at if -5 exit then
  0 ;

( The buttons of the top bar: play/stop, tempo -, tempo +, clear, save, quit )
create btn-x 8 , 44 , 98 , 140 , 182 , 286 ,
create btn-w 24 , 20 , 20 , 32 , 32 , 26 ,
5 constant btn-y   20 constant btn-h   6 constant buttons
create btn-actions ' toggle-play , ' tempo-down , ' tempo-up , ' clear-pattern , ' save-song , ' quit! ,

: button-at ( -- id )
  -1 { found }
  buttons 0 ?do
    i cells btn-x + @  btn-y  i cells btn-w + @  btn-h  inside? if i to found then
  loop
  found ;

( ---- editing a note ---- )
: row-note ( row -- note )
  { row } track-base cur-track cells + @  roll-rows 1- row - + ;

( ---- previewing a pitch, through the soundfont's real instrument when one loaded,
  else the plain SDL2 beeper ---- )
create semi-ratio 10000 , 10595 , 11225 , 11892 , 12599 , 13348 , 14142 , 14983 , 15874 , 16818 , 17818 , 18877 ,
0 value nf-oct   0 value nf-d
: note-freq ( note -- hz )
  69 -  to nf-d   0 to nf-oct
  begin nf-d 0< while  12 +to nf-d   -1 +to nf-oct  repeat
  begin nf-d 11 > while  -12 +to nf-d   1 +to nf-oct  repeat
  440 nf-d cells semi-ratio + @ * 10000 /
  begin nf-oct 0 > while  2 *   -1 +to nf-oct  repeat
  begin nf-oct 0< while  2 /   1 +to nf-oct  repeat ;
: beep-preview ( note -- )
  note-freq { f }
  4000 to beep-volume   1 to wave   0 to pitch-drop
  f 150 hit ;

( a small FluidSynth of its own, so a click sounds like the real instrument;
  lazy: the first preview opens it, silently falls back to beep-preview if no
  soundfont can be reached or the library is missing )
0 value fsynth   0 value fsynth-ready   0 value fsynth-failed
: fsynth-init ( -- )
  fsynth-ready if exit then
  fsynth-failed if exit then
  ['] fsynth-open catch if -1 to fsynth-failed then ;
: preview-note ( row -- )
  row-note { note }
  fsynth-init
  fsynth-ready if
    fsynth cur-track instrument fs-program-change drop
    fsynth cur-track note 100 fs-noteon drop
  else
    note beep-preview
  then ;

: set-note ( pitch pat trk stp len -- )
  { pitch pat trk stp len }
  pat trk stp clear-from
  pitch pat trk stp note!
  len 1 ?do
    stp i + steps < if pitch $80 or pat trk stp i + note! then
  loop ;
: note-matches? ( row col -- f )
  { row col }
  cur-pattern cur-track col note@ note-pitch  row row-note = ;
: set-cell ( row col -- )
  { row col }
  stop-if-playing
  cur-pattern cur-track col note@ note-pitch  row row-note <>  if row preview-note then
  row row-note cur-pattern cur-track col note-len set-note ;
: clear-cell ( col -- )
  { col } stop-if-playing cur-pattern cur-track col clear-note ;

0 value paint-write
: begin-paint ( row col -- )
  { row col }
  row col note-matches? if
    0 to paint-write   col clear-cell
  else
    -1 to paint-write   row col set-cell
  then ;
: continue-paint ( row col -- )
  paint-write if set-cell else drop clear-cell then ;

( ---- mouse and keyboard ---- )
0 value was-btn   0 value painting
-1 value btn-hover   -1 value held-btn   0 value repeat-at
-1 value hover-row   -1 value hover-col   -1 value key-hover
-1 value track-hover   -1 value pat-hover   -1 value song-hover
0 value instr-hover   0 value held-instr   0 value instr-repeat-at
0 value nlen-hover    0 value held-nlen    0 value nlen-repeat-at

: mouse-btn ( -- n )
  LEFT-BUTTON pressed? if 1 exit then
  RIGHT-BUTTON pressed? if 2 exit then
  0 ;
: run-tempo ( id b -- )   ( button 1 lowers, 2 raises; 1 bpm, or 5 with the right button )
  2 = if 5 else 1 then
  swap 1 = if negate then tempo ;
: press-button ( id b -- )
  { id b }
  id to held-btn   ticks 400 + to repeat-at
  id 1 = id 2 = or if id b run-tempo exit then
  b 1 = if id cells btn-actions + @ execute then ;
: repeat-button ( b -- )
  held-btn 1 <  held-btn 2 > or if drop exit then
  btn-hover held-btn <> if drop exit then
  ticks repeat-at - 0< if drop exit then
  held-btn swap run-tempo   80 +to repeat-at ;

: press-instrument ( id b -- )
  { id b }
  id to held-instr   ticks 400 + to instr-repeat-at
  id b run-instrument ;
: repeat-instrument ( b -- )
  held-instr 0= if drop exit then
  instr-hover held-instr <> if drop exit then
  ticks instr-repeat-at - 0< if drop exit then
  held-instr swap run-instrument   80 +to instr-repeat-at ;

: press-note-len ( id b -- )
  { id b }
  id to held-nlen   ticks 400 + to nlen-repeat-at
  id b run-note-len ;
: repeat-note-len ( b -- )
  held-nlen 0= if drop exit then
  nlen-hover held-nlen <> if drop exit then
  ticks nlen-repeat-at - 0< if drop exit then
  held-nlen swap run-note-len   150 +to nlen-repeat-at ;

: handle-mouse
  row-at to hover-row   col-at to hover-col   key-at to key-hover
  track-tab-at to track-hover   pattern-tab-at to pat-hover   song-tab-at to song-hover
  button-at to btn-hover   instrument-btn-at to instr-hover   note-len-btn-at to nlen-hover
  mouse-btn { b }
  b 0= if 0 to painting  -1 to held-btn  0 to held-instr  0 to held-nlen then
  b was-btn 0= and { fresh }
  fresh if
    btn-hover 0 >= if btn-hover b press-button then
    instr-hover 0<> if instr-hover b press-instrument then
    nlen-hover 0<> if nlen-hover b press-note-len then
  then
  fresh b 1 = and if
    track-hover 0 >= if track-hover to cur-track then
    pat-hover 0 >= if pat-hover pick-pattern then
    song-hover 0 >= if song-hover pick-song-slot then
    key-hover 0 >= if key-hover preview-note then
    hover-row 0 >= hover-col 0 >= and if
      hover-row hover-col begin-paint   -1 to painting
    then
  then
  painting hover-row 0 >= and hover-col 0 >= and if
    hover-row hover-col continue-paint
  then
  b if b repeat-button  b repeat-instrument  b repeat-note-len then
  b to was-btn ;

create was 256 allot   was 256 erase
: edge? ( k -- f ) dup pressed? swap 255 and was + c@ 0= and ;
: key-remember ( k -- ) dup pressed? swap 255 and was + c! ;
: remember-keys
  key-space key-remember  key-up key-remember  key-down key-remember
  tracks 0 do [char] 1 i + key-remember loop ;
: handle-keys
  key-space edge? if toggle-play then
  key-up edge? if tempo-up then
  key-down edge? if tempo-down then
  wheel 5 * tempo
  tracks 0 do [char] 1 i + edge? if i to cur-track then loop
  remember-keys ;

( ---- drawing ---- )
create digit-bits
  $7b6f , $2c97 , $73e7 , $73cf , $5bc9 , $79cf , $79ef , $7249 , $7bef , $7bcf ,
  $7927 , $4927 , $7bf5 , $5aad ,   ( 10 C  11 L  12 R  13 X )
  $2bed , $6bae , $3923 , $6b6e , $79a7 , $79a4 , $396b , $5bed , $7497 , $126a ,
  $5bad , $4927 , $5f6d , $5ffd , $2b6a , $6ba4 , $2b7b , $6bad , $388e , $7492 ,
  $5b6f , $5b6a , $5b7d , $5aad , $5a92 , $72a7 ,   ( 14..39 A..Z )
  0 ,                                                ( 40 space )

: draw-digit { d x y s }
  d cells digit-bits + @ { bits }
  15 0 do
    bits 14 i - rshift 1 and if
      x i 3 mod s * +   y i 3 / s * +   s s box
    then
  loop ;
: char-index ( ch -- idx )   ( 0..9 a digit, 14..39 a letter, case folded, else 40, blank )
  { ch }
  ch [char] 0 >= ch [char] 9 <= and if ch [char] 0 - exit then
  ch [char] a >= ch [char] z <= and if ch 32 - to ch then
  ch [char] A >= ch [char] Z <= and if ch [char] A - 14 + exit then
  40 ;
: draw-text ( a n x y s -- )
  { a n x y s }
  n 0 ?do
    a i + c@ char-index   x 4 s * i * +   y s draw-digit
  loop ;
: draw-num3 { n x y s }   ( 0..127, no leading zeros )
  n 100 / { h }   h if h x y s draw-digit then
  n 10 / 10 mod { t }   h t or if t x 4 s * +  y s draw-digit then
  n 10 mod   x 8 s * +  y s draw-digit ;
: draw-bpm { x y s } bpm x y s draw-num3 ;
: plus-at { x y }
  x 4 + y 6 +  10 2 box
  x 8 + y 2 +  2 10 box ;
: minus-at { x y } x 4 + y 6 +  10 2 box ;

: draw-instrument
  instr-hover -2 = if $5a6aa0 else $28325a then color
  instr-x 30 instr-w 14 box
  white color   instr-x 30 minus-at
  instr-hover -3 = if $5a6aa0 else $28325a then color
  instr-x2 30 instr-w 14 box
  white color   instr-x2 30 plus-at
  white color   instrument  instr-x 24 +  33  2 draw-num3
  white color   instrument-name  instr-x2 24 +  33  1 draw-text ;

: draw-note-len
  nlen-hover -4 = if $5a6aa0 else $28325a then color
  nlen-x 48 nlen-w 14 box
  white color   nlen-x 48 minus-at
  nlen-hover -5 = if $5a6aa0 else $28325a then color
  nlen-x2 48 nlen-w 14 box
  white color   nlen-x2 48 plus-at
  white color   note-len-label  nlen-x2 24 +  53  1 draw-text ;

: draw-tab { label active x y }
  active if $3a4680 else $1c2440 then color
  x y 18 14 box
  white color
  label x 4 + y 4 + 2 draw-digit ;
: draw-track-tabs
  tracks 0 do i 1+  i cur-track =  grid-x i 20 * +  30  draw-tab loop ;
: draw-pattern-tabs
  pattern-count 0 do i 1+  i cur-pattern =  grid-x i 20 * +  48  draw-tab loop
  pattern-count max-patterns < if
    $28325a color   grid-x pattern-count 20 * +  48  18 14 box
    white color   grid-x pattern-count 20 * +  48  plus-at
  then ;
: draw-song-tabs
  song-len 0 do
    i song + c@ 1+   playing song-pos i = and   grid-x i 20 * +  66  draw-tab
  loop
  song-len max-song < if
    $28325a color   grid-x song-len 20 * +  66  18 14 box
    white color   grid-x song-len 20 * +  66  plus-at
  then ;

: black-key? ( note -- f )
  12 mod { m }
  m 1 = m 3 = or m 6 = or m 8 = or m 10 = or ;
: key-color ( row -- $rrggbb )
  { row }
  row key-hover = if $5a6aa0 exit then
  row row-note black-key? if $202020 else $d8d8d8 then ;
: draw-keys
  roll-rows 0 do
    i key-color color
    8  i row-h * grid-y +  grid-x 12 -  row-box  box
  loop ;

: playing-here? ( col -- f )
  playing 0= if drop 0 exit then
  song-pos song + c@ cur-pattern <> if drop 0 exit then
  pat-step = ;
0 value cc-nn
: cell-color ( row col -- $rrggbb )
  { row col }
  cur-pattern cur-track col note@ to cc-nn
  cc-nn note-pitch row row-note = if
    col playing-here? if white else
      cc-nn note-tie? if track-colors cur-track cells + @ $7f7f7f and
      else track-colors cur-track cells + @ then
    then
  else
    row row-note black-key? if $181818 else $242424 then
  then ;
: draw-grid
  roll-rows 0 do
    steps 0 do
      j i cell-color color
      i col-w * grid-x +   j row-h * grid-y +   col-box row-box box
    loop
  loop ;

: icon-play { x w }
  x w 12 - 2 / + { cx }
  playing if
    white color   cx 1 + btn-y 5 +  10 10 box
  else
    green color
    12 0 do  cx  btn-y 4 + i +  i 6 < if i 1+ else 12 i - then 2 *  hline  loop
  then ;
: icon-minus ( x w -- ) 10 - 2 / +  btn-y 9 +  10 2 box ;
: icon-plus ( x w -- ) 2dup icon-minus   2 - 2 / +  btn-y 5 +  2 10 box ;
: icon-clear ( x w -- )
  22 - 2 / + { gx }
  10 gx btn-y 5 + 2 draw-digit
  11 gx 8 + btn-y 5 + 2 draw-digit
  12 gx 16 + btn-y 5 + 2 draw-digit ;
: icon-save ( x w -- )
  10 - 2 / + { cx }
  cx btn-y 4 + 10 10 frame
  cx 2 + btn-y 6 + 6 3 box ;
: icon-quit ( x w -- ) 6 - 2 / +  13 swap btn-y 5 + 2 draw-digit ;

: button-color ( id -- $rrggbb )
  dup btn-hover = if
    LEFT-BUTTON pressed? if drop $5a6aa0 else drop $3a4680 then exit
  then
  0= playing and if $1c7c1c else $28325a then ;
: draw-button { id }
  id cells btn-x + @ { x }   id cells btn-w + @ { w }
  id button-color color   x btn-y w btn-h box
  white color
  id 0 = if x w icon-play then
  id 1 = if x w icon-minus then
  id 2 = if x w icon-plus then
  id 3 = if x w icon-clear then
  id 4 = if x w icon-save then
  id 5 = if x w icon-quit then ;
: draw-buttons   buttons 0 do i draw-button loop ;

: draw-all
  $101830 color cls
  draw-buttons
  white color   242 10 2 draw-bpm
  draw-track-tabs draw-pattern-tabs draw-song-tabs draw-instrument draw-note-len
  draw-keys draw-grid
  flip ;

: frame-step
  events
  key-esc pressed? if quit! then
  handle-keys handle-mouse
  advance-playhead
  draw-all
  dt 8 < if 8 dt - delay then ;
: run begin frame-step quit? until close-screen ;

: demo-pattern
  60 0 0 0 4 set-note   64 0 0 4 2 set-note   67 0 0 8 2 set-note   64 0 0 12 4 set-note
  36 0 1 0 4 set-note   36 0 1 4 4 set-note   43 0 1 8 4 set-note   36 0 1 12 4 set-note
  72 0 2 2 2 set-note   76 0 2 6 2 set-note   79 0 2 10 2 set-note   76 0 2 14 2 set-note
  84 0 3 0 8 set-note ;

." click: grid notes, tabs (track/pattern/song, + adds), buttons  space: play/stop  up/down: tempo  1-4: track  esc: quit" cr
340 constant field-w   260 constant field-h
3 to zoom
field-w field-h screen
s" Piano roll" title
demo-pattern
run
bye
