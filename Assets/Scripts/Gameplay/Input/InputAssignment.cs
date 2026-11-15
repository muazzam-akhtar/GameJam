using System.Collections.Generic;

/// <summary>Which half of the keyboard a player reads.</summary>
[System.Flags]
public enum KeySet
{
    None = 0,
    /// <summary>WASD, Left Shift slap, E action, Q / R look.</summary>
    Left = 1,
    /// <summary>Arrow keys, Right Shift slap, Right Ctrl action, comma / period look.</summary>
    Right = 2,
    /// <summary>The whole keyboard (the only keyboard player, next to a controller player).</summary>
    Both = Left | Right,
}

/// <summary>The devices one player reads.</summary>
public struct PlayerDevices
{
    /// <summary>The player holds a controller.</summary>
    public bool pad;
    public KeySet keys;
    /// <summary>Which pointing device (mouse / trackpad) steers this player's camera; -1 = none.</summary>
    public int pointer;

    public override string ToString() => $"pad={pad} keys={keys} pointer={pointer}";
}

/// <summary>
/// Pure rules for handing out devices (no Unity input calls, so it can be unit tested):
///  - controller 1 goes to player one, controller 2 to player two, and a player keeps their controller while it
///    stays plugged in (unplugging one never moves the other player's controller);
///  - two controllers: both play on them (the keyboard halves still work as a backup);
///  - one controller: the other player gets the whole keyboard and the mouse;
///  - no controller: player one has WASD, player two the arrows, and each gets their own mouse / trackpad when
///    there are two (otherwise the single mouse goes to player one; swap flips that).
/// </summary>
public static class InputAssignment
{
    public const int Players = 2;

    /// <summary>
    /// Updates <paramref name="owner"/> (device id of the controller each player holds, -1 = none) for the
    /// currently connected controllers, lowest id first. Returns true when anything changed.
    /// </summary>
    public static bool DistributePads(IReadOnlyList<int> connected, int[] owner)
    {
        bool changed = false;
        for (int p = 0; p < owner.Length; p++)
        {
            if (owner[p] < 0 || Contains(connected, owner[p])) continue;
            owner[p] = -1;                                   // unplugged: back to the keyboard
            changed = true;
        }
        foreach (var id in connected)
        {
            if (System.Array.IndexOf(owner, id) >= 0) continue;
            for (int p = 0; p < owner.Length; p++)
            {
                if (owner[p] >= 0) continue;
                owner[p] = id;                               // first free player takes the new controller
                changed = true;
                break;
            }
        }
        return changed;
    }

    /// <summary>Keyboard half and pointer for each player, given who holds a controller.</summary>
    public static PlayerDevices[] Resolve(bool p1Pad, bool p2Pad, int pointers, bool swapPointers)
    {
        var r = new PlayerDevices[Players];
        r[0].pad = p1Pad;
        r[1].pad = p2Pad;
        r[0].pointer = r[1].pointer = -1;

        if (p1Pad && p2Pad)
        {
            r[0].keys = KeySet.Left;
            r[1].keys = KeySet.Right;
        }
        else if (p1Pad || p2Pad)
        {
            int kb = p1Pad ? 1 : 0;
            r[kb].keys = KeySet.Both;
            r[1 - kb].keys = KeySet.None;
            if (pointers > 0) r[kb].pointer = 0;
        }
        else
        {
            r[0].keys = KeySet.Left;
            r[1].keys = KeySet.Right;
            int first = swapPointers ? 1 : 0;
            if (pointers >= 1) r[first].pointer = 0;
            if (pointers >= 2) r[1 - first].pointer = 1;
        }
        return r;
    }

    static bool Contains(IReadOnlyList<int> list, int id)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == id) return true;
        return false;
    }
}
