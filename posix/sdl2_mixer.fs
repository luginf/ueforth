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

( Lazy load SDL2_mixer: sound effects and music files, mixed together )
\
\ Usage:
\   sdl2-mixer
\   s" boom.wav" load-sound constant boom
\   boom play                      \ overlapping sounds are mixed
\   s" theme.ogg" load-music music \ loops until stop-music
\ Sounds can be WAV, OGG, MP3, FLAC depending on the installed decoders.
\ NOTE: the block below is a string, so it must not contain a vertical bar.

: sdl2-mixer r|

sdl2 definitions also posix

z" libSDL2_mixer-2.0.so.0" shared-library sdlmix

z" Mix_OpenAudio" 4 sdlmix Mix_OpenAudio ( n n n n -- n )
z" Mix_CloseAudio" 0 sdlmix Mix_CloseAudio ( -- void )
z" Mix_LoadWAV_RW" 2 sdlmix Mix_LoadWAV_RW ( a n -- a )
z" Mix_FreeChunk" 1 sdlmix Mix_FreeChunk ( a -- void )
z" Mix_PlayChannelTimed" 4 sdlmix Mix_PlayChannelTimed ( n a n n -- n )
z" Mix_Volume" 2 sdlmix Mix_Volume ( n n -- n )
z" Mix_HaltChannel" 1 sdlmix Mix_HaltChannel ( n -- n )
z" Mix_Playing" 1 sdlmix Mix_Playing ( n -- n )
z" Mix_LoadMUS" 1 sdlmix Mix_LoadMUS ( z -- a )
z" Mix_FreeMusic" 1 sdlmix Mix_FreeMusic ( a -- void )
z" Mix_PlayMusic" 2 sdlmix Mix_PlayMusic ( a n -- n )
z" Mix_HaltMusic" 0 sdlmix Mix_HaltMusic ( -- n )
z" Mix_VolumeMusic" 1 sdlmix Mix_VolumeMusic ( n -- n )
z" Mix_PlayingMusic" 0 sdlmix Mix_PlayingMusic ( -- n )

44100 constant MIX_RATE
2 constant MIX_CHANNELS
0 value mixer-open

: mixer-open? ( -- f ) mixer-open 0<> ;
: mixer-init ( -- )
  mixer-open? if exit then
  SDL_INIT_AUDIO SDL_InitSubSystem drop
  MIX_RATE AUDIO_S16SYS MIX_CHANNELS 1024 Mix_OpenAudio sign-extend 0<
  if sdl-error type cr -1 throw then
  -1 to mixer-open
;

: load-sound ( a n -- snd )
  mixer-init  s>z z" rb" SDL_RWFromFile 1 Mix_LoadWAV_RW
  dup 0= if sdl-error type cr -1 throw then
;
: free-sound ( snd -- ) Mix_FreeChunk drop ;
: play ( snd -- ) -1 swap 0 -1 Mix_PlayChannelTimed drop ;
: sound-volume ( 0..128 -- ) -1 swap Mix_Volume drop ;
: stop-sounds ( -- ) -1 Mix_HaltChannel drop ;
: sounds-playing ( -- n ) -1 Mix_Playing sign-extend ;

: load-music ( a n -- mus )
  mixer-init s>z Mix_LoadMUS
  dup 0= if sdl-error type cr -1 throw then
;
: free-music ( mus -- ) Mix_FreeMusic drop ;
: music ( mus -- ) -1 Mix_PlayMusic drop ;
: music-once ( mus -- ) 1 Mix_PlayMusic drop ;
: stop-music ( -- ) Mix_HaltMusic drop ;
: music-volume ( 0..128 -- ) Mix_VolumeMusic drop ;
: music? ( -- f ) Mix_PlayingMusic sign-extend 0<> ;

: mixer-close ( -- )
  mixer-open? if Mix_CloseAudio drop 0 to mixer-open then ;
' mixer-close to close-hook

previous
| evaluate ;
