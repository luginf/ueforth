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

( Lazy load SDL2_image: sprites from PNG, JPG, BMP... files )
\
\ Usage (after opening a screen):
\   sdl2-image
\   s" ship.png" load-image constant ship   \ also finds it beside the script
\   ship 100 50 draw
\   ship 0 0 16 16  10 10 32 32 draw-part   \ sprite sheet cell, scaled
\ NOTE: the block below is a string, so it must not contain a vertical bar.

: sdl2-image r|

sdl2 definitions also posix

z" libSDL2_image-2.0.so.0" shared-library sdlimg

z" IMG_LoadTexture" 2 sdlimg IMG_LoadTexture ( a z -- a )

create srect 4 cells allot   ( source rectangle for sprite sheets )
create isize 2 cells allot

: try-image ( a n -- img, or 0 ) s>z renderer swap IMG_LoadTexture ;
\ A name with directories that is not found is tried again without them.
: load-image ( a n -- img )
  2dup try-image  dup if nip nip exit then  drop
  base-name try-image
  dup 0= if sdl-error type cr -1 throw then
;
: free-image ( img -- ) SDL_DestroyTexture drop ;
: image-size ( img -- w h )
  0 0 isize isize 4 + SDL_QueryTexture drop   isize sl@ isize 4 + sl@ ;

: draw-part ( img sx sy sw sh x y w h -- )
  { img sx sy sw sh x y w h }
  sx sy sw sh srect rect-at!
  x y w h rect rect-at!
  renderer img srect rect SDL_RenderCopy drop
;
: draw-size ( img x y w h -- )
  { img x y w h }
  x y w h rect rect-at!
  renderer img 0 rect SDL_RenderCopy drop
;
: draw ( img x y -- ) { img x y }  img image-size { w h }  img x y w h draw-size ;
: image-alpha ( img a -- ) SDL_SetTextureAlphaMod drop ;
: image-tint ( img $rrggbb -- )
  { img c }  img c 16 rshift 8bit c 8 rshift 8bit c 8bit SDL_SetTextureColorMod drop ;

previous
| evaluate ;
