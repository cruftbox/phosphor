// Phosphor — Matrix-style digital rain for Windows terminal
// Types defined below: NativeMethods, CharSet, ColorUtils, Column, Renderer, PhosphorApp

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

[StructLayout(LayoutKind.Sequential)]
internal struct COORD { public short X; public short Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct SMALL_RECT
{
    public short Left; public short Top;
    public short Right; public short Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CONSOLE_SCREEN_BUFFER_INFO
{
    public COORD dwSize;
    public COORD dwCursorPosition;
    public ushort wAttributes;
    public SMALL_RECT srWindow;
    public COORD dwMaximumWindowSize;
}

internal static class NativeMethods
{
    internal const int STD_OUTPUT_HANDLE = -11;
    internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    internal const uint ENABLE_PROCESSED_OUTPUT = 0x0001;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetConsoleScreenBufferInfo(
        IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
}

internal static class CharSet
{
    private static readonly char[] Chars;

    static CharSet()
    {
        var list = new List<char>();
        for (char c = '･'; c <= 'ﾟ'; c++) list.Add(c);  // half-width katakana U+FF65–U+FF9F
        for (char c = '0'; c <= '9'; c++) list.Add(c);
        for (char c = 'A'; c <= 'Z'; c++) list.Add(c);
        Chars = list.ToArray();
    }

    internal static char Pick(Random rng) => Chars[rng.Next(Chars.Length)];
}

internal static class ColorUtils
{
    internal static (byte R, byte G, byte B) HsvToRgb(float h, float s, float v)
    {
        h %= 1.0f;
        int i = (int)(h * 6);
        float f = h * 6 - i;
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);
        float r, g, b;
        switch (i % 6)
        {
            case 0:  r = v; g = t; b = p; break;
            case 1:  r = q; g = v; b = p; break;
            case 2:  r = p; g = v; b = t; break;
            case 3:  r = p; g = q; b = v; break;
            case 4:  r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}

internal sealed class Column
{
    public int  X;
    public int  HeadRow;
    public int  TrailLength;
    public int  FallRate;
    public int  TickCounter;
    public int  DormantCountdown;
    public bool IsActive;
    public bool IsAccent;
    public byte AccentR, AccentG, AccentB;
    public char[] Chars = Array.Empty<char>();

    public void Initialize(Random rng, int screenWidth, int screenHeight)
    {
        X           = rng.Next(screenWidth);
        TrailLength = rng.Next(6, 25);    // 6–24 inclusive
        FallRate    = rng.Next(1, 5);     // 1–4 inclusive
        TickCounter = 0;
        HeadRow     = -TrailLength;
        IsActive    = true;
        IsAccent    = rng.NextDouble() < 0.05;

        if (IsAccent)
            (AccentR, AccentG, AccentB) = ColorUtils.HsvToRgb((float)rng.NextDouble(), 1.0f, 0.9f);

        if (Chars.Length != TrailLength + 1)
            Chars = new char[TrailLength + 1];
        for (int i = 0; i <= TrailLength; i++)
            Chars[i] = CharSet.Pick(rng);
    }

    public void Tick(Random rng, int screenWidth, int screenHeight)
    {
        if (!IsActive)
        {
            if (--DormantCountdown <= 0)
                Initialize(rng, screenWidth, screenHeight);
            return;
        }

        // Head character flickers every tick
        Chars[0] = CharSet.Pick(rng);

        // 8% glitch per trail cell
        for (int i = 1; i <= TrailLength; i++)
            if (rng.NextDouble() < 0.08)
                Chars[i] = CharSet.Pick(rng);

        if (++TickCounter >= FallRate)
        {
            TickCounter = 0;
            HeadRow++;
        }

        // Transition to dormant once entire thread has scrolled off-screen
        if (HeadRow > screenHeight + TrailLength)
        {
            IsActive         = false;
            DormantCountdown = rng.Next(20, 81);  // 20–80 ticks
        }
    }
}

internal sealed class Renderer
{
    private readonly IntPtr _stdOut;
    private readonly StringBuilder _sb = new(1 << 16);

    // Pre-allocated cell grid — resized on demand to avoid per-frame GC pressure
    private char[] _cellChar   = Array.Empty<char>();
    private byte[] _cellR      = Array.Empty<byte>();
    private byte[] _cellG      = Array.Empty<byte>();
    private byte[] _cellB      = Array.Empty<byte>();
    private bool[] _cellActive = Array.Empty<bool>();
    private bool[] _cellErase  = Array.Empty<bool>();
    private int    _cellCap    = 0;

    public Renderer()
    {
        _stdOut = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
    }

    public void EnableVT()
    {
        NativeMethods.GetConsoleMode(_stdOut, out uint mode);
        NativeMethods.SetConsoleMode(_stdOut,
            mode | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING
                 | NativeMethods.ENABLE_PROCESSED_OUTPUT);
        Console.CursorVisible = false;
    }

    public bool TryGetDimensions(out int width, out int height)
    {
        if (!NativeMethods.GetConsoleScreenBufferInfo(_stdOut, out var info))
        {
            width = height = 0;
            return false;
        }
        width  = info.srWindow.Right  - info.srWindow.Left + 1;
        height = info.srWindow.Bottom - info.srWindow.Top  + 1;
        return true;
    }

