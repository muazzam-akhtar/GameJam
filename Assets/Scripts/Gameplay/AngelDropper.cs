using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Angel's pouch of offerings: she starts a round carrying <see cref="pouchSize"/> and must hide them all, one by
/// one (E / A / right-click puts one down just behind her). A short cooldown stops spamming, offerings never go
/// through walls, and once down she can't pick them back up.
/// </summary>
[DisallowMultipleComponent]
public class AngelDropper : MonoBehaviour
{
    public Offering offeringPrefab;
    [Tooltip("Parent for dropped offerings (empty = scene root).")]
    public Transform container;
    [SerializeField] Key dropKey = Key.E;
    [Tooltip("Offerings she carries at the start of a round.")]
    public int pouchSize = 8;
    [Tooltip("Seconds between drops.")]
    public float cooldown = 1.2f;
    [Tooltip("How far behind the Angel the offering lands.")]
    [SerializeField] float dropBehind = 10f;

    readonly List<Offering> placed = new List<Offering>();
    float nextDrop = float.NegativeInfinity;
    int carried = -1;

    /// <summary>Offerings still in her pouch.</summary>
    public int Carried => carried < 0 ? pouchSize : carried;

    /// <summary>Offerings she has put down that the Evil hasn't eaten yet.</summary>
    public int PlacedCount
    {
        get { placed.RemoveAll(o => o == null || o.Done); return placed.Count; }
    }

    public float CooldownLeft => Mathf.Max(0f, nextDrop - Time.time);
    public Key DropKey => dropKey;

    /// <summary>Raised for every offering put down.</summary>
    public event Action<Offering> Dropped;

    /// <summary>Fills the pouch again (round start).</summary>
    public void Refill()
    {
        carried = pouchSize;
        placed.Clear();
        nextDrop = float.NegativeInfinity;
    }

    void Update()
    {
        // E on the keyboard, A / LB on a controller, right-click on her mouse (whatever InputRouter gave her)
        if (TryGetComponent<PlayerInputSource>(out var input))
        {
            if (input.ActionPressed) TryDrop(Time.time);
            return;
        }
        var kb = Keyboard.current;
        if (kb != null && dropKey != Key.None && kb[dropKey].wasPressedThisFrame) TryDrop(Time.time);
    }

    /// <summary>Hides an offering behind her if the cooldown is over and she still has one. Returns it, or null.</summary>
    public Offering TryDrop(float now)
    {
        if (now < nextDrop || !offeringPrefab || Carried <= 0) return null;
        float behind = dropBehind;

        var from = transform.position; from.y = 0f;
        var back = -transform.forward; back.y = 0f;
        back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
        var pos = from + back * behind;
        // don't put it inside a wall: stop short of whatever is behind her
        if (Physics.Raycast(from + Vector3.up * 10f, back, out var hit, behind + 4f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<CharacterActionsBase>())
            pos = from + back * Mathf.Max(0f, hit.distance - 6f);

        var o = container ? Instantiate(offeringPrefab, pos, Quaternion.identity, container) : Instantiate(offeringPrefab, pos, Quaternion.identity);
        o.gameObject.SetActive(true);
        placed.Add(o);
        carried = Carried - 1;
        nextDrop = now + cooldown;
        if (TryGetComponent<AngelGlow>(out var glow)) glow.Flare();
        Dropped?.Invoke(o);
        return o;
    }
}
