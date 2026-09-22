#!/usr/bin/env python3

# Copyright 2026 luginf
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

"""Writes WORDS.md, a reference of the words of uEforth.

The list of words is taken from the real interpreters (out/posix/ueforth, and
out/web/ueforth.js run by node), so it is exact. Each word is described from
what the project already says about it, in this order:
  1. the reference pages site/*.html,
  2. tools/words_notes.py (short notes for the words the pages leave out),
  3. the stack comment in the Forth sources, or the C function a word calls,
and words of the ANS Forth standard are marked. Build first (ninja posix web).

    python3 tools/words_reference.py
"""

import glob, html, json, os, re, subprocess, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) + '/'
sys.path.insert(0, ROOT + 'tools')
from words_notes import NOTES

POSIX_VOCABS = ['forth', 'internals', 'posix', 'structures', 'streams', 'tasks',
                'ansi', 'editor', 'sockets', 'x11', 'graphics', 'httpd',
                'telnetd', 'web-interface', 'termios', 'visual', 'recognizers']
WEB_VOCABS = ['forth', 'internals', 'web', 'structures', 'streams', 'tasks',
              'ansi', 'editor', 'graphics', 'recognizers']

# ---- Names of ANS Forth 2012 words (only those we are sure about) ----
ANS_TEXT = """
! # #> #s ' ( * */ */mod + +! +loop , - . ." / /mod 0< 0= 1+ 1- 2! 2* 2/ 2@ 2drop 2dup 2over 2swap
: ; < <# = > >body >in >number >r ?dup @ abort abort" abs accept align aligned allot and base begin
bl c! c, c@ cell+ cells char char+ chars constant count cr create decimal depth do does> drop dup
else emit environment? evaluate execute exit fill find fm/mod here hold i if immediate invert j key
leave literal loop lshift m* max min mod move negate or over postpone quit r> r@ recurse repeat rot
rshift s" s>d sign sm/rem source space spaces state swap then type u. u< um* um/mod unloop until
variable while word xor [ ['] [char] ]
#tib .( .r 0<> 0> 2>r 2r> 2r@ :noname <> ?do action-of again buffer: c" case compile, defer defer!
defer@ endcase endof erase false hex holds is marker nip of pad parse parse-name pick refill
restore-input roll s\\" save-input source-id to true tuck u.r u> unused value within [compile] \\
2constant 2literal 2variable d+ d- d. d.r d0< d0= d2* d2/ d< d= d>s dabs dmax dmin dnegate m*/ m+ 2rot 2value du<
catch throw at-xy key? page ms
bin close-file create-file delete-file file-position file-size include-file included open-file r/o r/w
read-file read-line reposition-file resize-file w/o write-file write-line flush-file rename-file
file-status include required
f! f* f+ f- f/ f0< f0= f< f>d f>s f@ falign faligned fconstant fdepth fdrop fdup fliteral floor fmax
fmin fnegate fover frot fround fswap fvariable represent d>f df! df@ dfalign dfaligned dfloat+
dfloats f** f. fabs facos facosh falog fasin fasinh fatan fatan2 fatanh fcos fcosh fe. fexp fexpm1
ffield: fln flnp1 flog fs. fsin fsincos fsinh fsqrt ftan ftanh f~ precision set-precision sf! sf@
sfalign sfaligned sfloat+ sfloats s>f float+ floats
(local) locals| {:
allocate free resize
.s ? dump see words [else] [if] [then] ahead assembler bye code forget name>string synonym
also definitions forth get-current get-order only order previous search-wordlist set-current
set-order wordlist
-trailing /string blank cmove cmove> compare search sliteral unescape replaces substitute
block buffer empty-buffers flush list load save-buffers scr thru update blk
"""
ANS = set(w.lower() for w in ANS_TEXT.split())

# ---- Friendly names for the groups of common/forth_namespace_tests.fs ----
GROUPS = {
  'case': 'Case', 'locals': 'Locals', 'highlevel-floats': 'Floating-Point',
  'float-opcodes': 'Floating-Point', 'boot': 'Boot and system',
  'tier0-opcodes': 'Primitives', 'tier1-opcodes': 'Primitives',
  'opcodes': 'Primitives', 'tier2-opcodes': 'Dictionary entries',
  'files-dir': 'Files', 'files-dir-reverse': 'Files', 'files': 'Files',
  'files-reverse': 'Files', 'fileops': 'Files', 'filetools': 'Files',
  'imports': 'Files', 'blocks': 'Blocks', 'vocabulary': 'Vocabularies',
  '[]conds': 'Interpret Time Conditions', 'utils': 'Utilities',
  'phase1': 'Boot and system', 'phase2': 'Utilities', 'snapshots': 'Dictionary images',
  'ansi': 'Terminal colors', 'tasks': 'Tasks', 'args': 'Arguments',
  'allocation': 'Memory allocation', 'desktop': 'Desktop', 'asm': 'Assembler',
}

