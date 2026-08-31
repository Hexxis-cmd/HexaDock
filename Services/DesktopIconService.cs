using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace HexaDock.Services;

public sealed class DesktopIconService
{
    private bool? _originalVisibility;
    private bool _watchdogStarted;
    public static string RecoveryMarker => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HexaDock", "desktop-icons.hidden");

    public bool Apply(bool hide)
    {
        var view = FindDesktopView();
        if (view == IntPtr.Zero) return false;
        _originalVisibility ??= IsWindowVisible(view);
        ShowWindow(view, hide ? 0 : 5);
        if (hide)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecoveryMarker)!);
            File.WriteAllText(RecoveryMarker, Environment.ProcessId.ToString());
            StartWatchdog();
        }
        else if (File.Exists(RecoveryMarker)) File.Delete(RecoveryMarker);
        return true;
    }

    public void Restore()
    {
        if (_originalVisibility is not bool visible) return;
        var view = FindDesktopView();
        if (view != IntPtr.Zero) ShowWindow(view, visible ? 5 : 0);
        if (File.Exists(RecoveryMarker)) File.Delete(RecoveryMarker);
    }

    public static void Watch(int processId)
    {
        try { Process.GetProcessById(processId).WaitForExit(); } catch { }
        if (!File.Exists(RecoveryMarker)) return;
        SetVisible(true);
        try { File.Delete(RecoveryMarker); } catch { }
    }

    public static bool SetVisible(bool visible)
    {
        var view = FindDesktopView();
        if (view == IntPtr.Zero) return false;
        ShowWindow(view, visible ? 5 : 0);
        return true;
    }

    private void StartWatchdog()
    {
        if (_watchdogStarted || Environment.ProcessPath is null) return;
        _watchdogStarted = true;
        var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--watchdog");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(start);
    }

    private static IntPtr FindDesktopView()
    {
        IntPtr result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            var topClass = ClassName(window);
            if (topClass is not ("Progman" or "WorkerW")) return true;
            EnumChildWindows(window, (child, _) =>
            {
                if (ClassName(child) != "SysListView32") return true;
                var parent = GetParent(child);
                while (parent != IntPtr.Zero && parent != window)
                {
                    if (ClassName(parent) == "SHELLDLL_DefView")
                    {
                        result = child;
                        return false;
                    }
                    parent = GetParent(parent);
                }
                return true;
            }, IntPtr.Zero);
            return result == IntPtr.Zero;
        }, IntPtr.Zero);
        return result;
    }

    private static string ClassName(IntPtr window)
    {
        var value = new StringBuilder(128);
        GetClassName(window, value, value.Capacity);
        return value.ToString();
    }

    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, WindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximum);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);
}
