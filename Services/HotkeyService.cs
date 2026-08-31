using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HexaDock.Services;

public sealed class HotkeyService : IDisposable
{
    private const int Id = 0x4844;
    private const int HotkeyMessage = 0x0312;
    private readonly Window _window;
    private readonly Action _action;
    private HwndSource? _source;
    private IntPtr _handle;

    public HotkeyService(Window window, Action action)
    {
        _window = window;
        _action = action;
        window.SourceInitialized += Initialize;
    }

    private void Initialize(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_handle);
        _source.AddHook(ProcessMessage);
        RegisterHotKey(_handle, Id, 0x0001 | 0x0002, 0x20);
    }

    private IntPtr ProcessMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == HotkeyMessage && wParam.ToInt32() == Id)
        {
            _action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero) UnregisterHotKey(_handle, Id);
        _source?.RemoveHook(ProcessMessage);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