def run_posix(vocab):
    out = subprocess.run([ROOT + 'out/posix/ueforth'], input='." @@" %s words ." @@" cr bye\n' % vocab,
                         capture_output=True, text=True, timeout=60).stdout.replace('\r', '')
    parts = out.split('@@')
    return parts[-2].split() if len(parts) >= 4 else None

def version():
    out = subprocess.run([ROOT + 'out/posix/ueforth'], input='bye\n', capture_output=True,
                         text=True, timeout=30).stdout
    m = re.search(r'uEforth v\S+ - rev \S+', out)
    return m.group(0) if m else 'uEforth'

def run_web(vocab, shim):
    out = subprocess.run(['node', shim, ROOT + 'out/web/ueforth.js'],
                         input='." @@" %s words ." @@" cr bye\n' % vocab,
                         capture_output=True, text=True, timeout=60).stdout.replace('\r', '')
    parts = out.split('@@')
    return parts[-2].split() if len(parts) >= 3 else None

def collect():
    posix = {v: run_posix(v) for v in POSIX_VOCABS}
    web = {}
    if os.path.exists(ROOT + 'out/web/ueforth.js'):
        shim = tempfile.mktemp(suffix='.js')
        open(shim, 'w').write("""const fs = require('fs');
const lines = fs.readFileSync(0, 'utf8').split('\\n');
let i = 0;
globalThis.write = s => process.stdout.write(s);
globalThis.readline = () => (i < lines.length ? lines[i++] : 'bye');
globalThis.quit = c => process.exit(c);
globalThis.load = f => { (0, eval)(fs.readFileSync(f, 'utf8')); };
require(process.argv[2]);""")
        web = {v: run_web(v, shim) for v in WEB_VOCABS}
        os.unlink(shim)
    return posix, web

def own_words(table):
    """Words a vocabulary adds to forth (a vocabulary lists the ones it chains to)."""
    forth = set(w.lower() for w in table['forth'] or [])
    own = {}
    for v, ws in table.items():
        if ws is None or v == 'forth':
            continue
        own[v] = [w for w in ws if w.lower() not in forth]
    for v in list(own):   # a vocabulary chained to posix or internals lists their words too
        for parent in ('posix', 'internals'):
            if parent in own and v != parent and set(w.lower() for w in own[parent]) <= set(w.lower() for w in own[v]):
                ps = set(w.lower() for w in own[parent])
                own[v] = [w for w in own[v] if w.lower() not in ps]
    return own

def site_entries():
    entries = {}
    for path in sorted(glob.glob(ROOT + 'site/*.html')):
        page = os.path.basename(path)
        s = open(path, encoding='utf-8').read()
        section = ''
        for m in re.finditer(r'<h[1-6][^>]*>(.*?)</h[1-6]>|<pre[^>]*>(.*?)</pre>', s, re.S):
            if m.group(1) is not None:
                section = html.unescape(re.sub(r'<[^>]+>', '', m.group(1))).strip()
                continue
            body = html.unescape(re.sub(r'<[^>]+>', '', m.group(2)))
            cur = None
            for line in body.split('\n'):
                mm = re.match(r'^(\S+)\s+\(\s*([^)]*)\)\s*(.*)$', line)
                if mm:
                    cur = {'stack': '( %s )' % mm.group(2).strip(), 'desc': mm.group(3).strip(),
                           'section': section, 'page': page}
                    key = mm.group(1).lower()
                    if key not in entries or page != 'ESP32forth.html':
                        entries[key] = cur
                elif cur and line.strip() and line.startswith((' ', '\t')):
                    cur['desc'] += ' ' + line.strip()
                else:
                    cur = None
    return entries

def group_map():
    """word -> group friendly name, from the check-* blocks of the namespace tests."""
    text = open(ROOT + 'common/forth_namespace_tests.fs', encoding='utf-8').read()
    result = {}
    for m in re.finditer(r'^e: check-(\S+)\n(.*?)^;e', text, re.S | re.M):
        name = GROUPS.get(m.group(1))
        if not name:
            continue
        for w in re.findall(r'^\s*out: (\S+)', m.group(2), re.M):
            result.setdefault(w.lower(), name)
    return result

