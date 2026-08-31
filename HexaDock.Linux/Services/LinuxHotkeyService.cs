using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace HexaDock.Linux.Services;

public sealed class LinuxHotkeyService
{
    private const int KeyPress = 2;
    private const int GrabModeAsync = 1;
    private const uint ControlMask = 1u << 2;
    private const uint Mod1Mask = 1u << 3;
    private const uint LockMask = 1u << 1;
    private const uint Mod2Mask = 1u << 4;
    private readonly Action _pressed;

    public bool IsAvailable { get; }

    public LinuxHotkeyService(Action pressed)
    {
        _pressed = pressed;
        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))) return;
        try
        {
            XInitThreads();
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero) return;
            var root = XDefaultRootWindow(display);
            var keycode = XKeysymToKeycode(display, 0x20);
            var modifiers = ControlMask | Mod1Mask;
            foreach (var extra in new[] { 0u, LockMask, Mod2Mask, LockMask | Mod2Mask })
                XGrabKey(display, keycode, modifiers | extra, root, true, GrabModeAsync, GrabModeAsync);
            XSync(display, false);
            IsAvailable = true;
            var thread = new Thread(() => EventLoop(display)) { IsBackground = true, Name = "HexaDock global hotkey" };
            thread.Start();
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private void EventLoop(IntPtr display)
    {
        var nativeEvent = Marshal.AllocHGlobal(192);
        try
        {
            while (true)
            {
                XNextEvent(display, nativeEvent);
                if (Marshal.ReadInt32(nativeEvent) == KeyPress) Dispatcher.UIThread.Post(_pressed);
            }
        }
        catch { }
        finally { Marshal.FreeHGlobal(nativeEvent); }
    }

    [DllImport("libX11.so.6")] private static extern int XInitThreads();
    [DllImport("libX11.so.6")] private static extern IntPtr XOpenDisplay(IntPtr displayName);
    [DllImport("libX11.so.6")] private static extern UIntPtr XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(IntPtr display, ulong keysym);
    [DllImport("libX11.so.6")] private static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, UIntPtr window, bool ownerEvents, int pointerMode, int keyboardMode);
    [DllImport("libX11.so.6")] private static extern int XSync(IntPtr display, bool discard);
    [DllImport("libX11.so.6")] private static extern int XNextEvent(IntPtr display, IntPtr eventReturn);
}
