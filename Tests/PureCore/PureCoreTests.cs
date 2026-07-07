#if SCREENROUTER_PURE_TESTS
// Standalone csc/mono runner for the engine-free state logic. Compile with:
//   csc -define:SCREENROUTER_PURE_TESTS -out:/tmp/sr.exe \
//       Runtime/Core/*.cs Tests/PureCore/PureCoreTests.cs && mono /tmp/sr.exe
// This whole file is compiled out of the Unity assembly (the define is never set there), so its
// Main cannot collide with the game build.

using System;
using System.Collections.Generic;
using System.Linq;
using PFound.ScreenRouter;

internal static class PureCoreTests
{
    private static int _passed;
    private static int _failed;

    private static void Check(bool condition, string label)
    {
        if (condition) { _passed++; }
        else { _failed++; Console.WriteLine("  FAIL: " + label); }
    }

    // ---- test double types used only as keys ----
    private sealed class Alpha { }
    private sealed class Beta { }
    private sealed class Gamma { }

    public static int Main()
    {
        FrameStackTests();
        FrameQueueTests();
        StackingTests();
        GuardTests();
        PoolingTests();
        DefinitionRegistryTests();

        Console.WriteLine();
        Console.WriteLine($"ScreenRouter pure-core tests: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void FrameStackTests()
    {
        var s = new FrameStackModel<string>();
        Check(s.IsEmpty, "new stack is empty");
        Check(s.Top == null, "empty top is null");

        s.PushTop("a"); s.PushTop("b"); s.PushTop("c");
        Check(s.Count == 3, "count after three pushes");
        Check(s.Top == "c", "top is last pushed");
        Check(s.Bottom == "a", "bottom is first pushed");

        // ordering: bottom-to-top matches insertion order (drives sorting depth)
        Check(s.BottomToTop().SequenceEqual(new[] { "a", "b", "c" }), "bottom-to-top order");
        Check(s.TopToBottom().SequenceEqual(new[] { "c", "b", "a" }), "top-to-bottom order");

        // remove from the middle keeps the rest ordered
        Check(s.Remove("b"), "remove middle returns true");
        Check(s.BottomToTop().SequenceEqual(new[] { "a", "c" }), "order intact after middle removal");
        Check(!s.Remove("zzz"), "remove absent returns false");

        // top-down search finds the topmost match first
        s.Clear();
        s.PushTop("x1"); s.PushTop("y"); s.PushTop("x2");
        Check(s.FindTopDown(v => v.StartsWith("x")) == "x2", "find top-down returns topmost match");

        Check(s.PopTop() == "x2", "pop returns top");
        Check(s.Top == "y", "pop advances top");

        s.Clear();
        Check(s.IsEmpty, "clear empties");
        Check(s.PopTop() == null, "pop on empty is null");
    }

    private static void FrameQueueTests()
    {
        var q = new FrameQueueModel<int>(3);
        Check(q.IsEmpty, "new queue empty");
        Check(q.Enqueue(typeof(Alpha), 1), "enqueue first");
        Check(q.Enqueue(typeof(Beta), 2), "enqueue second");
        Check(!q.Enqueue(typeof(Alpha), 9), "dedup rejects same key");
        Check(q.Count == 2, "dedup did not grow queue");

        Check(q.Enqueue(typeof(Gamma), 3), "enqueue third fills capacity");
        Check(!q.Enqueue(typeof(string), 4), "capacity blocks overflow");

        Check(q.TryDequeue(out var k1, out var v1) && k1 == typeof(Alpha) && v1 == 1, "FIFO dequeue first");
        Check(q.Enqueue(typeof(string), 4), "space frees after dequeue");
        Check(q.TryDequeue(out var k2, out _) && k2 == typeof(Beta), "FIFO dequeue second");

        // unbounded queue (each key is a distinct nested generic type)
        var u = new FrameQueueModel<int>(0);
        Type t = typeof(int);
        for (int i = 0; i < 50; i++) { u.Enqueue(t, i); t = typeof(List<>).MakeGenericType(t); }
        Check(u.Count == 50, "capacity 0 is unbounded");

        q.Clear();
        Check(q.IsEmpty, "clear empties queue");
        Check(!q.TryDequeue(out _, out _), "dequeue empty returns false");
    }

    private static void StackingTests()
    {
        // empty stack accepts anything
        Check(StackingEvaluator.CanStack(null, typeof(Alpha), false), "empty stack accepts");

        // permissive top accepts anything
        var open = new StackingRules(blocks: false, whitelist: null, forcesOnTop: false);
        Check(StackingEvaluator.CanStack(open, typeof(Alpha), false), "non-blocking top accepts");

        // blocking top with empty whitelist rejects
        var closed = new StackingRules(blocks: true, whitelist: null, forcesOnTop: false);
        Check(!StackingEvaluator.CanStack(closed, typeof(Alpha), false), "blocking top rejects unlisted");

        // blocking top with whitelist accepts listed only
        var gated = new StackingRules(blocks: true, whitelist: new[] { typeof(Beta) }, forcesOnTop: false);
        Check(StackingEvaluator.CanStack(gated, typeof(Beta), false), "blocking top accepts whitelisted");
        Check(!StackingEvaluator.CanStack(gated, typeof(Alpha), false), "blocking top rejects non-whitelisted");

        // incoming force-on-top bypasses a blocking top
        Check(StackingEvaluator.CanStack(closed, typeof(Alpha), incomingForcesOnTop: true),
            "force-on-top overrides blocking top");

        // PermitsOnTop direct
        Check(!closed.PermitsOnTop(typeof(Alpha)), "PermitsOnTop reflects blocking");
        Check(gated.PermitsOnTop(typeof(Beta)), "PermitsOnTop honours whitelist");
    }

    private static void GuardTests()
    {
        var reg = new GuardRegistry();

        // no guards => approved as-is
        Check(reg.Resolve(typeof(Alpha)).Approved, "no guards approves");

        // single blocking guard
        reg.Add(typeof(Alpha), () => GuardDecision.Block());
        var denied = reg.Resolve(typeof(Alpha));
        Check(denied.Kind == GuardResolutionKind.Denied, "block guard denies");

        // AND-ing: one passes, one blocks => denied
        reg.RemoveAll(typeof(Alpha));
        reg.Add(typeof(Alpha), () => GuardDecision.Pass());
        reg.Add(typeof(Alpha), () => GuardDecision.Block());
        Check(!reg.Resolve(typeof(Alpha)).Approved, "AND of pass+block denies");

        // reroute chain resolves to final approved target
        var reg2 = new GuardRegistry();
        reg2.Add(typeof(Alpha), () => GuardDecision.Reroute<Beta>());
        var routed = reg2.Resolve(typeof(Alpha));
        Check(routed.Approved && routed.Target == typeof(Beta), "reroute lands on target");

        // reroute then block
        reg2.Add(typeof(Beta), () => GuardDecision.Block());
        Check(!reg2.Resolve(typeof(Alpha)).Approved, "reroute into a block denies");

        // reroute loop detection A->B->A
        var reg3 = new GuardRegistry();
        reg3.Add(typeof(Alpha), () => GuardDecision.Reroute<Beta>());
        reg3.Add(typeof(Beta), () => GuardDecision.Reroute<Alpha>());
        var loop = reg3.Resolve(typeof(Alpha));
        Check(loop.Kind == GuardResolutionKind.RedirectLoop, "reroute cycle detected");

        // remove specific guard
        var reg4 = new GuardRegistry();
        Func<GuardDecision> g = () => GuardDecision.Block();
        reg4.Add(typeof(Alpha), g);
        Check(reg4.HasAny(typeof(Alpha)), "guard registered");
        Check(reg4.Remove(typeof(Alpha), g), "remove specific guard");
        Check(!reg4.HasAny(typeof(Alpha)), "guard gone after remove");
    }

    private static void PoolingTests()
    {
        Check(PoolingPolicy.DestroyOnClose(PoolingType.Ephemeral), "ephemeral destroys on close");
        Check(!PoolingPolicy.DestroyOnClose(PoolingType.Recyclable), "recyclable kept on close");
        Check(PoolingPolicy.KeepForReuse(PoolingType.AppLifetime), "app-lifetime kept for reuse");
        Check(!PoolingPolicy.KeepForReuse(PoolingType.Ephemeral), "ephemeral not kept");

        Check(PoolingPolicy.ReclaimWhenIdle(PoolingType.Recyclable), "recyclable reclaimed when idle");
        Check(!PoolingPolicy.ReclaimWhenIdle(PoolingType.AppLifetime), "app-lifetime not idle-reclaimed");

        Check(PoolingPolicy.DropOnSceneChange(PoolingType.SceneLifetime), "scene-lifetime dropped on scene change");
        Check(!PoolingPolicy.DropOnSceneChange(PoolingType.AppLifetime), "app-lifetime survives scene change");

        Check(PoolingPolicy.CanRevive(PoolingType.Recyclable), "recyclable revivable");
        Check(!PoolingPolicy.CanRevive(PoolingType.Ephemeral), "ephemeral not revivable");
    }

    private static void DefinitionRegistryTests()
    {
        // config tier supplies Beta; runtime supplies Alpha and shadows a config Alpha
        var config = new Dictionary<Type, string>
        {
            { typeof(Beta), "config-beta" },
            { typeof(Alpha), "config-alpha" },
        };
        var reg = new DefinitionRegistry<string>(t => config.TryGetValue(t, out var v) ? v : null);

        // falls through to config
        Check(reg.TryResolve(typeof(Beta), out var b) && b == "config-beta", "resolves from config tier");

        // runtime shadows config
        reg.Register(typeof(Alpha), "runtime-alpha");
        Check(reg.Resolve(typeof(Alpha)) == "runtime-alpha", "runtime shadows config");
        Check(reg.HasRuntime(typeof(Alpha)), "runtime registration reported");

        // unknown type
        Check(!reg.TryResolve(typeof(Gamma), out _), "unknown type not resolved");
        bool threw = false;
        try { reg.Resolve(typeof(Gamma)); } catch (KeyNotFoundException) { threw = true; }
        Check(threw, "resolve throws for unknown");

        // unregister restores config-tier value
        Check(reg.Unregister(typeof(Alpha)), "unregister runtime");
        Check(reg.Resolve(typeof(Alpha)) == "config-alpha", "config visible again after unregister");
    }
}
#endif
