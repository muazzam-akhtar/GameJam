using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

/// <summary>
/// Hands the connected devices out to the two players and keeps that up to date while controllers come and go
/// (rules in <see cref="InputAssignment"/>). F1 or a controller's Select / Share button swaps the devices.
/// With no controllers it reads every mouse and trackpad separately (<see cref="RawMice"/>, Windows) so each
/// keyboard player can steer their own camera.
/// Ticked lazily by whoever asks first in a frame; runs while at least one <see cref="PlayerInputSource"/> is alive.
/// </summary>
public static class InputRouter
{
    public const Key SwapKey = Key.F1;

    /// <summary>Raised when who-plays-on-what changes.</summary>
    public static event Action Changed;

    /// <summary>Read every mouse / trackpad on its own when nobody has a controller (Windows).</summary>
    public static bool SeparatePointers = true;

    /// <summary>A menu (the result screen) is up: the cursor stays free so its buttons can be clicked.</summary>
    public static bool MenuOpen;

    static readonly int[] padOwner = { -1, -1 };
    static PlayerDevices[] current = InputAssignment.Resolve(false, false, 1, false);
    static readonly Vector2[] pointerDelta = new Vector2[InputAssignment.Players];
    static readonly bool[] pointerLeft = new bool[InputAssignment.Players];
    static readonly bool[] pointerRight = new bool[InputAssignment.Players];
    static readonly List<RawMice.Sample> samples = new List<RawMice.Sample>();
    static readonly List<int> padIds = new List<int>();
    static bool swapPointers, dirty = true, cursorFree;
    static int pointerCount = -1, users, lastFrame = -1;

    public static void Acquire()
    {
        if (users++ > 0) return;
        InputSystem.onDeviceChange += OnDeviceChange;
        dirty = true;
        lastFrame = -1;
    }

