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

( Reader for tunes in ABC notation, shared by the POSIX and Web builds )
\
\ Not loaded by itself: abc-parser gives the source of the reader as a string,
\ and the loaders sdl2-abc of posix/sdl2_abc.fs and web/sdl2_abc.fs evaluate it,
\ then add what plays the tune. See posix/SDL2.md for the words it defines.
\
\ What is understood of ABC (https://abcnotation.com): the fields K: (key and
\ mode) L: M: Q: and the line %%MIDI program n; notes A to G and a to g with
\ ^ _ = , and ' marks, lengths like 2 /2 3/2, rests z and x, chords [CEG],
\ ties, broken rhythm > <, tuplets (3, bar lines. Words, decorations, grace
\ notes, repeats, voices and other fields are skipped. One voice, as in a
\ melody line. NOTE: the source below is a string, so it must not contain a
\ vertical bar (that is the ABC bar line: the reader tests for character 124).

internals definitions
: abc-parser r|
\ The tune: notes with a start, a length and a MIDI key, in ticks of 1/1920 of a
\ whole note (a quarter note is 480 ticks, as in a MIDI file).
0 value tune-program   ( General MIDI instrument, 0 to 127; %%MIDI program sets it )
120 value tune-bpm     ( quarter notes per minute; Q: sets it )

vocabulary abc-int   also abc-int definitions

1024 constant max-notes
create nt max-notes cells allot
create nd max-notes cells allot
create nk max-notes cells allot
: nt@ ( i -- v ) cells nt + @ ;   : nt! ( v i -- ) cells nt + ! ;
: nd@ ( i -- v ) cells nd + @ ;   : nd! ( v i -- ) cells nd + ! ;
: nk@ ( i -- v ) cells nk + @ ;   : nk! ( v i -- ) cells nk + ! ;

0 value n-notes   0 value cur      ( events so far, and the time now )
0 value p   0 value e              ( the text being read )
240 value unit   0 value l-set     ( length of a plain note, and was it given by L: )
create key-acc 7 cells allot       ( sharps and flats of the key, by C D E F G A B )
create bar-acc 70 cells allot      ( accidentals of the bar: 10 octaves by 7 letters )
create chord-keys 16 cells allot   0 value chord-n
0 value tie   0 value tie-key
1 value bn   1 value bd            ( broken rhythm: length of the next note as bn/bd )
0 value tup-left   1 value tup-num   1 value tup-den
0 value grp   0 value grp-d        ( first event of the last note, and its length )
0 value bol                        ( at the start of a line )
0 value len-n   1 value len-d      ( a length just read: len-n/len-d of a plain note )

( ---- Reading text ---- )
: more? ( -- f ) p e < ;
: peek ( -- c ) more? if p c@ else 0 then ;
: peek2 ( -- c ) p 1+ e < if p 1+ c@ else 0 then ;
: advance ( -- ) 1 +to p ;
: blank? ( c -- f ) dup 32 = swap 9 = or ;
: digit? ( c -- f ) dup 47 > swap 58 < and ;
: letter? ( c -- f ) 32 or dup 96 > swap 104 < and ;   ( a to g, either case )
: alpha? ( c -- f ) 32 or dup 96 > swap 123 < and ;
: skip-blanks ( -- ) begin peek blank? more? and while advance repeat ;
: skip-through ( c -- )   ( goes past the next c, or to the end )
  begin more? while peek over = advance if drop exit then repeat drop ;
: read-int ( -- n )
  0 begin peek digit? more? and while 10 * peek 48 - + advance repeat ;

: read-length ( -- )   ( 3 or 3/2 or /2 or / or //, into len-n and len-d )
  read-int dup 0= if drop 1 then to len-n
  1 to len-d
  begin peek 47 = more? and while
    advance
    read-int dup 0= if drop len-d 2 * then to len-d
  repeat ;

: ticks-of ( -- ticks )   ( the length just read, with broken rhythm and tuplet )
  unit len-n * bn * tup-num *   len-d bd * tup-den * / ;

: item-done ( -- )   ( a note, chord or rest is over: its temporary changes end )
  1 to bn   1 to bd
  tup-left 0 > if
    -1 +to tup-left
    tup-left 0= if 1 to tup-num   1 to tup-den then
  then ;

( ---- Pitch ---- )
create semis 0 c, 2 c, 4 c, 5 c, 7 c, 9 c, 11 c,
: letter-idx ( c -- 0..6 )   ( C D E F G A B )   32 or 99 - 7 + 7 mod ;
: bar-reset ( -- ) bar-acc 70 cells erase ;

0 value pre-acc   0 value pre-set   0 value pch-idx   0 value pch-oct   0 value slot-i
: read-accidental ( -- )   ( ^ sharp, _ flat, = natural, before a note )
  0 to pre-acc   0 to pre-set
  begin
    more?  peek dup 94 = over 95 = or swap 61 = or  and
  while
    -1 to pre-set
    peek 94 = if 1 +to pre-acc then
    peek 95 = if -1 +to pre-acc then
    advance
  repeat ;

: read-pitch ( -- key )
  read-accidental
  peek letter-idx to pch-idx
  peek 96 > if 5 else 4 then to pch-oct
  advance
  begin peek 39 = peek 44 = or more? and while
    peek 39 = if 1 +to pch-oct else -1 +to pch-oct then
    advance
  repeat
  pch-oct 0 max 9 min 7 * pch-idx + to slot-i
  pre-set if
    pre-acc 3 + slot-i cells bar-acc + !   pre-acc
  else
    slot-i cells bar-acc + @ dup if 3 - else drop pch-idx cells key-acc + @ then
  then
  { acc }
  pch-oct 1+ 12 *  pch-idx semis + c@ +  acc + ;

( ---- Notes, rests and chords ---- )
: event! ( key ticks -- )   ( adds an event at the current time )
  n-notes max-notes < 0= if 2drop exit then
  n-notes nd!   n-notes nk!   cur n-notes nt!   1 +to n-notes ;

: note! ( key ticks -- )   ( a note, joined to the one before when tied )
  { k d }
  d to grp-d
  tie if
    0 to tie
    k tie-key = n-notes 0 > and if
      n-notes 1 - nd@ d + n-notes 1 - nd!   d +to cur   exit
    then
  then
  n-notes to grp   k d event!   k to tie-key   d +to cur ;

: rest! ( ticks -- )   n-notes to grp   dup to grp-d   +to cur   0 to tie ;

: note ( -- ) read-pitch read-length ticks-of note! item-done ;
: rest ( -- ) advance read-length ticks-of rest! item-done ;

: chord ( -- )   ( after the [ : the notes up to the ] and a length )
  0 to chord-n   0 to tie
  1 { in-n }   1 { in-d }
  begin more? peek 93 = 0= and while
    peek letter? peek 94 = or peek 95 = or peek 61 = or if
      read-pitch
      chord-n 16 < if chord-n cells chord-keys + !  1 +to chord-n else drop then
      read-length
      chord-n 1 = if len-n to in-n   len-d to in-d then
    else advance then
  repeat
  advance
  read-length
  len-n in-n * to len-n   len-d in-d * to len-d
  ticks-of { d }
  n-notes to grp   d to grp-d
  chord-n 0 ?do  i cells chord-keys + @  d event!  loop
  d +to cur   item-done ;

( ---- Broken rhythm and tuplets ---- )
: scale-last ( n d -- )   ( the last note or rest becomes n/d of its length )
  { n d }
  grp-d n * d / { new }
  n-notes grp ?do new i nd! loop
  new grp-d - +to cur   new to grp-d ;

: broken ( c -- )   ( c is the > or < just read: one lengthens, the other shortens )
  { c }   1 { k }
  begin peek c = more? and while advance k 1+ to k repeat
  1 k lshift { f }
  c 62 = if
    f 2 * 1 -  f scale-last   1 to bn   f to bd
  else
    1 f scale-last   f 2 * 1 - to bn   f to bd
  then ;

: tuplet-q ( p -- q ) dup 2 = over 4 = or over 8 = or if drop 3 else drop 2 then ;
: tuplet ( -- )   ( after the ( of (3 : the next p notes take the time of q )
  read-int { p }
  p 2 < if exit then
  p to tup-left   p to tup-den   p tuplet-q to tup-num ;

( ---- Information fields ---- )
create root-fifths 0 , 2 , 4 , -1 , 1 , 3 , 5 ,
create sharps-order 3 , 0 , 4 , 1 , 5 , 2 , 6 ,
create flats-order 6 , 2 , 5 , 1 , 4 , 0 , 3 ,

: set-key ( fifths -- )
  key-acc 7 cells erase
  dup 0 > if  7 min 0 ?do  1 i cells sharps-order + @ cells key-acc + !  loop exit then
  dup 0 < if  negate 7 min 0 ?do  -1 i cells flats-order + @ cells key-acc + !  loop exit then
  drop ;

: mode-offset ( -- n )   ( the mode word after the key: m, min, dor, mix... )
  skip-blanks
  peek 32 or { m1 }   peek2 32 or { m2 }
  m1 109 = if
    m2 97 = if 0 exit then
    m2 105 = if p 2 + e < if p 2 + c@ 32 or 120 = if -1 exit then then then
    -3 exit
  then
  m1 100 = if -2 exit then
  m1 112 = if -4 exit then
  m1 108 = if  m2 121 = if 1 else -5 then exit then
  m1 97 = if -3 exit then
  0 ;

: read-key ( -- )
  peek letter? 0= if 0 set-key exit then
  peek letter-idx cells root-fifths + @ { f }
  advance
  peek 35 = if 7 +to f advance then
  peek 98 = if -7 +to f advance then
  mode-offset +to f
  f set-key ;

: read-unit ( -- )   ( L:1/8 )
  read-int { a }
  peek 47 = if advance read-int else 1 then { b }
  b 0 > 0= if exit then
  1920 a * b /  to unit   -1 to l-set ;

: read-meter ( -- )   ( M:3/4 : a short meter makes the default note a sixteenth )
  l-set if exit then
  read-int { a }
  peek 47 = if advance read-int else 1 then { b }
  a 4 * b 3 * < a 0 > and if 120 else 240 then to unit ;

0 value t-a   0 value t-b
: read-tempo ( -- )   ( Q:1/4=120 or Q:120 )
  peek 34 = if advance 34 skip-through then
  skip-blanks
  read-int to t-a
  peek 47 = if
    advance read-int to t-b
    skip-blanks
    peek 61 = if
      advance skip-blanks read-int
      t-b 0 > if t-a 4 * * t-b / to t-a else drop then
    then
  then
  t-a 20 max 400 min to tune-bpm ;

: field ( end -- )   ( a field like K:G, up to the end character )
  { end }   peek { f }   advance advance skip-blanks
  f 75 = if read-key then
  f 76 = if read-unit then
  f 77 = if read-meter then
  f 81 = if read-tempo then
  end skip-through ;

0 value dir-a
: directive ( -- )   ( a % line: %%MIDI program 24 sets the instrument, others are ignored )
  advance
  peek 37 = if
    advance skip-blanks
    p 4 s" MIDI" str= if
      4 +to p skip-blanks
      p 7 s" program" str= if
        7 +to p skip-blanks
        read-int to dir-a   skip-blanks
        peek digit? if read-int else dir-a then to tune-program
      then
    then
  then
  10 skip-through ;

( ---- The whole tune ---- )
: token ( -- )
  peek { c }
  c 10 = if advance 1 to bol exit then
  c blank? if advance exit then
  bol if
    0 to bol
    c alpha? peek2 58 = and if 10 field 1 to bol exit then
  then
  c 37 = if directive 1 to bol exit then
  c letter? c 94 = or c 95 = or c 61 = or if note exit then
  c 122 = c 120 = or if rest exit then
  c 91 = if
    p 2 + e < if p 2 + c@ 58 = if advance 93 field exit then then
    peek2 letter? peek2 94 = or peek2 95 = or peek2 61 = or if advance chord exit then
    advance bar-reset exit
  then
  c 124 = c 58 = or if bar-reset advance exit then
  c 45 = if -1 to tie advance exit then
  c 62 = c 60 = or if advance c broken exit then
  c 40 = if advance tuplet exit then
  c 34 = if advance 34 skip-through exit then
  c 33 = if advance 33 skip-through exit then
  c 123 = if advance 125 skip-through exit then
  advance ;

also sdl2 definitions

: abc ( a n -- )   ( reads a tune in ABC notation, ready for tune-play )
  over + to e   to p
  0 to n-notes   0 to cur   240 to unit   0 to l-set   0 set-key   bar-reset
  0 to tie   1 to bn   1 to bd   1 to tup-num   1 to tup-den   0 to tup-left
  0 to grp   0 to grp-d   1 to bol   120 to tune-bpm   0 to tune-program
  begin more? while token repeat ;

: tune-notes ( -- n ) n-notes ;
: tune-ms ( -- ms ) cur 125 * tune-bpm / ;

previous previous

| ;
forth definitions
