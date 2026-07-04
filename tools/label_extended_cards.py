#!/usr/bin/env python3
"""Add legible text labels to the 10 Extended Rules placeholder card SVGs.

PRD §6 chunk 1 added Blacksmith (diamond face cards), Merchant (heart face
cards), and the two Jokers with deliberately minimal, text-free placeholder
art (see card_assets/{jack,queen,king,ace}_{diamonds,hearts}.svg and
card_assets/joker_{red,black}.svg). This script adds a name-banner label to
each one, identifying what it does, without touching the existing
background/shape/color art.

Requires fonttools:  python3 -m pip install fonttools
(If your system Python is "externally managed" — e.g. Arch Linux — pip
install into it will fail. Use a venv instead:
    python3 -m venv /tmp/fonttools-venv
    /tmp/fonttools-venv/bin/pip install fonttools
    /tmp/fonttools-venv/bin/python3 tools/label_extended_cards.py
)

IMPORTANT — ThorVG (Godot's SVG renderer) cannot render <text> elements or
@font-face references; they are invisible in-game regardless of how they're
specified. The only safe way to put readable text on these cards is to
convert each glyph to an SVG <path> outline at generation time. This script
reuses GlyphRenderer from gen_cards.py (the same class/font the other 44
cards' name banners use) rather than reimplementing text-to-path conversion.

Rerunnable: each run strips its own previously-inserted <path> block (marked
by an HTML comment) before re-adding it, so wording/colors can be changed
and this can be safely re-run any number of times without stacking
duplicate paths or needing to regenerate the whole card.
"""

import os
import re
import sys

_TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
_REPO_ROOT = os.path.dirname(_TOOLS_DIR)
sys.path.insert(0, _TOOLS_DIR)
from gen_cards import GlyphRenderer, W  # reuse — do not reimplement text-to-path

# Resolved relative to this checkout (not gen_cards.py's hardcoded absolute
# path, which points at a specific machine's clone) so this script works
# correctly from any worktree/clone as long as the repo layout is intact.
FONT_PATH = os.path.join(_REPO_ROOT, "assets", "fonts", "DejaVuSans-Bold.ttf")
OUT = os.path.join(_REPO_ROOT, "card_assets")

# Matches fonts[3] (9px) in gen_cards.load_fonts() — the same size used for
# the name banner on all other 44 cards, for typographic consistency.
FONT_SIZE = 9

# (filename, label, fill color). Fill colors are drawn from gen_cards.P's
# per-suit "text" palette entries where a direct match exists (diamonds,
# hearts), and a matching light tint of each joker's own accent color
# otherwise, so the labels read as part of the same visual system as the
# rest of the deck rather than a mismatched addition.
LABELS = [
    ("jack_diamonds.svg",  "BLACKSMITH J",  "rgb(255,242,185)"),
    ("queen_diamonds.svg", "BLACKSMITH Q",  "rgb(255,242,185)"),
    ("king_diamonds.svg",  "BLACKSMITH K",  "rgb(255,242,185)"),
    ("ace_diamonds.svg",   "BLACKSMITH A",  "rgb(255,242,185)"),
    ("jack_hearts.svg",    "MERCHANT J",    "rgb(255,195,195)"),
    ("queen_hearts.svg",   "MERCHANT Q",    "rgb(255,195,195)"),
    ("king_hearts.svg",    "MERCHANT K",    "rgb(255,195,195)"),
    ("ace_hearts.svg",     "MERCHANT A",    "rgb(255,195,195)"),
    ("joker_red.svg",      "POTION POCKET", "rgb(255,190,190)"),
    ("joker_black.svg",    "WEAPON POCKET", "rgb(230,230,235)"),
]

LABEL_START = "<!-- extended-card-label:start -->"
LABEL_END = "<!-- extended-card-label:end -->"


def render_banner_label(glyphs, label, fill):
    """Place glyph-path text inside the name-banner rect these placeholders
    already have at [7,162]-[142,202] (135x40), mirroring the centering/
    line-split logic of gen_cards.draw_name_banner. Only emits the <path>
    glyph outlines — the banner background rect is already baked into each
    placeholder SVG and is left untouched."""
    tw = glyphs.measure(label, FONT_SIZE)
    elems = []
    if tw > W - 18 and ' ' in label:
        parts = label.split(' ', 1)
        for i, part in enumerate(parts):
            pw = glyphs.measure(part, FONT_SIZE)
            x = (W - pw) / 2
            y = 165 + i * 13
            elems.extend(glyphs.render(x, y, part, FONT_SIZE, fill))
    else:
        x = (W - tw) / 2
        y = 172
        elems.extend(glyphs.render(x, y, label, FONT_SIZE, fill))
    return elems


def main():
    print("Loading font…")
    glyphs = GlyphRenderer(FONT_PATH)

    for fname, label, fill in LABELS:
        path = os.path.join(OUT, fname)
        with open(path, encoding='utf-8') as f:
            svg = f.read()

        # Idempotent: drop any label block a previous run left behind.
        svg = re.sub(
            re.escape(LABEL_START) + r'.*?' + re.escape(LABEL_END),
            '', svg, flags=re.S,
        )

        elems = render_banner_label(glyphs, label, fill)
        block = LABEL_START + '\n' + '\n'.join(elems) + '\n' + LABEL_END + '\n'
        svg = svg.replace('</svg>', block + '</svg>')

        with open(path, 'w', encoding='utf-8') as f:
            f.write(svg)
        print(f"  {fname}: \"{label}\"")

    print(f"\nDone — labeled {len(LABELS)} cards.")


if __name__ == '__main__':
    main()
