# Phosphor

Matrix-style digital rain for Windows Terminal. Half-width katakana, ASCII digits, and Latin letters fall in green-gradient columns, with ~5% of threads rendered in vivid random RGB colors.

Renders using ANSI true-color escape sequences (`\x1b[38;2;R;G;Bm`) enabled via Win32 `SetConsoleMode`. No dependencies beyond the .NET 8 runtime.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows 10 or Windows 11 (requires Win32 console API + ANSI VT support)
- A terminal that supports 24-bit true color — **Windows Terminal** is recommended

## Build

```
dotnet build
```

## Run

```
dotnet run
```

Or after building:

```
.\bin\Debug\net8.0\Phosphor.exe
```

Press **Escape** or **Q** to quit. **Ctrl+C** also exits cleanly — the cursor and terminal color state are always restored.

## Font requirements

Half-width katakana characters (U+FF65–U+FF9F) require a font that includes CJK glyphs. Without one, katakana positions will show as blank rectangles.

**Windows Terminal:** Settings → Profiles → Defaults → Appearance → Font face → `MS Gothic` (or `NSimSun`, or any CJK-capable monospace font).

Alternatively, add a dedicated profile that sets the font only for Phosphor:

```json
{
    "name": "Phosphor",
    "commandline": "\"C:\\Program Files\\dotnet\\dotnet.exe\" run --project \"C:\\path\\to\\phosphor\"",
    "startingDirectory": "C:\\path\\to\\phosphor",
    "font": { "face": "MS Gothic", "size": 12 }
}
```

## How it works

- One `Column` object per active thread tracks head position, trail length (6–24 cells), fall rate (1–4 ticks/row), and character state
- The screen is populated with `2×width` threads so random X-position assignment leaves few visible gaps
- Each frame builds into a single `StringBuilder` and flushes in one `Console.Write` call to minimize flicker
- Each row begins with an absolute cursor-position escape (`\x1b[row;1H`) to prevent drift if any glyph renders wider than one cell
- ~5% of threads are assigned a vivid accent color via HSV→RGB at initialization; accent color re-randomizes each time a thread restarts