def source_info():
    """Stack comments, constants and C bindings found in the Forth and C sources."""
    stack, desc = {}, {}
    files = sorted(glob.glob(ROOT + 'common/*.fs') + glob.glob(ROOT + 'posix/*.fs') + glob.glob(ROOT + 'web/*.fs'))
    for f in files:
        base = os.path.basename(f)
        if 'sdl2' in base or base in ('abc.fs',) or base.endswith('_tests.fs'):
            continue
        for line in open(f, encoding='utf-8', errors='ignore'):
            m = re.match(r'^:\s+(\S+)\s+\(([^)]*)\)', line)
            if m:
                stack.setdefault(m.group(1).lower(), '( %s )' % m.group(2).strip())
            m = re.match(r'^z" (\w+)" (\d+) (?:sysfunc|sofunc)\s+(\S+)', line)
            if m:
                desc.setdefault(m.group(3).lower(), 'C function `%s`, %s argument%s.' % (m.group(1), m.group(2), '' if m.group(2) == '1' else 's'))
            m = re.match(r'^(-?\d+|\$[0-9a-fA-F]+)\s+constant\s+(\S+)', line)
            if m:
                desc.setdefault(m.group(2).lower(), 'Constant, %s.' % m.group(1))
            m = re.match(r'^JSWORD:\s+(\S+)\s+\{([^}]*)\}', line)
            if m:
                args = m.group(2).strip()
                stack.setdefault(m.group(1).lower(), '( %s )' % args)
                desc.setdefault(m.group(1).lower(), 'JavaScript function of the web page.')
    tiers = {}
    for f in glob.glob(ROOT + 'common/tier*_opcodes.h') + glob.glob(ROOT + 'common/floats.h') + glob.glob(ROOT + 'posix/*.h') + glob.glob(ROOT + 'web/*.h'):
        tier = os.path.basename(f).replace('_opcodes.h', '').replace('.h', '')
        for line in open(f, encoding='utf-8', errors='ignore'):
            m = re.match(r'^\s*(?:X|Y|YV)\(\s*(?:\w+,\s*)?"?([^",\s]+)"?\s*,', line)
            if m:
                tiers.setdefault(m.group(1).lower(), tier)
    return stack, desc, tiers

def cell(text):
    return text.replace('|', '\\|')

