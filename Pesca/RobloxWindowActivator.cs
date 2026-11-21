using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pesca;

internal static class RobloxWindowActivator
{
    public static nint ForegroundWindow => GetForegroundWindow();

    public static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public static void TryShowMenu(nint window)
    {
        if (IsWindow(window))
        {
            if (IsIconic(window))
            {
                ShowWindow(window, 9);
            }

            SetForegroundWindow(window);
        }
    }

    public static Point GetScreenCenter(Screen screen)
    {
        return new Point(screen.Bounds.Left + screen.Bounds.Width / 2,
            screen.Bounds.Top + screen.Bounds.Height / 2);
    }

    public static Point GetClickPoint(nint window, Screen screen)
    {
        var visibleClient = Rectangle.Intersect(screen.Bounds, GetClientBounds(window));
        if (visibleClient.Width <= 0 || visibleClient.Height <= 0)
        {
            throw new InvalidOperationException("La zona de juego no está visible en su pantalla.");
        }

        return new Point(visibleClient.Left + visibleClient.Width / 2,
            visibleClient.Top + visibleClient.Height / 2);
    }

    public static Rectangle GetClientBounds(nint window)
    {
        if (!GetClientRect(window, out var client))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var origin = new Point(client.Left, client.Top);
        if (!ClientToScreen(window, ref origin))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        if (client.Right <= client.Left || client.Bottom <= client.Top)
        {
            throw new InvalidOperationException("La zona de juego no está visible en su pantalla.");
        }

        return new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
    }

    public static void EnsureInputTarget(nint window, Screen screen)
    {
        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
        {
            throw new FishingInterruptedException();
        }

        if (!IsWindow(window) || IsIconic(window) || GetForegroundWindow() != window)
        {
            throw new InvalidOperationException("Roblox ha perdido el foco o su ventana no está disponible.");
        }

        screen = Screen.FromHandle(window);
        if (!IsZoomed(window))
        {
            ShowWindow(window, 3); // Mantiene Roblox maximizado durante la pesca.
            Cursor.Position = GetScreenCenter(screen);
        }

        if (!screen.Bounds.Contains(Cursor.Position))
        {
            Cursor.Position = GetScreenCenter(screen);
        }

        var pointer = Cursor.Position;
        if (GetAncestor(WindowFromPoint(pointer), 2) != window)
        {
            throw new InvalidOperationException("Roblox ha perdido el foco o el cursor ha salido de su ventana.");
        }
    }

    public static nint FindWindow(int processId)
    {
        nint selected = 0;
        long largestArea = -1;

        // Algunas ventanas tienen propietario y no aparecen en Process.MainWindowHandle.
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var windowProcessId);
            if (windowProcessId == (uint)processId && IsWindowVisible(window) &&
                GetWindowRect(window, out var bounds))
            {
                var area = (long)Math.Max(0, bounds.Right - bounds.Left) *
                    Math.Max(0, bounds.Bottom - bounds.Top);
                if (area > largestArea)
                {
                    largestArea = area;
                    selected = window;
                }
            }

            return true;
        }, 0);

        return selected;
    }

    public static (bool OnTargetScreen, bool InForeground, bool Maximized) Activate(nint window, Screen targetScreen)
    {
        if (!IsWindow(window))
        {
            throw new InvalidOperationException("La ventana de Roblox ya no existe.");
        }

        var wasMaximized = IsZoomed(window);
        var needsMove = Screen.FromHandle(window).DeviceName != targetScreen.DeviceName;

        if (IsIconic(window) || (needsMove && wasMaximized))
        {
            ShowWindow(window, 9); // SW_RESTORE
        }

        if (needsMove)
        {
            if (!GetWindowRect(window, out var bounds))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var area = targetScreen.WorkingArea;
            var width = Math.Clamp(bounds.Right - bounds.Left, 1, area.Width);
            var height = Math.Clamp(bounds.Bottom - bounds.Top, 1, area.Height);
            var x = area.Left + (area.Width - width) / 2;
            var y = area.Top + (area.Height - height) / 2;

            // Conserva el orden de ventanas durante el traslado; el foco se solicita después.
            if (!SetWindowPos(window, 0, x, y, width, height, 0x0004 | 0x0010))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (wasMaximized)
            {
                ShowWindow(window, 3); // SW_MAXIMIZE
            }
        }

        ShowWindow(window, 3); // SW_MAXIMIZE, también cuando ya estaba visible.
        SetForegroundWindow(window);

        // Comprueba el resultado real, incluso si Windows rechaza el cambio de foco.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (GetForegroundWindow() == window &&
                Screen.FromHandle(window).DeviceName == targetScreen.DeviceName && !IsIconic(window) && IsZoomed(window))
            {
                return (true, true, true);
            }

            Thread.Sleep(50);
        }

        return (Screen.FromHandle(window).DeviceName == targetScreen.DeviceName,
            GetForegroundWindow() == window && !IsIconic(window), IsZoomed(window));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out WindowRect bounds);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    private delegate bool WindowCallback(nint window, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(WindowCallback callback, nint parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint window, out WindowRect bounds);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint window, ref Point point);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
