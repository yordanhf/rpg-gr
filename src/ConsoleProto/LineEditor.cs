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
    private static string _prompt = "";
    private static bool _reading;

    public static string? ReadLine(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        lock (Gate)
        {
            _prompt = prompt;
            Buffer.Clear();
            _reading = true;
            Console.Write(prompt);
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
    public static void Print(Action write)
    {
        lock (Gate)
        {
            if (_reading)
                ClearLine();

            write();

            if (_reading)
                Console.Write(_prompt + Buffer);
        }
    }

    private static void Replace(string text)
    {
        ClearLine();
        Buffer.Clear().Append(text);
        Console.Write(_prompt + Buffer);
    }

    private static void Redraw()
    {
        ClearLine();
        Console.Write(_prompt + Buffer);
    }

    private static void ClearLine()
    {
        // Wide enough to also cover the char just removed by backspace.
        Console.Write("\r" + new string(' ', _prompt.Length + Buffer.Length + 1) + "\r");
    }
}