def main():
    posix, web = collect()
    pown = own_words(posix)
    wown = own_words(web) if web else {}
    site = site_entries()
    groups = group_map()
    stacks, descs, tiers = source_info()
    pf = set(w.lower() for w in posix['forth'])
    wf = set(w.lower() for w in (web.get('forth') or []))
    names = {}
    for w in posix['forth'] + (web.get('forth') or []):
        names.setdefault(w.lower(), w)
    stats = {'site': 0, 'note': 0, 'ans': 0, 'source': 0, 'none': 0}

    def describe(key, vocab='forth'):
        """(stack, description, kind) for a lower case name."""
        s = site.get(key)
        n = NOTES.get(key)
        if s and s['desc'] and s['page'] != 'ESP32forth.html':
            return s['stack'], s['desc'], 'site'
        if n:
            stack = n[0] or (s['stack'] if s else '') or stacks.get(key, '')
            return stack, n[1], 'note'
        if s and s['desc']:
            return s['stack'], s['desc'], 'site'
        st = stacks.get(key, '') or (s['stack'] if s else '')
        d = descs.get(key, '')
        if not d and key in tiers:
            d = 'Primitive defined in C.'
        if key in ANS and not d:
            d = 'Standard word, see the ANS Forth standard.'
            return st, d, 'ans'
        if st or d:
            return st, d, 'source'
        if s:
            return s['stack'], '', 'site'
        return '', '', 'none'

    T = []
    T.append('# Words of uEforth\n')
    T.append('The words of the interpreter, listed from the real thing: %s, for the Linux build (`out/posix/ueforth`)%s.\n' %
             (version(), ' and the web build (`out/web/ueforth.js`)' if web else ''))
    T.append('''Generated by `tools/words_reference.py`, which asks the interpreters for their
words, so nothing here can be out of date or missing. The descriptions come, in
this order, from the reference pages `site/*.html`, from short notes in
`tools/words_notes.py`, and from the stack comments and bindings in the Forth
sources. A word of the ANS Forth standard that the project does not describe is
marked *ANS*: it does what the standard says (see the links below).

The columns: **Word**, **Stack** effect, **What it does**, and where the word
exists: **P** Linux (POSIX) build, **W** web build.
''')
    T.append('## Other places to read about the words\n')
    T.append('''* [µEforth documentation](https://eforth.appspot.com/linux.html) and
  [ESP32forth](https://eforth.appspot.com/ESP32forth.html): the pages of `site/`,
  published.
* [ESP32forth glossary](https://esp32forth.forth2020.org/glossary), a spreadsheet
  kept by the community, and the
  [v7.5 list of words](https://esp32forth.wordpress.com/esp32forth-v-7-5-list-of-words/),
  which repeats the reference pages.
* [esp32.arduino-forth.com](https://esp32.arduino-forth.com/index/glossaire), in
  French: a glossary and a lexicon per version
  (for example [v7.0712](https://esp32.arduino-forth.com/article/lexiqueESP32forthV70712)),
  with a help page for each word. It follows ESP32forth, the same language on the
  ESP32, so it also covers hardware words that this build does not have.
* [ANS Forth standard](https://forth-standard.org/standard/words), for the
  standard words.
* The SDL2 words are in [posix/SDL2-words.md](posix/SDL2-words.md).
''')

    # ---- forth ----
    allforth = sorted(names, key=lambda k: (k.strip('#[]{}()<>:;\'"'), k))
    cats = {}
    for key in allforth:
        s, d, kind = describe(key)
        stats[kind if kind != 'ans' else 'ans'] += 1
        sec = ''
        e = site.get(key)
        if e and e['page'] != 'ESP32forth.html' and e['section']:
            sec = e['section']
        elif key in ANS:
            sec = 'Standard words'
        elif key in groups:
            sec = groups[key]
        elif e and e['section']:
            sec = e['section']
        else:
            sec = 'Other'
        cats.setdefault(sec, []).append((names[key], s, d, key))
    total = len(allforth)
    T.append('## The `forth` vocabulary\n')
    T.append('%d words. %d are described by the reference pages, %d by the notes, %d are standard words the project leaves to the standard, %d have only a stack comment or binding, %d have no description yet.\n' %
             (total, stats['site'], stats['note'], stats['ans'], stats['source'], stats['none']))
    for sec in sorted(cats, key=lambda c: (c == 'Other', c == 'Standard words', c)):
        T.append('\n### %s\n' % sec)
        T.append('| Word | Stack | What it does | P W |')
        T.append('|------|-------|--------------|-----|')
        for name, s, d, key in sorted(cats[sec], key=lambda t: t[0].lower()):
            where = ('P' if key in pf else '-') + ' ' + ('W' if key in wf else '-')
            if key in ANS:
                d = 'ANS.' if (not d or d.startswith('Standard word')) else 'ANS. ' + d
            T.append('| `%s` | %s | %s | %s |' % (cell(name), ('`%s`' % cell(s)) if s else '', cell(d), where))

    # ---- other vocabularies ----
    def vocab_section(title, note, words, plat):
        if not words:
            return
        T.append('\n## %s\n' % title)
        if note:
            T.append(note + '\n')
        T.append('| Word | Stack | What it does | %s |' % plat)
        T.append('|------|-------|--------------|---|')
        for w in sorted(words, key=str.lower):
            key = w.lower()
            s, d, kind = describe(key)
            T.append('| `%s` | %s | %s | %s |' % (cell(w), ('`%s`' % cell(s)) if s else '', cell(d), plat))
    notes = {
      'internals': 'Words of the inner workings: the interpreter loop, the dictionary, system variables. Use `internals words`, or put `internals` in the search order with `also internals`.',
      'posix': 'System calls and constants of Linux and macOS, reached with `sysfunc`. `also posix` puts them in the search order.',
    }
    for v in ['internals', 'posix', 'structures', 'streams', 'tasks', 'ansi', 'editor', 'sockets', 'x11', 'graphics',
              'httpd', 'telnetd', 'web-interface', 'termios', 'visual', 'recognizers']:
        vocab_section('The `%s` vocabulary (Linux)' % v, notes.get(v, ''), pown.get(v, []), 'P')
    for v in ['web', 'internals']:
        ws = [w for w in wown.get(v, []) if w.lower() not in set(x.lower() for x in pown.get(v, []))]
        vocab_section('The `%s` vocabulary (web only)' % v, '', ws, 'W')
    open(ROOT + 'WORDS.md', 'w').write('\n'.join(T) + '\n')
    print('wrote WORDS.md:', total, 'forth words;', stats)

ANS_DOC_OK = set()
if __name__ == '__main__':
    main()