    public void WriteFrame(Column[] columns, int width, int height)
    {
        int size = width * height;

        // Grow backing arrays if the screen is larger than last seen
        if (size > _cellCap)
        {
            _cellChar   = new char[size];
            _cellR      = new byte[size];
            _cellG      = new byte[size];
            _cellB      = new byte[size];
            _cellActive = new bool[size];
            _cellErase  = new bool[size];
            _cellCap    = size;
        }
        else
        {
            Array.Clear(_cellActive, 0, size);
            Array.Clear(_cellErase,  0, size);
        }

        // Populate cell grid from active column states
        foreach (var col in columns)
        {
            if (!col.IsActive || col.X < 0 || col.X >= width) continue;

            int head = col.HeadRow;

            // Head cell
            if ((uint)head < (uint)height)
            {
                int idx = head * width + col.X;
                _cellChar[idx] = col.Chars[0];
                (_cellR[idx], _cellG[idx], _cellB[idx]) = col.IsAccent
                    ? (col.AccentR, col.AccentG, col.AccentB)
                    : ((byte)220, (byte)255, (byte)220);
                _cellActive[idx] = true;
            }

            // Trail cells
            for (int d = 1; d <= col.TrailLength; d++)
            {
                int row = head - d;
                if ((uint)row >= (uint)height) continue;
                int idx = row * width + col.X;
                _cellChar[idx] = col.Chars[d];
                (_cellR[idx], _cellG[idx], _cellB[idx]) = col.IsAccent
                    ? (col.AccentR, col.AccentG, col.AccentB)
                    : GreenTrailColor(d, col.TrailLength);
                _cellActive[idx] = true;
            }

            // Erase cell — one row past the tail end
            int eraseRow = head - col.TrailLength - 1;
            if ((uint)eraseRow < (uint)height)
                _cellErase[eraseRow * width + col.X] = true;
        }

        // Build output — cursor-home then full grid scan
        _sb.Clear();
        _sb.Append("\x1b[H");

        byte curR = 0, curG = 0, curB = 0;
        bool colorSet = false;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int idx = row * width + col;

                if (_cellActive[idx])
                {
                    byte r = _cellR[idx], g = _cellG[idx], b = _cellB[idx];
                    if (!colorSet || r != curR || g != curG || b != curB)
                    {
                        _sb.Append("\x1b[38;2;");
                        _sb.Append(r); _sb.Append(';');
                        _sb.Append(g); _sb.Append(';');
                        _sb.Append(b); _sb.Append('m');
                        curR = r; curG = g; curB = b;
                        colorSet = true;
                    }
                    _sb.Append(_cellChar[idx]);
                }
                else if (_cellErase[idx])
                {
                    _sb.Append("\x1b[0m ");
                    colorSet = false;
                }
                else
                {
                    if (colorSet) { _sb.Append("\x1b[0m"); colorSet = false; }
                    _sb.Append(' ');
                }
            }
        }

        _sb.Append("\x1b[0m");
        Console.Write(_sb);
    }

    private static (byte r, byte g, byte b) GreenTrailColor(int d, int trailLength)
    {
        if (d <= 3)               return (0, 255, 70);   // bright green
        if (d <= trailLength / 2) return (0, 180, 0);    // standard green
        return                          (0, 80,  0);     // dark green
    }

    public void Cleanup()
    {
        Console.Write("\x1b[0m\x1b[2J\x1b[H");
        Console.CursorVisible = true;
    }
}

internal sealed class PhosphorApp
{
    public void Run()
    {
        var renderer = new Renderer();
        renderer.EnableVT();

        if (!renderer.TryGetDimensions(out int width, out int height) || width <= 0 || height <= 0)
        {
            Console.WriteLine("Cannot read console dimensions. Run this in a terminal window.");
            return;
        }

        var rng = new Random();
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var columns = CreateColumns(rng, width, height);
        var sw = Stopwatch.StartNew();

        try
        {
            while (!cts.IsCancellationRequested)
            {
                sw.Restart();

                // Drain keyboard input
                bool stop = false;
                while (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(intercept: true).Key;
                    if (k == ConsoleKey.Escape || k == ConsoleKey.Q) { stop = true; break; }
                }
                if (stop) break;

                // Resize detection — reallocate columns if dimensions changed
                if (renderer.TryGetDimensions(out int nw, out int nh) && (nw != width || nh != height))
                {
                    width = nw; height = nh;
                    columns = CreateColumns(rng, width, height);
                }

                // Advance all columns
                foreach (var col in columns)
                    col.Tick(rng, width, height);

                // Render frame (catch IOException from resize race)
                try { renderer.WriteFrame(columns, width, height); }
                catch (IOException) { }

                int sleepMs = 50 - (int)sw.ElapsedMilliseconds;
                if (sleepMs > 0) Thread.Sleep(sleepMs);
            }
        }
        finally
        {
            renderer.Cleanup();
        }
    }

    private static Column[] CreateColumns(Random rng, int width, int height)
    {
        var cols = new Column[width];
        for (int i = 0; i < width; i++)
        {
            cols[i] = new Column();
            cols[i].Initialize(rng, width, height);
            // Pre-seed dormant state to stagger first appearances (0–6 seconds at 20 fps)
            cols[i].IsActive         = false;
            cols[i].DormantCountdown = rng.Next(0, 121);
        }
        return cols;
    }
}

new PhosphorApp().Run();
