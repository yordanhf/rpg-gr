namespace ConsoleProto;

/// Reads a command line key by key so that text printed by the background combat loop doesn't cut the
/// half-typed command in two: the prompt line is erased, the message printed, then prompt + typed text redrawn.
/// Supports typing, backspace and up/down history (no cursor movement inside the line).
/// Falls back to plain Console.ReadLine when input is redirected (piped tests).
internal static class LineEditor
{
    private static readonly object Gate = new();
    private static readonly List<string> History = new();
    private static readonly System.Text.StringBuilder Buffer = new();

    // The prompt is a function, not a fixed string: it carries live values (HP/MP), so every redraw
    // (e.g. a combat message arriving while sitting at the prompt) shows the current numbers, not
    // whatever they were when ReadLine was first called. `_prompt` caches the last-drawn text, needed
    // only to know how many characters to erase before drawing the next one.
    private static Func<string> _promptProvider = () => "";
    private static string _prompt = "";
    private static bool _reading;

    public static string? ReadLine(Func<string> promptProvider)
    {
        if (Console.IsInputRedirected)
        {
            var prompt = promptProvider();
            Console.Write(prompt);
            return Console.ReadLine();
        }

        lock (Gate)
        {
            _promptProvider = promptProvider;
            _prompt = promptProvider();
            Buffer.Clear();
            _reading = true;
            Console.Write(_prompt);
        }

        int historyIndex = History.Count;

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            lock (Gate)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        var line = Buffer.ToString();
                        _reading = false;
                        Buffer.Clear();
                        Console.WriteLine();
                        if (line.Trim().Length > 0 && (History.Count == 0 || History[^1] != line))
                            History.Add(line);
                        return line;

                    case ConsoleKey.Backspace:
                        if (Buffer.Length > 0)
                        {
                            Buffer.Length--;
                            Redraw();
                        }
                        break;

                    case ConsoleKey.UpArrow:
                        if (historyIndex > 0)
                            Replace(History[--historyIndex]);
                        break;

                    case ConsoleKey.DownArrow:
                        if (historyIndex < History.Count)
                        {
                            historyIndex++;
                            Replace(historyIndex == History.Count ? "" : History[historyIndex]);
                        }
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            Buffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar);
                        }
                        break;
                }
            }
        }
    }

    /// Runs a write to the console safely while the user may be mid-typing (called from the combat loop).
    /// This is also the moment a stale HP/MP prompt gets refreshed: whatever changed HP (a hit landing)
    /// is exactly what's being printed here, so re-fetching the prompt text picks up the new numbers.
    public static void Print(Action write)
    {
        lock (Gate)
        {
            if (_reading)
                ClearLine();

            write();

            if (_reading)
            {
                _prompt = _promptProvider();
                Console.Write(_prompt + Buffer);
            }
        }
    }

    private static void Replace(string text)
    {
        ClearLine();
        _prompt = _promptProvider();
        Buffer.Clear().Append(text);
        Console.Write(_prompt + Buffer);
    }

    private static void Redraw()
    {
        ClearLine();
        _prompt = _promptProvider();
        Console.Write(_prompt + Buffer);
    }

    private static void ClearLine()
    {
        // Wide enough to also cover the char just removed by backspace.
        Console.Write("\r" + new string(' ', _prompt.Length + Buffer.Length + 1) + "\r");
    }
}
