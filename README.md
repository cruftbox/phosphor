# Phosphor

Matrix-style digital rain for Windows Terminal. Half-width katakana, ASCII digits, and Latin letters fall in green-gradient columns with ~5% of threads rendered in vivid random RGB colors.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows 10 or Windows 11 (requires Win32 console API + ANSI VT support)
- A terminal that supports 24-bit true color — **Windows Terminal** is recommended

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run
```

Or after building:

```bash
.\bin\Debug\net8.0\Phosphor.exe
```

## Exit

Press **Escape** or **Q** to quit. **Ctrl+C** also exits cleanly — the cursor and terminal color state are always restored on exit.

## Font requirements

Half-width katakana characters (U+FF65–U+FF9F) require a font that includes CJK glyphs. If you see blank rectangles or boxes instead of Japanese characters:

1. Open **Windows Terminal → Settings → Profiles → Defaults → Appearance**
2. Set **Font face** to `MS Gothic`, `NSimSun`, or another CJK-capable monospace font
3. Save, restart the terminal, and run the app again
