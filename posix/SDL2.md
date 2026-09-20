# SDL2 for the POSIX build

Windows, drawing, keyboard, mouse, images, text and sound for games,
through the shared libraries of SDL2. Nothing is linked into `ueforth`:
the libraries are opened at run time (`dlopen`) the first time you use them.

```forth
sdl2                          \ load, sdl2 becomes your vocabulary
3 to zoom  320 240 screen     \ 320x240 logical pixels, shown 3 times bigger
: frame  events  black color cls  red color 10 10 50 50 box  flip ;
: run  begin frame quit? until close-screen ;
run
```

See `examples/sdl2_demo.fs` for a small tour of the words,
`examples/sdl2_breakout.fs` for a complete game (breakout, about 200 lines)
`examples/sdl2_drums.fs` for a minimal drum machine (16 steps, 4 drums,
all of it usable with the mouse, or with the keyboard) and
`examples/sdl2_roguelike.fs` for a minimal roguelike drawn from a tile sheet
(`examples/sdl2_tiles.png`, 8 tiles of 8x8, loaded with `sdl2-image` and
drawn with `draw-part`; `examples/make_sdl2_tiles.fs`, itself a ueforth program, draws that sheet, and
`examples/make_sdl2_tiles.py` does the same in Python to compare the two).
Breakout, drums and roguelike also run in the browser.

The complete list of words, with stack effects, is in [SDL2-words.md](SDL2-words.md).

The short words also exist in the web build, so a game written with them runs
in a browser too: see [../web/SDL2.md](../web/SDL2.md).

## Packages

| Loader        | Library needed       | Debian/Ubuntu package  | Adds                        |
|---------------|----------------------|------------------------|-----------------------------|
| `sdl2`        | libSDL2-2.0.so.0     | `libsdl2-2.0-0`        | window, drawing, input, beeps |
| `sdl2-image`  | libSDL2_image-2.0.so.0 | `libsdl2-image-2.0-0` | sprites (PNG, JPG, BMP...)  |
| `sdl2-ttf`    | libSDL2_ttf-2.0.so.0 | `libsdl2-ttf-2.0-0`    | text with TrueType fonts    |
| `sdl2-mixer`  | libSDL2_mixer-2.0.so.0 | `libsdl2-mixer-2.0-0` | sound files, music, mixing  |

The loaders are ordinary words that load their source on first use,
like `x11` does. Typing one again just puts `sdl2` back on top.
All words below live in the `sdl2` vocabulary, together with the raw C
names (`SDL_Init`, `SDL_RenderFillRect`, ...) when you need something
that has no short word. `sdl2` also sets itself as the vocabulary where
your own definitions go, so they are never hidden by library words.

## Short words (`sdl2`)

Screen: `screen ( w h -- )`, `close-screen`, `zoom` (value, set before `screen`),
`title ( a n -- )`, `fullscreen ( f -- )`, `cursor ( f -- )`, `width`, `height`.

Colors: `color ( $rrggbb -- )`, `rgb ( r g b -- )`, `rgba ( r g b a -- )`,
`alpha ( a -- )`, and named colors `black white red green blue yellow cyan
magenta orange purple pink brown gray` (`red color`).

Drawing: `cls`, `dot ( x y -- )`, `line ( x1 y1 x2 y2 -- )`, `box ( x y w h -- )`,
`frame ( x y w h -- )`, `hline ( x y w -- )`, `disc ( cx cy r -- )`,
`circle ( cx cy r -- )`, `flip`, `pixel@ ( x y -- $rrggbb )`.

Frame loop: `events` (call once per frame), `quit?`, `quit!`, `wait`,
`ticks ( -- ms )`, `delay ( ms -- )`, `dt` (ms taken by the last frame).

Input: `pressed? ( key -- f )` with `[char] a`, `key-left key-right key-up
key-down key-space key-enter key-esc key-tab key-backspace`,
`LEFT-BUTTON MIDDLE-BUTTON RIGHT-BUTTON`, `mouse-x`, `mouse-y`, `last-key`,
`wheel`. Same names and meaning as the generic graphics interface.

Beeps (no library beyond libSDL2): `beep ( freq ms -- )` queues a tone,
so several `beep` in a row play as a melody. `wave` picks the shape
(0 square, 1 triangle, 2 saw, 3 noise), `beep-volume`, `silence`, `beeping?`.

Hits: `hit ( freq ms -- )` is like `beep` but starts at once and plays over
the other sounds (8 at a time), fading out; `pitch-drop` (percent) makes the
pitch fall while it plays, which is enough for a kick drum. The mix is queued
about 50 ms ahead by `hit`, `flip` and `delay`, so call `flip` or `delay`
regularly, as any game loop does. A `beep` waits for the queue and a hit does
not: use one kind of sound per program.

## Images (`sdl2-image`)

`load-image ( a n -- img )`, `free-image`, `image-size ( img -- w h )`,
`draw ( img x y -- )`, `draw-size ( img x y w h -- )`,
`draw-part ( img sx sy sw sh x y w h -- )` for sprite sheets,
`image-alpha`, `image-tint`. Load images after `screen`. A name with
directories that is not found is tried again without them, so
`s" examples/tiles.png" load-image` also works from inside `examples/`.

## Text (`sdl2-ttf`)

`load-font ( a n pt -- font )`, `font ( font -- )`, `free-font`,
`text ( x y a n -- )` in the current color, `text-size ( a n -- w h )`.

## Sound (`sdl2-mixer`)

`load-sound ( a n -- snd )`, `play ( snd -- )`, `free-sound`, `sound-volume`,
`stop-sounds`, `sounds-playing`, `load-music ( a n -- mus )`,
`music ( mus -- )` (loops), `music-once`, `stop-music`, `music-volume`,
`music?`, `free-music`. Overlapping sounds are mixed.

## Limits

* Calls only pass integers and pointers. SDL functions that take floats or
  structs by value (`SDL_RenderCopyEx`, rotation, ...) are not available.
  Text colors work because `SDL_Color` fits in one register.
* No callbacks from C into Forth, so audio uses queues (`beep`, `hit`) or the mixer.
* Mouse positions are in logical pixels, whatever the `zoom`: checked with a
  real pointer in a `zoom` 3 window (pointer at 97,73 of a 192x144 window
  reads as 32,24).
* `pressed?` reads the held state once per `events`. A key or button pressed
  and released within one frame (about 16 ms) is not seen; a human click is
  far longer.
* ueforth ignores case, so `SDL_Quit` (function) and `SDL_QUIT` (event type)
  are the same name. The function is bound as `sdl-shutdown`; `SDL_QUIT` is the
  event constant.

## Tests

They need the libraries, so they are not part of `ninja posix`:

```sh
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/ueforth posix/sdl2_tests.fs
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy out/posix/ueforth posix/sdl2_media_tests.fs
```

The `dummy` drivers run without a screen or a sound card; pixels are read
back with `SDL_RenderReadPixels`.
