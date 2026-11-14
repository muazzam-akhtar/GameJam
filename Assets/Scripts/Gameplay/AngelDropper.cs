using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Angel's job: press E to put an offering down just behind her. A short cooldown and a cap on how many can be
/// on the field keep it from being spammed. Offerings never go through walls.
/// </summary>
[DisallowMultipleComponent]
public class AngelDropper : MonoBehaviour
{
    public Offering offeringPrefab;
    [Tooltip("Parent for dropped offerings (empty = scene root).")]
    public Transform container;
    [SerializeField] Key dropKey = Key.E;
    [Tooltip("Max offerings on the field at once.")]
    public int maxActive = 8;
    [Tooltip("Seconds between drops.")]
    public float cooldown = 1.2f;
    [Tooltip("How far behind the Angel the offering lands.")]
    [SerializeField] float dropBehind = 10f;

    readonly List<Offering> active = new List<Offering>();
    float nextDrop = float.NegativeInfinity;

    public int ActiveCount
    {
        get { active.RemoveAll(o => o == null || o.Done); return active.Count; }
    }

    public float CooldownLeft => Mathf.Max(0f, nextDrop - Time.time);
    public Key DropKey => dropKey;

    /// <summary>Raised for every offering put down.</summary>
    public event Action<Offering> Dropped;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && dropKey != Key.None && kb[dropKey].wasPressedThisFrame) TryDrop(Time.time);
    }

    /// <summary>Puts an offering down if the cooldown is over and the cap isn't reached. Returns it, or null.</summary>
    public Offering TryDrop(float now)
    {
        if (!offeringPrefab || now < nextDrop || ActiveCount >= maxActive) return null;

        var from = transform.position; from.y = 0f;
        var back = -transform.forward; back.y = 0f;
        back = back.sqrMagnitude > 1e-4f ? back.normalized : Vector3.back;
        var pos = from + back * dropBehind;
        // don't put it inside a wall: stop short of whatever is behind her
        if (Physics.Raycast(from + Vector3.up * 10f, back, out var hit, dropBehind + 4f, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<CharacterActionsBase>())
            pos = from + back * Mathf.Max(0f, hit.distance - 6f);

        var o = container ? Instantiate(offeringPrefab, pos, Quaternion.identity, container) : Instantiate(offeringPrefab, pos, Quaternion.identity);
        o.gameObject.SetActive(true);
        active.Add(o);
        nextDrop = now + cooldown;
        var glow = GetComponent<AngelGlow>();
        if (glow) glow.Flare();
        Dropped?.Invoke(o);
        return o;
    }
}
