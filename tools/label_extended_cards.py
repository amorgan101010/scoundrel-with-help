#!/usr/bin/env python3
"""Add MTG-style title + wrapped rules-text descriptions to the 10 Extended
Rules placeholder card SVGs.

PRD §6 chunk 1 added Blacksmith (diamond face cards), Merchant (heart face
cards), and the two Jokers with deliberately minimal, text-free placeholder
art (see card_assets/{jack,queen,king,ace}_{diamonds,hearts}.svg and
card_assets/joker_{red,black}.svg). A later chunk (7) added a single short
name-banner line to each one. This version replaces that banner with a
richer layout: a bigger title near the top, and a word-wrapped multi-line
rules-text description below it describing the actual mechanic, without
touching the existing background/border/shape art.

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

Word-wrap is a real greedy wrap: each candidate line is measured with
GlyphRenderer.measure and a word is only added to the current line if it
still fits within the available width — unlike the crude single-space
2-way split gen_cards.draw_name_banner/the old version of this script used
for the much shorter 44-card names and banner labels.

Rerunnable: each run strips its own previously-inserted <path> block (marked
by an HTML comment) before re-adding it, so wording/sizes can be changed and
this can be safely re-run any number of times without stacking duplicate
paths or needing to regenerate the whole card.
"""

import os
import re
import sys

_TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
_REPO_ROOT = os.path.dirname(_TOOLS_DIR)
sys.path.insert(0, _TOOLS_DIR)
from gen_cards import GlyphRenderer, W, H  # reuse — do not reimplement text-to-path

# Resolved relative to this checkout (not gen_cards.py's hardcoded absolute
# path, which points at a specific machine's clone) so this script works
# correctly from any worktree/clone as long as the repo layout is intact.
FONT_PATH = os.path.join(_REPO_ROOT, "assets", "fonts", "DejaVuSans-Bold.ttf")
OUT = os.path.join(_REPO_ROOT, "card_assets")

LABEL_START = "<!-- extended-card-label:start -->"
LABEL_END = "<!-- extended-card-label:end -->"

# ── Layout constants ─────────────────────────────────────────────────────────
# The other 44 cards draw a corner pip (rank at size 22, suit symbol at size
# 15) occupying roughly x:9-30, y:7-46 in the top-left corner (gen_cards.py
# draw_pip). These 10 placeholders never call draw_pip — there is no rendered
# corner pip on them — but we still start the title a little below that zone
# so the layout stays consistent with the rest of the deck's conventions.
TITLE_TOP_Y = 12
TITLE_MAX_WIDTH = W - 20          # 10px margin each side
TITLE_CANDIDATE_SIZES = list(range(20, 12, -1))  # try largest that fits first
TITLE_GAP_AFTER = 8

DESC_MAX_WIDTH = W - 24           # 12px margin each side ("reasonable margins")
DESC_CANDIDATE_SIZES = [9, 8, 7, 6, 5, 4]
LINE_HEIGHT_RATIO = 1.4           # matches gen_cards' existing 13px/9px two-line ratio
BOTTOM_LIMIT = H - 8              # stay clear of the outer border (stroke ends ~207)

# A first text-only pass (no backing) was rendered to PNG and visually
# inspected: on every card the shape occupies enough of the card that some
# text lines cross it, and the light pastel text colors (matched to the
# deck's existing palette) have weak contrast against the bright accent-
# colored shapes — worst on joker_black (light grey text on a light grey
# star) and the Aces (largest shapes). A plain dark backing band behind the
# title+description text fixes this without altering any existing art; its
# colors are pulled from each card's own pre-existing name-banner rect so it
# reads as the same visual system, not a foreign addition.
#
# Tradeoff: the panel is opaque (not fill-opacity — ThorVG's SVG opacity
# support is unverified and this project already burned a pass on a ThorVG
# text-rendering mismatch; opaque rects are the same primitive the existing
# banner already uses safely) and, on the cards with the largest shapes
# (the Aces, both Jokers), it covers most of the placeholder diamond/star.
# If the designer would rather keep the shape fully visible and accept
# weaker text contrast, flip this to False — everything else is unchanged.
ADD_PANEL = True
PANEL_MARGIN_X = 6
PANEL_TOP_PAD = 5
PANEL_BOTTOM_PAD = 6
_BANNER_FILL_RE = re.compile(
    r'<rect x="7" y="162" width="135" height="40" fill="(rgb\([^)]+\))"/>'
)
_BANNER_STROKE_RE = re.compile(
    r'<rect x="7" y="162" width="135" height="40" fill="none" '
    r'stroke="(rgb\([^)]+\))" stroke-width="1"/>'
)

