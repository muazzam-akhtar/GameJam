using UnityEngine;

/// <summary>
/// Math for copying one rig's animation motion onto another rig whose bones point along different axes.
/// Motion is moved as a delta through the character-root space, so "lean back" stays "lean back"
/// whatever the bones' local axes are.
/// </summary>
public static class ReactionRetarget
{
    /// <summary>
    /// The source bone's change from <paramref name="srcRest"/> to <paramref name="srcNow"/> (both local rotations),
    /// re-expressed for the destination bone and applied on top of <paramref name="dstBase"/>.
    /// Parent rotations are the parents' rotations relative to each character's root.
    /// </summary>
    public static Quaternion TransferRotation(Quaternion srcRest, Quaternion srcNow, Quaternion srcParentRootRot,
        Quaternion dstParentRootRot, Quaternion dstBase)
    {
        var deltaParent = srcNow * Quaternion.Inverse(srcRest);
        var deltaRoot = srcParentRootRot * deltaParent * Quaternion.Inverse(srcParentRootRot);
        var deltaDst = Quaternion.Inverse(dstParentRootRot) * deltaRoot * dstParentRootRot;
        return Normalize(deltaDst * dstBase);
    }

    /// <summary>
    /// A local-position offset of the source bone (in its parent's space), scaled by the characters' height
    /// ratio and re-expressed as a local-position offset for the destination bone.
    /// </summary>
    public static Vector3 TransferOffset(Vector3 srcDeltaParentSpace, Quaternion srcParentRootRot, Vector3 srcParentRootScale,
        Quaternion dstParentRootRot, Vector3 dstParentRootScale, float heightRatio)
    {
        var root = srcParentRootRot * Vector3.Scale(srcParentRootScale, srcDeltaParentSpace) * heightRatio;
        var local = Quaternion.Inverse(dstParentRootRot) * root;
        return new Vector3(SafeDiv(local.x, dstParentRootScale.x), SafeDiv(local.y, dstParentRootScale.y), SafeDiv(local.z, dstParentRootScale.z));
    }

    /// <summary>Flips quaternion signs so neighbouring samples never interpolate the long way round.</summary>
    public static void MakeContinuous(Quaternion[] samples)
    {
        for (int i = 1; i < samples.Length; i++)
            if (Quaternion.Dot(samples[i - 1], samples[i]) < 0f)
                samples[i] = new Quaternion(-samples[i].x, -samples[i].y, -samples[i].z, -samples[i].w);
    }

    static float SafeDiv(float a, float b) => Mathf.Abs(b) < 1e-6f ? a : a / b;

    static Quaternion Normalize(Quaternion q)
    {
        float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        return m < 1e-8f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
    }
}
