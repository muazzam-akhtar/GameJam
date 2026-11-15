using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Versus only - the end-of-match story, played while the result screen waits (<see cref="Length"/> s):
///   the Keeper won  - the main character comes running in, cheers (happy face) and the surviving souls fly to them;
///   the Reaper won  - the main character trudges in and stops, crestfallen (sad face).
/// Both halves of the screen watch it (both cameras frame the Keeper's spot, then are handed back).
/// </summary>
public class EndingScene : MonoBehaviour
{
    public const float Length = 3.6f;                // ~3 s of story, then the result panel
    const float Height = 26f;                       // about the Keeper's size (its big head makes it read larger)
    const float RunSpeed = 46f, WalkSpeed = 20f;

    MainCharController hero;
    bool happy;
    Vector3 from, to;
    float speed, age, arriveAt;
    bool arrived;
    readonly List<(Transform t, Vector3 start, float delay, GameObject burst)> souls = new List<(Transform, Vector3, float, GameObject)>();
    readonly List<(FollowCamera cam, Transform target)> cams = new List<(FollowCamera, Transform)>();
    RectTransform emote;
    Image emoteImage;
    Camera viewer;

    /// <summary>Starts the scene next to the Keeper; returns null if there's no main character to show.</summary>
    public static EndingScene Play(bool keeperWon, Transform keeper, IEnumerable<Offering> survivors, params FollowCamera[] views)
    {
        var theme = HudTheme.Load();
        if (!theme || !theme.mainCharacter || !keeper) return null;
        var go = new GameObject("Ending");
        var scene = go.AddComponent<EndingScene>();
        scene.Begin(theme, keeperWon, keeper, survivors, views);
        return scene;
    }

    void Begin(HudTheme theme, bool keeperWon, Transform keeper, IEnumerable<Offering> survivors, FollowCamera[] views)
    {
        happy = keeperWon;
        viewer = views.Length > 0 && views[0] ? views[0].GetComponent<Camera>() : Camera.main;

        // stand beside the Keeper, coming in from the side of the Keeper's view
        var side = viewer ? Vector3.ProjectOnPlane(viewer.transform.right, Vector3.up).normalized : Vector3.right;
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        // the end spot is picked through the camera: lower right of the shot, so it's in frame on any screen shape
        to = Flat(keeper.position) + side * 20f;
        if (viewer)
        {
            var ray = viewer.ViewportPointToRay(new Vector3(0.72f, 0.34f, 0f));
            if (ray.direction.y < -0.01f) to = Flat(ray.GetPoint(-ray.origin.y / ray.direction.y));
            var off = to - Flat(keeper.position);
            if (off.magnitude < 16f) to = Flat(keeper.position) + side * 16f;    // never inside the Keeper
        }
        from = to + side * (happy ? 48f : 30f);                         // starts at the edge of the shot: seen at once
        speed = happy ? RunSpeed : WalkSpeed;
        arriveAt = Vector3.Distance(from, to) / speed;

        var go = Instantiate(theme.mainCharacter, from, Quaternion.LookRotation(to - from), transform);
        go.transform.localScale *= Height / Mathf.Max(0.01f, BoundsHeight(go));
        hero = go.GetComponent<MainCharController>();
        if (!happy && theme.mainCharacterSadFace)                          // the sad face on the body itself
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial && r.sharedMaterial.name.StartsWith("MC_Body"))
                {
                    var m = r.material;                                     // this copy only, never the prefab's
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", theme.mainCharacterSadFace);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", theme.mainCharacterSadFace);
                }
        if (hero)
        {
            hero.DriveFromCode();
            hero.SetMove(happy ? MainCharController.MoveState.Run : MainCharController.MoveState.Walk);
        }

        foreach (var v in views)
        {
            if (!v) continue;
            cams.Add((v, v.target));
            v.target = keeper;                                             // the stage: the Keeper's spot
            if (v == views[0]) continue;                                    // the Keeper's camera is already there
            v.Snap();                                                       // cut straight there, no flight across the map
            if (views[0]) v.Look(views[0].Yaw - v.Yaw, views[0].Pitch - v.Pitch);   // and the same shot as the Keeper's half
        }

        if (happy && survivors != null)
        {
            float delay = Mathf.Max(0.3f, arriveAt - 0.4f);
            foreach (var o in survivors)
            {
                if (!o || o.Done) continue;
                o.enabled = false;                                         // it flies now, no more bobbing / eating
                foreach (var c in o.GetComponentsInChildren<Collider>()) c.enabled = false;
                souls.Add((o.transform, o.transform.position, delay, o.goodBurst));
                delay += 0.1f;
            }
        }

        BuildEmote(happy ? theme.emoteHappy : theme.emoteSad, go.transform);
    }

    void BuildEmote(Sprite sprite, Transform parent)
    {
        if (!sprite) return;
        var canvasGo = new GameObject("Emote", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        emote = canvasGo.GetComponent<RectTransform>();
        emote.sizeDelta = new Vector2(100f, 100f);
        emote.localScale = Vector3.zero;
        emoteImage = HudStyle.Img(emote, "Face", sprite, Color.white);
        emoteImage.preserveAspect = true;
        HudStyle.Fill(emoteImage.rectTransform);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (!hero) return;
        var t = hero.transform;

        if (!arrived)
        {
            float k = Mathf.Clamp01(age / arriveAt);
            t.position = Vector3.Lerp(from, to, k);
            if (k >= 1f)
            {
                arrived = true;
                hero.SetMove(MainCharController.MoveState.Idle);
                if (happy) hero.PowerUp(); else hero.Sting();
            }
        }
        else if (viewer)                                                    // turn to the camera
        {
            var look = Vector3.ProjectOnPlane(viewer.transform.position - t.position, Vector3.up);
            if (look.sqrMagnitude > 0.01f)
                t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(look), 360f * dt);
        }

        // the surviving souls stream to the main character
        var chest = t.position + Vector3.up * Height * 0.6f;
        for (int i = souls.Count - 1; i >= 0; i--)
        {
            var s = souls[i];
            if (!s.t) { souls.RemoveAt(i); continue; }
            float k = Mathf.Clamp01((age - s.delay) / 0.9f);
            if (k <= 0f) continue;
            float e = k * k * (3f - 2f * k);
            s.t.position = Vector3.Lerp(s.start, chest, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 40f;
            if (k >= 1f)
            {
                if (s.burst) Destroy(Instantiate(s.burst, chest, Quaternion.identity), 3f);
                Destroy(s.t.gameObject);
                souls.RemoveAt(i);
            }
        }

        if (emote)                                                         // the face pops in over the head on arrival
        {
            float k = arrived ? HudStyle.OutBack(Mathf.Clamp01((age - arriveAt) / 0.35f)) : 0f;
            emote.position = t.position + Vector3.up * (Height + 6f + Mathf.Sin(age * 3f) * 0.8f);
            emote.localScale = Vector3.one * 0.13f * k;
            if (viewer) emote.rotation = Quaternion.LookRotation(emote.position - viewer.transform.position);
        }
    }

    void OnDestroy()
    {
        foreach (var (cam, target) in cams)
            if (cam) { cam.target = target; cam.Snap(); }
        foreach (var s in souls) if (s.t) Destroy(s.t.gameObject);
    }

    static float BoundsHeight(GameObject go)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            min = Mathf.Min(min, r.bounds.min.y);
            max = Mathf.Max(max, r.bounds.max.y);
        }
        return max > min ? max - min : 1f;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
