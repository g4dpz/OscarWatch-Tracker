using System.Runtime.InteropServices;

namespace OscarWatch.Services;

internal static class SecondInstanceNotifier
{
    private const uint MbOk = 0x00000000;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbSetForeground = 0x00010000;

    public static void Show(string title, string message)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = MessageBox(IntPtr.Zero, message, title, MbOk | MbIconWarning | MbSetForeground);
            return;
        }

        Console.Error.WriteLine($"{title}: {message}");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr windowHandle, string text, string caption, uint type);
}