# (filename, title, description, fill color). Fill colors are drawn from the
# per-suit "text" palette entries already used for these cards' labels
# (diamonds/hearts), and a matching light tint of each joker's own accent
# color otherwise, so the new text reads as part of the same visual system
# as the rest of the deck.
CARDS = [
    ("jack_diamonds.svg", "BLACKSMITH",
     "Removes 1 slain monster from your weapon. If it has none attached, "
     "grants +1 attack instead.",
     "rgb(255,242,185)"),
    ("queen_diamonds.svg", "BLACKSMITH",
     "Removes 2 slain monsters from your weapon. If it has none attached, "
     "grants +2 attack instead.",
     "rgb(255,242,185)"),
    ("king_diamonds.svg", "BLACKSMITH",
     "Removes 3 slain monsters from your weapon. If it has none attached, "
     "grants +3 attack instead.",
     "rgb(255,242,185)"),
    ("ace_diamonds.svg", "BLACKSMITH",
     "Removes all slain monsters from your weapon. If it has none attached, "
     "grants a one-time +4 attack bonus instead.",
     "rgb(255,242,185)"),
    ("jack_hearts.svg", "MERCHANT",
     "Sells your weapon for HP equal to its value minus attached monsters "
     "(minimum 1).",
     "rgb(255,195,195)"),
    ("queen_hearts.svg", "MERCHANT",
     "Sells your weapon for HP equal to its value minus attached monsters "
     "(minimum 1), plus 1.",
     "rgb(255,195,195)"),
    ("king_hearts.svg", "MERCHANT",
     "Sells your weapon for HP equal to its value minus attached monsters "
     "(minimum 1), plus 3.",
     "rgb(255,195,195)"),
    ("ace_hearts.svg", "MERCHANT",
     "Sells your weapon for its full value plus 5 HP, ignoring attached "
     "monsters.",
     "rgb(255,195,195)"),
    ("joker_red.svg", "RED JOKER",
     "Can store a potion for later use by the player. Can fight monsters "
     "bare-handed. When HP drops to 0, the joker and its carried item are "
     "permanently lost.",
     "rgb(255,190,190)"),
    ("joker_black.svg", "BLACK JOKER",
     "Can store a weapon for later use by the player. Can fight monsters "
     "bare-handed. When HP drops to 0, the joker and its carried item are "
     "permanently lost.",
     "rgb(230,230,235)"),
]


def wrap_text(glyphs, text, size, max_width):
    """Greedy word-wrap: add words to the current line as long as it still
    measures within max_width at the given size, else start a new line.
    Breaks only at word boundaries — never mid-word."""
    words = text.split(' ')
    lines = []
    cur = ''
    for word in words:
        candidate = word if not cur else f'{cur} {word}'
        if not cur or glyphs.measure(candidate, size) <= max_width:
            cur = candidate
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


def fit_title(glyphs, title):
    """Pick the largest candidate size where the title fits on one line
    within TITLE_MAX_WIDTH. Falls back to wrapping at the smallest candidate
    size if even that doesn't fit on one line (not expected for these
    titles, but keeps this robust against future wording changes)."""
    for size in TITLE_CANDIDATE_SIZES:
        if glyphs.measure(title, size) <= TITLE_MAX_WIDTH:
            return size, [title]
    size = TITLE_CANDIDATE_SIZES[-1]
    return size, wrap_text(glyphs, title, size, TITLE_MAX_WIDTH)


def fit_description(glyphs, desc, start_y):
    """Pick the largest candidate description size whose wrapped lines fit
    vertically between start_y and BOTTOM_LIMIT, padding the last line by
    ~1.15x its size so descenders don't clip the card edge. Falls back to
    the smallest candidate if nothing fits cleanly (best effort — still
    fully wrapped, never truncated)."""
    chosen = None
    for size in DESC_CANDIDATE_SIZES:
        lines = wrap_text(glyphs, desc, size, DESC_MAX_WIDTH)
        line_height = size * LINE_HEIGHT_RATIO
        # Bottom of the last line's glyphs, including descender headroom.
        end_y = start_y + (len(lines) - 1) * line_height + size * 1.15
        if end_y <= BOTTOM_LIMIT:
            chosen = (size, lines, line_height)
            break
    if chosen is None:
        size = DESC_CANDIDATE_SIZES[-1]
        lines = wrap_text(glyphs, desc, size, DESC_MAX_WIDTH)
        chosen = (size, lines, size * LINE_HEIGHT_RATIO)
    return chosen


