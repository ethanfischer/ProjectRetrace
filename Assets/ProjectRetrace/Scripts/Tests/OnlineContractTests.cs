using NUnit.Framework;
using UnityEngine;

namespace ProjectRetrace.Tests
{
    /// <summary>The contracts online play leans on: routes survive the wire, prop ids are
    /// stable and distinct, seeds derive identically, and the session's handshake holds.</summary>
    public class OnlineContractTests
    {
        [Test]
        public void RouteDataRoundTripsThroughJson()
        {
            var route = new RecordedRoute { Owner = 2, Distance = 12.5f };
            route.Crumbs.Add(new Breadcrumb(new Vector3(1f, 0f, 2f), Vector3.forward));
            route.Crumbs.Add(new Breadcrumb(new Vector3(1f, 0f, 3f), Vector3.right));
            route.Dwells.Add(new DwellPoint(new Vector3(1f, 0f, 3f), 90f, 1, "House#0/Cupboard#3/Door#0"));

            var json = JsonUtility.ToJson(RouteData.From(route));
            var back = JsonUtility.FromJson<RouteData>(json).ToRoute();

            Assert.AreEqual(2, back.Owner);
            Assert.AreEqual(12.5f, back.Distance);
            Assert.AreEqual(2, back.Crumbs.Count);
            Assert.AreEqual(new Vector3(1f, 0f, 3f), back.Crumbs[1].Position);
            Assert.AreEqual(Vector3.right, back.Crumbs[1].Direction);
            Assert.AreEqual(1, back.Dwells.Count);
            Assert.AreEqual(90f, back.Dwells[0].FacingYaw);
            Assert.AreEqual(1, back.Dwells[0].CrumbIndex);
            Assert.AreEqual("House#0/Cupboard#3/Door#0", back.Dwells[0].PropId);
        }

        [Test]
        public void HierarchyPathDistinguishesSameNamedSiblings()
        {
            var root = new GameObject("House");
            var a = new GameObject("Cupboard");
            var b = new GameObject("Cupboard");
            a.transform.SetParent(root.transform);
            b.transform.SetParent(root.transform);
            try
            {
                Assert.AreEqual("House#" + root.transform.GetSiblingIndex() + "/Cupboard#0", HierarchyPath.Of(a.transform));
                Assert.AreNotEqual(HierarchyPath.Of(a.transform), HierarchyPath.Of(b.transform));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RoundSeedMatchesTheOriginalDerivation()
        {
            const int seed = 12345;
            Assert.AreEqual(unchecked(seed * 486187739 + 3), GameDirector.RoundSeed(seed, 3));
            Assert.AreNotEqual(GameDirector.RoundSeed(seed, 1), GameDirector.RoundSeed(seed, 2));
        }

        [Test]
        public void MessagesParseByHeaderThenConcreteType()
        {
            var json = JsonUtility.ToJson(new RoundResultMsg { kind = RoundResultMsg.Caught, round = 2, by = 1, lives = 1, winner = 0, epoch = 4 });
            var header = JsonUtility.FromJson<MsgHeader>(json);
            Assert.AreEqual("round-result", header.type);
            Assert.AreEqual(4, header.epoch);
            var result = JsonUtility.FromJson<RoundResultMsg>(json);
            Assert.AreEqual(RoundResultMsg.Caught, result.kind);
            Assert.IsTrue(result.durable);
        }

        [Test]
        public void LoopbackHandshakeReachesReadyAndRejectsMismatchedHouse()
        {
            var systems = new GameObject("Systems");
            try
            {
                var director = systems.AddComponent<GameDirector>();
                var session = systems.AddComponent<OnlineSession>();
                session.director = director;
                var loop = new LoopbackTransport();
                session.UseTransport(loop);

                session.OpenLobby();
                session.CreateRoom();
                Assert.AreEqual(1, loop.Sent.Count);
                Assert.AreEqual("create", JsonUtility.FromJson<MsgHeader>(loop.Sent[0]).type);

                loop.Inject(new JoinedMsg { room = "ABCD", seat = 1, token = "t", peerPresent = true });
                loop.Inject(new SyncedMsg());
                loop.Poll();
                Assert.AreEqual(NetState.InRoom, session.State);
                Assert.AreEqual("hello", JsonUtility.FromJson<MsgHeader>(loop.Sent[loop.Sent.Count - 1]).type);

                loop.Inject(new HelloMsg { seat = 2, house = HouseIdentity.Current, spots = HouseIdentity.KeySpotCount, protocol = HouseIdentity.Protocol });
                loop.Poll();
                Assert.AreEqual(NetState.Ready, session.State);

                loop.Inject(new HelloMsg { seat = 2, house = "somebody else's build", spots = 0, protocol = HouseIdentity.Protocol });
                loop.Poll();
                Assert.AreEqual(NetState.Error, session.State);
                StringAssert.Contains("Builds differ", session.LastError);
            }
            finally
            {
                Object.DestroyImmediate(systems);
            }
        }

        [Test]
        public void SnapshotBufferSamplesBetweenNeighbours()
        {
            var buffer = new SnapshotBuffer();
            var now = Time.unscaledTime;
            buffer.Push(new SnapshotMsg { t = now - 1.0f, player = new PlayerSnap { p = Vector3.zero } });
            buffer.Push(new SnapshotMsg { t = now - 0.5f, player = new PlayerSnap { p = Vector3.one } });
            buffer.Push(new SnapshotMsg { t = now, player = new PlayerSnap { p = Vector3.one * 2f } });

            Assert.IsTrue(buffer.Sample(0.75f, out var from, out var to, out var t));
            Assert.AreEqual(Vector3.zero, from.player.p);
            Assert.AreEqual(Vector3.one, to.player.p);
            Assert.AreEqual(0.5f, t, 0.05f);
        }
    }
}
