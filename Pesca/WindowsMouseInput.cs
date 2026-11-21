using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pesca;

internal sealed class WindowsMouseInput(nint window, Screen screen) : IMouseInput
{
    public void EnsureTarget() => RobloxWindowActivator.EnsureInputTarget(window, screen);

    public void LeftDown()
    {
        EnsureTarget();
        SendButton(0x0002); // MOUSEEVENTF_LEFTDOWN
    }

    public void LeftUp() => SendButton(0x0004); // MOUSEEVENTF_LEFTUP

    private static void SendButton(uint flags)
    {
        var input = new NativeInput { Type = 0, Mouse = new MouseInput { Flags = flags } };
        if (SendInput(1, [input], Marshal.SizeOf<NativeInput>()) != 1)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no ha aceptado la pulsación del ratón.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
}