def render_centered_lines(glyphs, lines, top_y, size, line_height, fill):
    elems = []
    for i, line in enumerate(lines):
        lw = glyphs.measure(line, size)
        x = (W - lw) / 2
        y = top_y + i * line_height
        elems.extend(glyphs.render(x, y, line, size, fill))
    return elems


def build_card_block(glyphs, title, desc, fill):
    """Returns (text_elems, panel_top_y, panel_bottom_y) — the panel bounds
    enclose the full title+description block with a little padding."""
    title_size, title_lines = fit_title(glyphs, title)
    title_line_height = title_size * LINE_HEIGHT_RATIO
    elems = render_centered_lines(
        glyphs, title_lines, TITLE_TOP_Y, title_size, title_line_height, fill,
    )

    desc_start_y = (
        TITLE_TOP_Y + len(title_lines) * title_line_height + TITLE_GAP_AFTER
    )
    desc_size, desc_lines, desc_line_height = fit_description(
        glyphs, desc, desc_start_y,
    )
    elems.extend(render_centered_lines(
        glyphs, desc_lines, desc_start_y, desc_size, desc_line_height, fill,
    ))

    desc_bottom = (
        desc_start_y + (len(desc_lines) - 1) * desc_line_height + desc_size * 1.15
    )
    panel_top = TITLE_TOP_Y - PANEL_TOP_PAD
    panel_bottom = min(desc_bottom + PANEL_BOTTOM_PAD, BOTTOM_LIMIT + PANEL_BOTTOM_PAD)
    return elems, panel_top, panel_bottom


def build_panel(svg, panel_top, panel_bottom):
    """A plain rect (+ 1px border) behind the text block, sized to the
    actual text extent, colored from this specific card's own pre-existing
    name-banner rect (fallback to a generic dark tone if not found)."""
    fill_m = _BANNER_FILL_RE.search(svg)
    stroke_m = _BANNER_STROKE_RE.search(svg)
    fill = fill_m.group(1) if fill_m else 'rgb(20,20,20)'
    stroke = stroke_m.group(1) if stroke_m else 'rgb(120,120,120)'
    x0, x1 = PANEL_MARGIN_X, W - PANEL_MARGIN_X
    h = panel_bottom - panel_top
    return [
        f'<rect x="{x0}" y="{panel_top:.2f}" width="{x1 - x0}" height="{h:.2f}" fill="{fill}"/>',
        f'<rect x="{x0}" y="{panel_top:.2f}" width="{x1 - x0}" height="{h:.2f}" '
        f'fill="none" stroke="{stroke}" stroke-width="1"/>',
    ]


def main():
    print("Loading font…")
    glyphs = GlyphRenderer(FONT_PATH)

    for fname, title, desc, fill in CARDS:
        path = os.path.join(OUT, fname)
        with open(path, encoding='utf-8') as f:
            svg = f.read()

        # Idempotent: drop any label block a previous run left behind.
        svg = re.sub(
            re.escape(LABEL_START) + r'.*?' + re.escape(LABEL_END),
            '', svg, flags=re.S,
        )

        text_elems, panel_top, panel_bottom = build_card_block(glyphs, title, desc, fill)
        panel_elems = build_panel(svg, panel_top, panel_bottom) if ADD_PANEL else []
        # Panel first so it paints under the text but over the existing
        # shape/background (SVG paints later elements on top).
        all_elems = panel_elems + text_elems
        block = LABEL_START + '\n' + '\n'.join(all_elems) + '\n' + LABEL_END + '\n'
        svg = svg.replace('</svg>', block + '</svg>')

        with open(path, 'w', encoding='utf-8') as f:
            f.write(svg)
        print(f"  {fname}: \"{title}\" — {len(desc)} chars")

    print(f"\nDone — labeled {len(CARDS)} cards.")


if __name__ == '__main__':
    main()
