# Phosphor

While walking the dog I wondered how hard it would be to recreate the Matrix digital rain, a visual that's lived rent-free in my head since 1999.

Turns out it was pretty easy for Claude.

Matrix-style digital rain for Windows Terminal. Half-width katakana, ASCII digits, and Latin letters fall in green-gradient columns, with ~5% of threads rendered in vivid random RGB colors.

Renders using ANSI true-color escape sequences (`\x1b[38;2;R;G;Bm`) enabled via Win32 `SetConsoleMode`. No dependencies beyond the .NET 8 runtime.

<p align="center"><img src="phosphor-animated-demo.png" alt="Phosphor demo" /></p>

## Download

Grab the latest `Phosphor.exe` from the [Releases](https://github.com/cruftbox/phosphor/releases) page. It is self-contained — no .NET installation required.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run
```

To produce a self-contained single-file executable:

```
dotnet restore -r win-x64
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true --no-restore
```

Output: `bin\Release\net8.0\win-x64\publish\Phosphor.exe`

## Requirements

- Windows 10 or Windows 11, **x64** (the release binary targets `win-x64`; ARM Windows is not currently supported)
- A terminal that supports 24-bit true color — **Windows Terminal** is recommended

## Exit

Press **Escape** or **Q** to quit. **Ctrl+C** also exits cleanly — the cursor and terminal color state are always restored.

## Font requirements

Half-width katakana characters (U+FF65–U+FF9F) require a font that includes CJK glyphs. Without one, katakana positions will show as blank rectangles.

**Windows Terminal:** Settings → Profiles → Defaults → Appearance → Font face → `MS Gothic` (or `NSimSun`, or any CJK-capable monospace font).

To set the font only for Phosphor, add a dedicated profile in Windows Terminal settings:

```json
{
    "name": "Phosphor",
    "commandline": "C:\\path\\to\\Phosphor.exe",
    "font": { "face": "MS Gothic", "size": 12 }
}
```

## License

MIT — see [LICENSE](LICENSE).

## How it works

- One `Column` object per active thread tracks head position, trail length (6–24 cells), fall rate (1–4 ticks/row), and character state
- The screen is populated with `2×width` threads so random X-position assignment leaves few visible gaps
- Each frame builds into a single `StringBuilder` and flushes in one `Console.Write` call to minimize flicker
- Each row begins with an absolute cursor-position escape (`\x1b[row;1H`) to prevent drift if any glyph renders wider than one cell
- ~5% of threads are assigned a vivid accent color via HSV→RGB at initialization; accent color re-randomizes each time a thread restarts
