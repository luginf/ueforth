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

( Lazy load SDL2_ttf: draw text with TrueType fonts )
\
\ Usage (after opening a screen):
\   sdl2-ttf
\   s" /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" 16 load-font font
\   white color   10 10 s" Hello" text
\ Each call to text builds a texture, fine for a score or a menu.
\ NOTE: the block below is a string, so it must not contain a vertical bar.

: sdl2-ttf r|

sdl2 definitions also posix

z" libSDL2_ttf-2.0.so.0" shared-library sdlttf

z" TTF_Init" 0 sdlttf TTF_Init ( -- n )
z" TTF_Quit" 0 sdlttf TTF_Quit ( -- void )
z" TTF_OpenFont" 2 sdlttf TTF_OpenFont ( z n -- a )
z" TTF_CloseFont" 1 sdlttf TTF_CloseFont ( a -- void )
z" TTF_SizeUTF8" 4 sdlttf TTF_SizeUTF8 ( a z a a -- n )
( the SDL_Color argument is passed packed as r + g<<8 + b<<16 + a<<24 )
z" TTF_RenderUTF8_Blended" 3 sdlttf TTF_RenderUTF8_Blended ( a z n -- a )

0 value current-font
create tsize 2 cells allot

: load-font ( a n pt -- font )
  { pt } s>z
  TTF_Init sign-extend 0< if sdl-error type cr -1 throw then
  pt TTF_OpenFont
  dup 0= if sdl-error type cr -1 throw then
;
: free-font ( font -- ) TTF_CloseFont drop ;
: font ( font -- ) to current-font ;

: text-size ( a n -- w h )
  s>z current-font swap tsize tsize 4 + TTF_SizeUTF8 drop
  tsize sl@ tsize 4 + sl@ ;

: text ( x y a n -- )
  dup 0= if 2drop 2drop exit then
  s>z { x y z }
  current-font z pen TTF_RenderUTF8_Blended { surf }
  surf 0= if exit then
  renderer surf SDL_CreateTextureFromSurface { tex }
  tex if
    tex 0 0 tsize tsize 4 + SDL_QueryTexture drop
    x y tsize sl@ tsize 4 + sl@ rect rect-at!
    renderer tex 0 rect SDL_RenderCopy drop
    tex SDL_DestroyTexture drop
  then
  surf SDL_FreeSurface drop
;

previous
| evaluate ;
