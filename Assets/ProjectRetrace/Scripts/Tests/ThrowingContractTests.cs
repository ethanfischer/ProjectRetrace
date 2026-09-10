using NUnit.Framework;
using UnityEngine;

namespace ProjectRetrace.Tests
{
    /// <summary>The contracts a replayed throw leans on: throws survive the wire, the
    /// recording maths inverts exactly, and the hit test behaves at the capsule's edges.</summary>
    public class ThrowingContractTests
    {
        [Test]
        public void RouteDataRoundTripsThrows()
        {
            var route = new RecordedRoute();
            route.Crumbs.Add(new Breadcrumb(Vector3.zero, Vector3.forward));
            route.Throws.Add(new ThrowPoint(new Vector3(1f, 0f, 2f), new Vector3(0.35f, 1.3f, 0.6f), 90f, -20f, 11f, 0, "House#0/Mug#2"));

            var json = JsonUtility.ToJson(RouteData.From(route));
            var back = JsonUtility.FromJson<RouteData>(json).ToRoute();

            Assert.AreEqual(1, back.Throws.Count);
            var t = back.Throws[0];
            Assert.AreEqual(new Vector3(1f, 0f, 2f), t.Position);
            Assert.AreEqual(new Vector3(0.35f, 1.3f, 0.6f), t.HandOffset);
            Assert.AreEqual(90f, t.Yaw);
            Assert.AreEqual(-20f, t.Pitch);
            Assert.AreEqual(11f, t.Speed);
            Assert.AreEqual(0, t.CrumbIndex);
            Assert.AreEqual("House#0/Mug#2", t.PropId);
        }

        [Test]
        public void ThrowPointReconstructsRecordedVector()
        {
            var root = new Vector3(3f, 0.05f, -4f);
            var origin = root + new Vector3(0.4f, 1.4f, 0.5f);
            var direction = new Vector3(0.6f, 0.3f, -0.74f).normalized;

            var recorded = ThrowPoint.Record(root, origin, direction, 11f, 7, "House#0/Mug#2");
            recorded.Reconstruct(root, out var backOrigin, out var velocity);

            Assert.That(Vector3.Distance(origin, backOrigin), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(direction * 11f, velocity), Is.LessThan(0.01f));
            Assert.AreEqual(7, recorded.CrumbIndex);
        }

        [Test]
        public void SnapshotMsgRoundTripsProjectilesAndHeld()
        {
            var snapshot = new SnapshotMsg { t = 1f };
            snapshot.player.held = "House#0/Mug#2";
            snapshot.projectiles.Add(new ProjectileSnap { id = "House#0/Vase#1", p = Vector3.one, r = new Vector3(0f, 90f, 0f) });
            snapshot.projectiles.Add(new ProjectileSnap { id = "clone:Sentry 1:House#0/Mug#2", src = "House#0/Mug#2", p = Vector3.zero, r = Vector3.zero });

            var json = JsonUtility.ToJson(snapshot);
            Assert.AreEqual("snapshot", JsonUtility.FromJson<MsgHeader>(json).type);
            var back = JsonUtility.FromJson<SnapshotMsg>(json);

            Assert.AreEqual("House#0/Mug#2", back.player.held);
            Assert.AreEqual(2, back.projectiles.Count);
            Assert.AreEqual("House#0/Mug#2", back.projectiles[1].src);
            Assert.AreEqual(Vector3.one, back.projectiles[0].p);

            var old = JsonUtility.FromJson<SnapshotMsg>("{\"type\":\"snapshot\",\"t\":2.0}");
            Assert.IsNotNull(old.projectiles);
            Assert.AreEqual(0, old.projectiles.Count);
        }

        [Test]
        public void HitsCapsuleInsideAndOutside()
        {
            var foot = new Vector3(2f, 0f, 2f);
            Assert.IsTrue(Projectile.HitsCapsule(foot + new Vector3(0.2f, 1.0f, 0f), foot, 0.5f, 1.8f));
            Assert.IsTrue(Projectile.HitsCapsule(foot + new Vector3(0f, 1.7f, 0.2f), foot, 0.5f, 1.8f));
            Assert.IsFalse(Projectile.HitsCapsule(foot + new Vector3(0.6f, 1.0f, 0f), foot, 0.5f, 1.8f));
            Assert.IsFalse(Projectile.HitsCapsule(foot + new Vector3(0f, 2.5f, 0f), foot, 0.5f, 1.8f));
        }
    }
}
