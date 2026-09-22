# sdl2 short words for the Web

The web build has the same `sdl2` short words as the Linux build
(`posix/SDL2.md`), drawn on an HTML canvas. A game written with them runs
unchanged in both: `examples/sdl2_breakout.fs` is loaded as is by
`web/sdl2_breakout.html`, `examples/sdl2_drums.fs` by
`web/sdl2_drums.html` and `examples/sdl2_roguelike.fs` (with its tile sheet
`examples/sdl2_tiles.png`) by `web/sdl2_roguelike.html`.

```forth
sdl2                          \ loads on first use, like on Linux
3 to zoom  320 240 screen     \ opens a canvas over the page
: frame  events  black color cls  red color 10 10 50 50 box  flip ;
: run  begin frame quit? until close-screen ;
run
```

## Try the games

The page loads its Forth file with an XMLHttpRequest, which browsers refuse
on `file://`, so serve the build directory over http:

```sh
ninja web
cd out/web && python3 -m http.server
# then open http://localhost:8000/sdl2_breakout.html
# or            http://localhost:8000/sdl2_drums.html
# or            http://localhost:8000/sdl2_roguelike.html
```

Arrows, mouse or touch move the paddle. Space, click or tap launches the
ball. Esc quits, and the page then offers "Play again".

In the drum machine everything can be done with the mouse: click or drag on
the grid, buttons for play/stop, tempo - and + (1 bpm with the left button,
5 with the right one, hold to repeat), CLR and X (quit), and the colored pads
on the left play the drums. The keyboard works too: space, up/down (5 bpm),
1 2 3 4, c, Esc.

In the roguelike, arrows or w a s d move, space waits and a click steps
toward the clicked tile.

## What exists

These words have the same names and stack effects as on Linux, see
[../posix/SDL2-words.md](../posix/SDL2-words.md) for what each one does:

* Screen: `screen close-screen screen-open? zoom width height title
  fullscreen cursor`
* Colors: `color rgb rgba alpha` and the named colors `black white red green
  blue yellow cyan magenta orange purple pink brown gray`
* Drawing: `cls dot line box frame hline disc circle flip pixel@`
* Timing: `ticks delay dt`
* Input: `events quit? quit! pressed? last-key mouse-x mouse-y wheel`, the
  `key-` constants (`key-left key-right key-up key-down key-space key-enter
  key-esc key-tab key-backspace`) and `LEFT-BUTTON MIDDLE-BUTTON RIGHT-BUTTON`
* Images (the words of `sdl2-image`, which is a no-op here):
  `load-image free-image image-size draw draw-size draw-part image-alpha
  image-tint`
* Beeps and hits: `beep hit pitch-drop silence beeping? wave beep-volume
  audio-open audio-open?`

## What is different

* **Not available:** the raw `SDL_` words, `sdl2-ttf`, the sound and music
  files of `sdl2-mixer` (`load-sound`, `play`, `load-music`, `music`...), and a
  few helpers: `wait sdl-error window-handle renderer pen
  pen-r pen-g pen-b pen-a sample-rate`.
* **`screen`** puts a black layer with its own canvas over the whole page, so
  the terminal is hidden while the game runs. The canvas keeps its aspect
  ratio, shown at `zoom` times its size or smaller if the window is too
  small. `close-screen` removes it.
* **`flip`** waits for the next animation frame, so games run at the screen
  refresh rate, and stop while the tab is hidden.
* **Keys** are taken from the terminal while a screen is open: they do not
  end up typed in it. Combinations with Ctrl, Alt or Meta and the F keys are
  not intercepted (Ctrl combinations are still handled by the terminal, as
  in any page of the web build).
* **`pressed?`** also sees a key or button pressed and released between two
  `events`, which the Linux version misses.
* **Mouse and touch** positions are in logical pixels, as on Linux, and a
  touch counts as `LEFT-BUTTON`.
* **Images** are loaded by the browser, so any format it reads works.
  `load-image` waits for the file (the interpreter yields meanwhile) and
  throws if it cannot be loaded. As on Linux, a name with directories that is
  not found is tried again without them: `s" examples/sdl2_tiles.png"` finds
  `sdl2_tiles.png` next to the page. `image-tint` multiplies the colors, as
  in SDL, using a copy of the image kept for each color.
* **Sound** starts after the first key press or click, because browsers
  refuse to play sound before. Beeps before that are dropped, and a queue
  more than two seconds long is not extended. A `hit` plays at once over
  the other sounds, with at most 24 sounds alive.
* **Tunes** (`sdl2-abc`, the reader of ABC notation, see `posix/SDL2.md`) have
  the same words as on Linux: `abc abc-play abc-loop tune-play tune-loop
  tune-stop tune-playing? tune-bpm tune-program tune-ms tune-notes`, but there
  is no MIDI synthesizer or soundfont in a browser. Each note is played by an
  oscillator (the triangle wave, or the one chosen with `wave`), so the
  instrument of `%%MIDI program` is ignored, and `tune-midi` and `tune-save`
  do not exist. `soundfont?` always answers false and `soundfont` does nothing,
  so a program that names a soundfont runs unchanged; `sdl2-mixer` only exists
  to be typed and gives `music-volume`. A looping tune waits for the sound to
  be allowed and starts at its next repeat if it was not yet. Loudness is
  `music-volume` (0 to 128, as on Linux).
* **`line`** uses Bresenham's algorithm and may differ by a pixel from the
  SDL one.
* **`bye`** stops the web interpreter. A game file that ends with `bye`, like
  the breakout, leaves the page inert after `run` returns (the browser console
  shows an `objects[op] is not a function` error, which a plain `bye` in the
  terminal page gives as well). Reload the page to start again.

## Tests

Checked with Chrome 150 without a screen, driven through the DevTools
protocol: the page loads without console errors, the breakout is drawn,
moving the pointer and holding a key move the paddle, a very short tap on
space launches the ball, a bot following the ball with the mouse breaks
bricks and schedules sound, Esc closes the screen. The drum machine page
was checked the same way: a click on the grid switches a step, the buttons
start and stop the sequencer (sounds are scheduled, several at once), change
the tempo, clear the grid and quit, and a pad click plays its drum. The roguelike page too: the tile sheet loads
(64x8), keys and clicks play a turn without reaching the terminal, remembered
tiles are darker, monsters chase and hurt, Esc closes the screen. Its music was
checked too: the tune is read (37 notes, 21.8 s at 88 bpm), all its notes are
scheduled and repeat, `m` stops and restarts it, and Esc stops it. Firefox and
Safari are not tested.
