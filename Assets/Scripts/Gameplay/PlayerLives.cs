using System;
using UnityEngine;

/// <summary>
/// A player's lives: 3 at the start of a round, one lost per slap. After a hit there is a short grace period so
/// one slap can never cost two lives.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLives : MonoBehaviour
{
    [SerializeField] int maxLives = 3;
    [Tooltip("Seconds after a hit during which another hit doesn't count.")]
    [SerializeField] float graceTime = 1.5f;

    int lives = -1;
    float lastHit = float.NegativeInfinity;

    public int MaxLives => maxLives;
    public int Lives => lives < 0 ? maxLives : lives;
    public bool IsAlive => Lives > 0;
    /// <summary>Hits don't cost lives (e.g. the frozen Evil during the Angel's head start).</summary>
    public bool Invulnerable { get; set; }

    /// <summary>Raised whenever the number of lives changes (also on reset).</summary>
    public event Action<PlayerLives> Changed;

    void Awake()
    {
        if (lives < 0) lives = maxLives;
    }

    public void ResetLives()
    {
        lives = maxLives;
        lastHit = float.NegativeInfinity;
        Changed?.Invoke(this);
    }

    public bool LoseLife() => LoseLife(Time.time);

    /// <summary>Takes a life unless the player is out of lives or still in the grace period. Returns true if one was lost.</summary>
    public bool LoseLife(float now)
    {
        if (Invulnerable || !IsAlive || now - lastHit < graceTime) return false;
        lastHit = now;
        lives = Lives - 1;
        Changed?.Invoke(this);
        return true;
    }
}
