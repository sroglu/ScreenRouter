using System;
using NUnit.Framework;
using PFound.ScreenRouter;

namespace PFound.ScreenRouter.Tests
{
    /// <summary>
    /// EditMode coverage for the engine-free state logic. These mirror the standalone csc/mono
    /// runner so the same assertions execute inside Unity's Test Runner. The MonoBehaviour lifecycle
    /// layer is exercised by integration tests on the project side, not here.
    /// </summary>
    public sealed class StateLogicTests
    {
        private sealed class Alpha { }
        private sealed class Beta { }
        private sealed class Gamma { }

        [Test]
        public void FrameStack_MaintainsBottomToTopOrder()
        {
            var s = new FrameStackModel<string>();
            s.PushTop("a"); s.PushTop("b"); s.PushTop("c");
            Assert.AreEqual("c", s.Top);
            Assert.AreEqual("a", s.Bottom);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, s.BottomToTop());
            CollectionAssert.AreEqual(new[] { "c", "b", "a" }, s.TopToBottom());
        }

        [Test]
        public void FrameStack_RemoveMiddle_KeepsOrder()
        {
            var s = new FrameStackModel<string>();
            s.PushTop("a"); s.PushTop("b"); s.PushTop("c");
            Assert.IsTrue(s.Remove("b"));
            CollectionAssert.AreEqual(new[] { "a", "c" }, s.BottomToTop());
        }

        [Test]
        public void FrameStack_FindTopDown_ReturnsTopmostMatch()
        {
            var s = new FrameStackModel<string>();
            s.PushTop("x1"); s.PushTop("y"); s.PushTop("x2");
            Assert.AreEqual("x2", s.FindTopDown(v => v.StartsWith("x")));
        }

        [Test]
        public void FrameQueue_DedupsAndRespectsCapacity()
        {
            var q = new FrameQueueModel<int>(2);
            Assert.IsTrue(q.Enqueue(typeof(Alpha), 1));
            Assert.IsFalse(q.Enqueue(typeof(Alpha), 9), "duplicate key rejected");
            Assert.IsTrue(q.Enqueue(typeof(Beta), 2));
            Assert.IsFalse(q.Enqueue(typeof(Gamma), 3), "capacity reached");
            Assert.IsTrue(q.TryDequeue(out var k, out var v));
            Assert.AreEqual(typeof(Alpha), k);
            Assert.AreEqual(1, v);
        }

        [Test]
        public void Stacking_ForceOnTop_BypassesBlockingTop()
        {
            var blocking = new StackingRules(true, null, false);
            Assert.IsFalse(StackingEvaluator.CanStack(blocking, typeof(Alpha), false));
            Assert.IsTrue(StackingEvaluator.CanStack(blocking, typeof(Alpha), true));
        }

        [Test]
        public void Stacking_Whitelist_AllowsListedOnly()
        {
            var gated = new StackingRules(true, new[] { typeof(Beta) }, false);
            Assert.IsTrue(StackingEvaluator.CanStack(gated, typeof(Beta), false));
            Assert.IsFalse(StackingEvaluator.CanStack(gated, typeof(Alpha), false));
        }

        [Test]
        public void Guards_AreAndedAndFollowReroute()
        {
            var reg = new GuardRegistry();
            reg.Add(typeof(Alpha), GuardDecision.Pass);
            reg.Add(typeof(Alpha), GuardDecision.Block);
            Assert.IsFalse(reg.Resolve(typeof(Alpha)).Approved);

            var reg2 = new GuardRegistry();
            reg2.Add(typeof(Alpha), GuardDecision.Reroute<Beta>);
            var routed = reg2.Resolve(typeof(Alpha));
            Assert.IsTrue(routed.Approved);
            Assert.AreEqual(typeof(Beta), routed.Target);
        }

        [Test]
        public void Guards_DetectRerouteLoop()
        {
            var reg = new GuardRegistry();
            reg.Add(typeof(Alpha), GuardDecision.Reroute<Beta>);
            reg.Add(typeof(Beta), GuardDecision.Reroute<Alpha>);
            Assert.AreEqual(GuardResolutionKind.RedirectLoop, reg.Resolve(typeof(Alpha)).Kind);
        }

        [Test]
        public void Pooling_PolicyMatchesTiers()
        {
            Assert.IsTrue(PoolingPolicy.DestroyOnClose(PoolingType.Ephemeral));
            Assert.IsTrue(PoolingPolicy.ReclaimWhenIdle(PoolingType.Recyclable));
            Assert.IsTrue(PoolingPolicy.DropOnSceneChange(PoolingType.SceneLifetime));
            Assert.IsFalse(PoolingPolicy.DropOnSceneChange(PoolingType.AppLifetime));
            Assert.IsFalse(PoolingPolicy.CanRevive(PoolingType.Ephemeral));
        }

        [Test]
        public void DefinitionRegistry_RuntimeShadowsConfig()
        {
            var config = new System.Collections.Generic.Dictionary<Type, string>
            {
                { typeof(Alpha), "config-alpha" },
            };
            var reg = new DefinitionRegistry<string>(t => config.TryGetValue(t, out var v) ? v : null);
            Assert.AreEqual("config-alpha", reg.Resolve(typeof(Alpha)));
            reg.Register(typeof(Alpha), "runtime-alpha");
            Assert.AreEqual("runtime-alpha", reg.Resolve(typeof(Alpha)));
            reg.Unregister(typeof(Alpha));
            Assert.AreEqual("config-alpha", reg.Resolve(typeof(Alpha)));
        }
    }
}
