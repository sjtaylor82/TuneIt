using System.Reflection;
using System.Runtime.InteropServices;

namespace TuneIt;

internal static class ScreenReaderOutput
{
    private static object? jaws;

    internal static bool TryAnnounce(string message)
    {
        // This mirrors Accessible Output 2's JAWS backend: connect to the
        // installed Freedom Scientific COM API and call SayString directly.
        if (FindWindow("JFWUI2", "JAWS") == IntPtr.Zero) return false;
        try
        {
            jaws ??= Activator.CreateInstance(Type.GetTypeFromProgID("FreedomSci.JawsApi")!);
            if (jaws is null) return false;
            jaws.GetType().InvokeMember(
                "SayString",
                BindingFlags.InvokeMethod,
                binder: null,
                target: jaws,
                args: ["      " + message, true]);
            return true;
        }
        catch
        {
            jaws = null;
            return false;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);
}