    public static void Release()
    {
        if (--users > 0) return;
        users = 0;
        InputSystem.onDeviceChange -= OnDeviceChange;
        RawMice.Stop();
        padOwner[0] = padOwner[1] = -1;
        swapPointers = false;
        pointerCount = -1;
        dirty = true;
        MenuOpen = false;
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    public static PlayerDevices Devices(int player) { Tick(); return current[player]; }

    /// <summary>The controller this player holds, or null.</summary>
    public static Gamepad PadOf(int player)
    {
        Tick();
        int id = padOwner[player];
        if (id < 0) return null;
        foreach (var g in Gamepad.all)
            if (g.deviceId == id) return g;
        return null;
    }

    /// <summary>This frame's mouse / trackpad movement for the player (pixels, y up). Zero while the cursor is free.</summary>
    public static Vector2 PointerDelta(int player) { Tick(); return pointerDelta[player]; }
    public static bool PointerClick(int player) { Tick(); return pointerLeft[player]; }
    public static bool PointerRightClick(int player) { Tick(); return pointerRight[player]; }

    /// <summary>Each keyboard player has their own mouse / trackpad.</summary>
    public static bool SplitPointers { get { Tick(); return RawMice.Active && pointerCount >= 2; } }

    /// <summary>Swap the devices between the players (controllers if any, otherwise the two pointers).</summary>
    public static void Swap()
    {
        if (padOwner[0] >= 0 || padOwner[1] >= 0)
            (padOwner[0], padOwner[1]) = (padOwner[1], padOwner[0]);
        else
            swapPointers = !swapPointers;
        Resolve();
    }

    /// <summary>Restart: the key, or Start / A on any controller.</summary>
    public static bool AnyRestartPressed(Key key)
    {
        var kb = Keyboard.current;
        if (kb != null && key != Key.None && kb[key].wasPressedThisFrame) return true;
        foreach (var g in Gamepad.all)
            if (g.startButton.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    public static void Tick()
    {
        if (users <= 0 || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;

        var kb = Keyboard.current;
        bool swap = kb != null && kb[SwapKey].wasPressedThisFrame;
        foreach (var g in Gamepad.all) swap |= g.selectButton.wasPressedThisFrame;

        if (dirty) Reassign();
        if (swap) Swap();
        ReadPointers();
    }

    static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Gamepad || device is Mouse) dirty = true;
    }

    static void Reassign()
    {
        dirty = false;
        padIds.Clear();
        foreach (var g in Gamepad.all)
            if (g.added) padIds.Add(g.deviceId);
        padIds.Sort();
        InputAssignment.DistributePads(padIds, padOwner);

        bool wantRaw = SeparatePointers && RawMice.Supported && Application.isPlaying && padOwner[0] < 0 && padOwner[1] < 0;
        if (wantRaw && !RawMice.Active) RawMice.Start();
        else if (!wantRaw && RawMice.Active) RawMice.Stop();
        Resolve();
    }

    static void Resolve()
    {
        pointerCount = RawMice.Active ? RawMice.DeviceCount : (Mouse.current != null ? 1 : 0);
        current = InputAssignment.Resolve(padOwner[0] >= 0, padOwner[1] >= 0, pointerCount, swapPointers);
        Changed?.Invoke();
    }

    static void ReadPointers()
    {
        for (int p = 0; p < InputAssignment.Players; p++) { pointerDelta[p] = Vector2.zero; pointerLeft[p] = pointerRight[p] = false; }

        bool raw = RawMice.Active, rawClick = false;
        if (raw)
        {
            RawMice.Drain(samples);
            if (samples.Count != pointerCount) Resolve();     // a new mouse / trackpad just moved
            foreach (var s in samples) rawClick |= s.left || s.right;
        }

        bool anyPointer = current[0].pointer >= 0 || current[1].pointer >= 0;
        bool locked = UpdateCursor(anyPointer, rawClick);

        if (raw)
        {
            if (!locked || !Application.isFocused) return;
            foreach (var s in samples)
            {
                int p = OwnerOf(s.slot);
                if (p < 0) continue;
                pointerDelta[p] += s.delta;
                pointerLeft[p] |= s.left;
                pointerRight[p] |= s.right;
            }
            return;
        }

        var mouse = Mouse.current;
        if (mouse == null || !locked) return;
        int owner = OwnerOf(0);
        if (owner < 0) return;
        pointerDelta[owner] = mouse.delta.ReadValue();
        pointerLeft[owner] = mouse.leftButton.wasPressedThisFrame;
        pointerRight[owner] = mouse.rightButton.wasPressedThisFrame;
    }

    static int OwnerOf(int slot)
    {
        for (int p = 0; p < InputAssignment.Players; p++)
            if (current[p].pointer == slot) return p;
        return -1;
    }

    /// <summary>Hides and locks the cursor while a mouse steers a camera. Esc frees it, a click takes it back.</summary>
    static bool UpdateCursor(bool wanted, bool rawClick)
    {
        if (!Application.isPlaying) return false;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) cursorFree = true;
        bool clicked = rawClick || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
        bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
        if (clicked && Application.isFocused) cursorFree = false;

        bool lockIt = wanted && !MenuOpen && !cursorFree && Application.isFocused;
        if (lockIt != wasLocked)
        {
            Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockIt;
        }
        return lockIt && wasLocked;                           // the click that grabs the cursor isn't a slap
    }

    // ---------------------------------------------------------------- HUD text

    /// <summary>One or two lines telling the player what they're on and which buttons do what.</summary>
    public static string Describe(int player, bool hasAction, string actionName)
    {
        var d = Devices(player);
        var pad = PadOf(player);
        string head, body;
        if (d.pad && pad != null)
        {
            bool ps = pad is DualShockGamepad;
            int number = Array.IndexOf(SortedPadIds(), pad.deviceId) + 1;
            head = $"<b>Controller {number}</b>";
            body = $"<b>L-Stick</b> move  <b>R-Stick</b> look  <b>{(ps ? "Square" : "X")}</b> slap"
                 + (hasAction ? $"  <b>{(ps ? "Cross" : "A")}</b> {actionName}" : "");
        }
        else if (d.keys == KeySet.Both)
        {
            head = "<b>Keyboard + Mouse</b>";
            body = "<b>WASD</b> move  <b>Mouse</b> look  <b>Click</b> slap" + (hasAction ? $"  <b>Right-click</b> {actionName}" : "");
        }
        else
        {
            bool left = d.keys == KeySet.Left;
            string look = d.pointer >= 0 ? (SplitPointers ? $"<b>Mouse {d.pointer + 1}</b>" : "<b>Mouse</b>") : (left ? "<b>Q / R</b>" : "<b>, / .</b>");
            head = d.pointer >= 0 ? $"<b>Keyboard</b> + {(SplitPointers ? $"mouse / trackpad {d.pointer + 1}" : "mouse")}" : "<b>Keyboard</b>";
            body = (left ? "<b>WASD</b>" : "<b>Arrows</b>") + $" move  {look} look  " + (left ? "<b>L-Shift</b>" : "<b>R-Shift</b>") + " slap"
                 + (hasAction ? $"  <b>{(left ? "E" : "R-Ctrl")}</b> {actionName}" : "");
        }
        return $"{head}   <size=85%><color=#FFFFFF77>{SwapKey} swap</color></size>\n{body}";
    }

    static int[] SortedPadIds()
    {
        var ids = new List<int>();
        foreach (var g in Gamepad.all) ids.Add(g.deviceId);
        ids.Sort();
        return ids.ToArray();
    }
}
