using NUnit.Framework;
using UnityEngine;

namespace GameJamTests
{
    public class ReactionRetargetTests
    {
        static float Angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);

        [Test]
        public void ZeroDelta_ReturnsDstBase()
        {
            var srcRest = Quaternion.Euler(12, 40, -7);
            var dstBase = Quaternion.Euler(-30, 5, 60);
            var result = ReactionRetarget.TransferRotation(srcRest, srcRest,
                Quaternion.Euler(-90, 0, 0), Quaternion.Euler(0, 0, 90), dstBase);
            Assert.Less(Angle(result, dstBase), 0.01f);
        }

        [Test]
        public void WorldYawDelta_TransfersAcrossDifferentParents()
        {
            var srcParent = Quaternion.Euler(-90, 0, 0);
            var dstParent = Quaternion.Euler(0, 0, 90);
            var srcRest = Quaternion.Euler(10, 20, 30);
            var yaw90 = Quaternion.AngleAxis(90, Vector3.up);
            // srcNow is srcRest turned 90 degrees about the character root's up axis
            var srcNow = Quaternion.Inverse(srcParent) * yaw90 * srcParent * srcRest;
            var dstBase = Quaternion.Euler(-45, 15, 5);

            var result = ReactionRetarget.TransferRotation(srcRest, srcNow, srcParent, dstParent, dstBase);

            var dstRootBefore = dstParent * dstBase;
            var dstRootAfter = dstParent * result;
            var rootDelta = dstRootAfter * Quaternion.Inverse(dstRootBefore);
            Assert.Less(Angle(rootDelta, yaw90), 0.01f);
        }

        [Test]
        public void Offset_ScalesByHeightAndParent()
        {
            var srcParent = Quaternion.Euler(-90, 0, 0);       // local +Z is root up
            var dstParent = Quaternion.Euler(0, 0, 90);
            var dstScale = new Vector3(2f, 2f, 2f);
            var local = ReactionRetarget.TransferOffset(new Vector3(0, 0, 0.1f), srcParent, Vector3.one,
                dstParent, dstScale, 1.5f);
            var dstRoot = dstParent * Vector3.Scale(dstScale, local);
            Assert.That(Vector3.Distance(dstRoot, new Vector3(0, 0.15f, 0)), Is.LessThan(1e-4f));
        }

        [Test]
        public void MakeContinuous_RemovesSignFlips()
        {
            var q = Quaternion.Euler(20, 30, 40);
            var neg = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            var samples = new[] { q, neg, q, neg, q };
            ReactionRetarget.MakeContinuous(samples);
            for (int i = 1; i < samples.Length; i++)
                Assert.GreaterOrEqual(Quaternion.Dot(samples[i - 1], samples[i]), 0f, $"flip at {i}");
        }
    }
}
