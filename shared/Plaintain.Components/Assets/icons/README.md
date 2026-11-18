# Icons

These SVG glyphs are vendored from [Lucide](https://lucide.dev) (ISC/MIT licensed,
© Lucide Contributors), kept local per Golden Rule #6 — icons are never fetched at runtime.

File names are stable internal keys (e.g. `align-left.svg`); a few correspond to a renamed
Lucide source glyph (`align-left` ← `text-align-start`, `filter` ← `funnel`). They are loaded and
stroked by `Controls/Icons/LucideIcons.cs`. To add one, drop the 24×24 `fill="none"` SVG here and
reference its file name as a command's `Icon` key in `AppCommands`.

`move-decimal-left` / `move-decimal-right` / `markdown` / `sparkles` are first-party glyphs drawn in
the same 24×24, `fill="none"`, stroke-2 Lucide style (Lucide has neither a decimal-shift nor a
Markdown icon). The first two show a decimal point shifting left or right; `markdown` is a file glyph
that tells `.md` files apart from plain text at a glance; `sparkles` is a wand and two stars, for
clean-up commands.

A glyph whose outline is a single closed path (e.g. `star`) can also be drawn solid, via
`LucideIcon.IsFilled` — a second, non-colour channel for a two-state control.
