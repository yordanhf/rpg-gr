using System.Runtime.InteropServices;

namespace ConsoleProto;

/// Legacy conhost.exe doesn't interpret ANSI escape codes unless this mode is turned on explicitly
/// (Windows Terminal already does it by default, but this keeps the RGB colors working either way).
internal static class ConsoleAnsi
{
    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    public static void EnableIfWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var handle = GetStdHandle(StdOutputHandle);
        if (GetConsoleMode(handle, out uint mode))
            SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }
}
