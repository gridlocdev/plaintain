# Colour themes

Plaintain wears one colour theme in dark mode and another in light mode. Pick them in Preferences (⌘,) under Appearance → Dark theme and Light theme. With Theme set to Follow System, the app switches between the two as macOS switches appearance.

## Built-in themes

| Dark | Light |
| --- | --- |
| Plaintain Dark (default) | Plaintain Light (default) |
| Midnight | Paper |
| Ember | Mist |
| High Contrast Dark | High Contrast Light |
| Forest | |

## Your own themes

Put a `.json` file in the `themes` folder beside Plaintain's `settings.json`. On macOS that is `~/Library/Application Support/Plaintain/themes/`. Preferences reads the folder each time it opens, so a new file shows up the next time you open it. A file that can't be read is left out, and Preferences names it and the reason under Light theme.

```json
{
  // Comments and trailing commas are allowed.
  "name": "Dusk",
  "base": "dark",
  "colors": {
    "background": "#202030",
    "text": "#E8E6F0",
    "accent": "#FF8800",
  }
}
```

- `name` is what Preferences shows. Leave it out and the file name is used.
- `base` is required: `"dark"` or `"light"`. It decides which list the theme appears in, and it fills in any colour the file leaves out from that appearance's default theme.
- `colors` sets any of the colours below. Write each as `#RRGGBB`, or `#AARRGGBB` with the alpha **first** (so `#40FF8800` is orange at a quarter strength). This is the reverse of CSS's `#RRGGBBAA`.

| Colour | What it paints | Default when left out |
| --- | --- | --- |
| `background` | The canvas behind the text | The base theme's |
| `text` | The document's text, and the caret line's number | The base theme's |
| `gutterBackground` | The line-number column | The base theme's |
| `gutterText` | Line numbers other than the caret's | The base theme's |
| `currentLine` | The tint across the caret's row. Keep it faint and translucent. | The base theme's |
| `selection` | The selection. Keep it translucent so the text shows through. Other occurrences of the selected text get the same colour at half its alpha. | `accent` at alpha `40` |
| `caret` | The caret, and the find match you have stepped to | `accent` |
| `accent` | The window's accent: buttons, focus rings and dialogs | The base theme's |
| `findMatch` | Every other find match. Keep it translucent. | The base theme's |

An unknown colour name is ignored, and Preferences lists it. A colour that doesn't parse leaves the whole file out, so a typo can't paint half a theme.
