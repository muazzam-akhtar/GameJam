using System;
using UnityEngine;

/// <summary>
/// Single player: a Reaper standing guard. It waits at its post, chases the Keeper who comes within
/// <see cref="sight"/>, slaps when in <see cref="reach"/>, and gives up and floats back once the Keeper gets away or it
/// has strayed <see cref="leash"/> from its post. A slap from the Keeper stuns it for <see cref="stunTime"/> seconds.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GuardAI : MonoBehaviour
{
    public Transform target;
    public float sight = 110f;
    public float leash = 170f;
    [Tooltip("The Keeper flies at 50: slower, so she can always get away.")]
    public float chaseSpeed = 40f;
    public float returnSpeed = 26f;
    public float reach = 20f;
    public float slapCooldown = 2.2f;
    public float stunTime = 2f;
    public float turnSpeed = 540f;

    /// <summary>The guard slapped the Keeper (a life was lost).</summary>
    public event Action<GuardAI> HitKeeper;
    /// <summary>The Keeper slapped the guard.</summary>
    public event Action<GuardAI> Stunned;

    public bool Chasing { get; private set; }
    public Vector3 Home { get; private set; }

    Rigidbody body;
    Quaternion homeRot;
    ICharacterActions actions;
    PlayerLives lives, targetLives;
    ICharacterActions targetActions;
    float nextSlap, stunnedUntil;
    int livesSeen;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        actions = GetComponent<ICharacterActions>();
        lives = GetComponent<PlayerLives>();
        if (lives) lives.Changed += OnSlapped;
    }

    void OnDestroy()
    {
        if (lives) lives.Changed -= OnSlapped;
    }

    /// <summary>Stands the guard at its post, facing <paramref name="facing"/>, and sets who it guards against.</summary>
    public void Post(Vector3 at, Vector3 facing, Transform keeper)
    {
        Home = new Vector3(at.x, 0f, at.z);
        facing.y = 0f;
        homeRot = Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.forward);
        target = keeper;
        targetLives = keeper ? keeper.GetComponent<PlayerLives>() : null;
        targetActions = keeper ? keeper.GetComponent<ICharacterActions>() : null;
        body.position = Home;
        body.rotation = homeRot;
        transform.SetPositionAndRotation(Home, homeRot);
        livesSeen = lives ? lives.Lives : 0;
    }

    void OnSlapped(PlayerLives l)
    {
        if (l.Lives < livesSeen)
        {
            stunnedUntil = Time.time + stunTime;
            Chasing = false;
            Stunned?.Invoke(this);
            livesSeen = l.MaxLives;
            l.ResetLives();                                    // guards don't run out: a slap only stuns them
        }
        livesSeen = l.Lives;
    }

    void FixedUpdate()
    {
        float now = Time.time, dt = Time.fixedDeltaTime;
        Vector3 pos = Flat(body.position);
        if (!target || now < stunnedUntil) { Halt(); return; }

        Vector3 keeper = Flat(target.position);
        float toKeeper = Vector3.Distance(pos, keeper);
        bool alive = !targetLives || targetLives.IsAlive;
        if (Chasing)
        {
            if (!alive || Vector3.Distance(pos, Home) > leash || Vector3.Distance(keeper, Home) > leash + reach) Chasing = false;
        }
        else if (alive && toKeeper < sight && Vector3.Distance(keeper, Home) < leash) Chasing = true;

        if (Chasing && toKeeper < reach)
        {
            Halt();
            Turn(keeper - pos, dt);
            if (now >= nextSlap)
            {
                nextSlap = now + slapCooldown;
                actions?.Slap();
                if (targetLives && targetLives.LoseLife(now))
                {
                    targetActions?.Stung();
                    HitKeeper?.Invoke(this);
                }
            }
            return;
        }

        Vector3 goal = Chasing ? keeper : Home;
        Vector3 d = goal - pos;
        if (!Chasing && d.magnitude < 3f)
        {
            Halt();
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, homeRot, turnSpeed * 0.5f * dt));
            return;
        }
        body.linearVelocity = d.normalized * (Chasing ? chaseSpeed : returnSpeed);
        Turn(d, dt);
    }

    void Turn(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(dir), turnSpeed * dt));
    }

    void Halt()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
