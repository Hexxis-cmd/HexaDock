using System.Runtime.InteropServices;

namespace HexaDock.Services;

public readonly record struct MonitorArea(string Name, double Left, double Top, double Right, double Bottom);

public static class MonitorService
{
    public static (double Left, double Top, MonitorArea Monitor) Constrain(double left, double top, double width, double height)
    {
        var monitor = At(left + width / 2, top + height / 2);
        return (
            Math.Clamp(left, monitor.Left, Math.Max(monitor.Left, monitor.Right - width)),
            Math.Clamp(top, monitor.Top, Math.Max(monitor.Top, monitor.Bottom - height)),
            monitor);
    }

    public static MonitorArea At(double x, double y)
    {
        var monitor = MonitorFromPoint(new Point((int)x, (int)y), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
            return new("Primary", 0, 0, System.Windows.SystemParameters.PrimaryScreenWidth, System.Windows.SystemParameters.PrimaryScreenHeight);
        return new(info.Device, info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
    }

    [StructLayout(LayoutKind.Sequential)] private readonly struct Point(int x, int y) { public readonly int X = x; public readonly int Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
