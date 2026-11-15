#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define RAW_MICE
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Reads every mouse and trackpad on its own through Windows Raw Input, so two keyboard players can each steer a
/// camera with their own pointing device (Unity merges them all into a single Mouse).
///
/// A background thread owns a hidden message-only window and pulls WM_INPUT off its own queue. No managed callback
/// is ever handed to Windows (the window uses the native STATIC window procedure), so a domain reload can't call
/// into freed code. While it runs it holds the process's mouse raw-input registration and hands the previous one
/// back on <see cref="Stop"/>.
///
/// Devices get a slot in the order they first move (or click). Windows only, other platforms report unsupported.
/// </summary>
public static class RawMice
{
    public struct Sample
    {
        public int slot;
        /// <summary>Movement since the last drain, in mouse counts (≈ pixels), y up.</summary>
        public Vector2 delta;
        public bool left, right;
    }

#if RAW_MICE
    public static bool Supported => true;
#else
    public static bool Supported => false;
#endif

    /// <summary>The reader is running and Windows accepted the registration.</summary>
    public static bool Active { get; private set; }

    /// <summary>Pointing devices seen moving so far.</summary>
    public static int DeviceCount { get { lock (gate) return order.Count; } }

    class Acc
    {
        public float dx, dy, travel;
        public bool left, right, hasAbs;
        public int absX, absY;
        public int slot = -1;
    }

    static readonly object gate = new object();
    static readonly Dictionary<IntPtr, Acc> devices = new Dictionary<IntPtr, Acc>();
    static readonly List<Acc> order = new List<Acc>();

    /// <summary>Hands out what every device did since the last call, in slot order.</summary>
    public static void Drain(List<Sample> into)
    {
        into.Clear();
        lock (gate)
        {
            foreach (var a in order)
                into.Add(new Sample { slot = a.slot, delta = new Vector2(a.dx, -a.dy), left = a.left, right = a.right });
            foreach (var a in devices.Values) { a.dx = a.dy = 0f; a.left = a.right = false; }
        }
    }

#if RAW_MICE
    static Thread thread;
    static volatile bool running;
    static ManualResetEvent ready;
    static IntPtr window;
    static RAWINPUTDEVICE? previous;

    /// <summary>Starts listening. Returns false (and stays stopped) if Windows refused.</summary>
    public static bool Start()
    {
        if (thread != null) return Active;
        lock (gate) { devices.Clear(); order.Clear(); }
        previous = null;
        running = true;
        ready = new ManualResetEvent(false);
        thread = new Thread(Run) { IsBackground = true, Name = "GameJam RawMice" };
        thread.Start();
        ready.WaitOne(1000);
        if (!Active) Stop();
        return Active;
    }

    /// <summary>Stops listening and gives the mouse registration back to whoever had it.</summary>
    public static void Stop()
    {
        if (thread == null) return;
        running = false;
        thread.Join(1000);
        thread = null;
        if (Active)
        {
            var size = (uint)Marshal.SizeOf<RAWINPUTDEVICE>();
            var back = previous ?? new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_REMOVE, hwndTarget = IntPtr.Zero };
            if (!RegisterRawInputDevices(new[] { back }, 1, size) && previous.HasValue)
                RegisterRawInputDevices(new[] { new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_REMOVE } }, 1, size);
        }
        Active = false;
        previous = null;
    }

    static void Run()
    {
        window = CreateWindowExW(0, "STATIC", "GameJam RawMice", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window != IntPtr.Zero)
        {
            previous = FindMouseRegistration();
            Active = Register(window);
        }
        ready.Set();
        if (!Active)
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
            window = IntPtr.Zero;
            return;
        }

        IntPtr buffer = IntPtr.Zero;
        uint capacity = 0;
        int ticks = 0;
        try
        {
            while (running)
            {
                MsgWaitForMultipleObjects(0, IntPtr.Zero, false, 10, QS_ALLINPUT);
                while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    if (msg.message == WM_INPUT) Read(msg.lParam, ref buffer, ref capacity);
                    DispatchMessageW(ref msg);                // DefWindowProc cleans the input up
                }
                if (++ticks % 50 == 0) KeepRegistered();       // ~2x a second: take it back if someone re-registered
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            DestroyWindow(window);
            window = IntPtr.Zero;
        }
    }

    static bool Register(IntPtr hwnd)
    {
        var rid = new RAWINPUTDEVICE { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_INPUTSINK, hwndTarget = hwnd };
        return RegisterRawInputDevices(new[] { rid }, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
    }

    static void KeepRegistered()
    {
        var now = FindMouseRegistration();
        if (now.HasValue && now.Value.hwndTarget == window) return;
        if (now.HasValue) previous = now;
        Register(window);
    }

    static RAWINPUTDEVICE? FindMouseRegistration()
    {
        uint size = (uint)Marshal.SizeOf<RAWINPUTDEVICE>(), count = 0;
        GetRegisteredRawInputDevices(null, ref count, size);
        if (count == 0) return null;
        var list = new RAWINPUTDEVICE[count];
        uint got = GetRegisteredRawInputDevices(list, ref count, size);
        if (got == uint.MaxValue) return null;
        for (int i = 0; i < got; i++)
            if (list[i].usUsagePage == 1 && list[i].usUsage == 2) return list[i];
        return null;
    }

    static void Read(IntPtr handle, ref IntPtr buffer, ref uint capacity)
    {
        uint header = (uint)Marshal.SizeOf<RAWINPUTHEADER>(), size = 0;
        GetRawInputData(handle, RID_INPUT, IntPtr.Zero, ref size, header);
        if (size == 0) return;
        if (size > capacity)
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            capacity = Math.Max(size, 64u);
            buffer = Marshal.AllocHGlobal((int)capacity);
        }
        if (GetRawInputData(handle, RID_INPUT, buffer, ref size, header) != size) return;
        if (Marshal.ReadInt32(buffer, 0) != RIM_TYPEMOUSE) return;

        var device = Marshal.ReadIntPtr(buffer, 8);           // RAWINPUTHEADER.hDevice (offset 8 on x86 and x64)
        int m = (int)header;                                  // RAWMOUSE follows the header
        int flags = (ushort)Marshal.ReadInt16(buffer, m);
        int buttons = (ushort)Marshal.ReadInt16(buffer, m + 4);
        int x = Marshal.ReadInt32(buffer, m + 12);
        int y = Marshal.ReadInt32(buffer, m + 16);

        int w = 0, h = 0;
        if ((flags & MOUSE_MOVE_ABSOLUTE) != 0)
        {
            bool virtualDesk = (flags & MOUSE_VIRTUAL_DESKTOP) != 0;
            w = GetSystemMetrics(virtualDesk ? SM_CXVIRTUALSCREEN : SM_CXSCREEN);
            h = GetSystemMetrics(virtualDesk ? SM_CYVIRTUALSCREEN : SM_CYSCREEN);
        }

        lock (gate)
        {
            if (!devices.TryGetValue(device, out var a)) devices[device] = a = new Acc();
            float dx, dy;
            if ((flags & MOUSE_MOVE_ABSOLUTE) != 0)
            {
                // pens, remote desktops and some trackpads report positions (0..65535) instead of movement
                dx = a.hasAbs ? (x - a.absX) * w / 65535f : 0f;
                dy = a.hasAbs ? (y - a.absY) * h / 65535f : 0f;
                a.absX = x; a.absY = y; a.hasAbs = true;
            }
            else { dx = x; dy = y; }
            a.dx += dx;
            a.dy += dy;
            if ((buttons & RI_MOUSE_LEFT_BUTTON_DOWN) != 0) a.left = true;
            if ((buttons & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0) a.right = true;
            if (a.slot < 0)
            {
                a.travel += Math.Abs(dx) + Math.Abs(dy);
                if (a.travel > 4f || a.left || a.right) { a.slot = order.Count; order.Add(a); }
            }
        }
    }

    static RawMice()
    {
        Application.quitting -= Stop;
        Application.quitting += Stop;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Stop;
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayMode;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
#endif
    }

#if UNITY_EDITOR
    static void OnPlayMode(UnityEditor.PlayModeStateChange s)
    {
        if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) Stop();
    }
#endif

    // ---------------------------------------------------------------- Win32

    const uint RIDEV_REMOVE = 0x00000001, RIDEV_INPUTSINK = 0x00000100;
    const uint RID_INPUT = 0x10000003;
    const int RIM_TYPEMOUSE = 0;
    const int MOUSE_MOVE_ABSOLUTE = 0x01, MOUSE_VIRTUAL_DESKTOP = 0x02;
    const int RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001, RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
    const uint WM_INPUT = 0x00FF, PM_REMOVE = 0x0001, QS_ALLINPUT = 0x04FF;
    const int SM_CXSCREEN = 0, SM_CYSCREEN = 1, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX, ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetRegisteredRawInputDevices([Out] RAWINPUTDEVICE[] devices, ref uint count, uint size);

    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern bool PeekMessageW(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    static extern IntPtr DispatchMessageW(ref MSG msg);

    [DllImport("user32.dll")]
    static extern uint MsgWaitForMultipleObjects(uint count, IntPtr handles, bool waitAll, uint milliseconds, uint wakeMask);

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);
#else
    public static bool Start() => false;
    public static void Stop() { }
#endif
}
