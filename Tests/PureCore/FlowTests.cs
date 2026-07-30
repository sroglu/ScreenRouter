#if SCREENROUTER_FLOW_TESTS
// Standalone csc/mono runner for the engine-free NavigationFlow subsystem. Compile with:
//   csc -nologo -warn:0 -define:SCREENROUTER_FLOW_TESTS -out:/tmp/sr_flow.exe \
//       Runtime/Flow/*.cs Tests/PureCore/FlowTests.cs && mono /tmp/sr_flow.exe
// The Unity seam (ScreenRouterNavigationSink) lives under Runtime/Router and is deliberately NOT in
// this glob, so the whole compile is engine-free. The define is never set in the Unity assembly, so
// this Main cannot collide with the game build.

using System;
using System.Collections.Generic;
using PFound.ScreenRouter.Flow;

internal static class FlowTests
{
    private static int _passed;
    private static int _failed;

    private static void Check(bool condition, string label)
    {
        if (condition) { _passed++; }
        else { _failed++; Console.WriteLine("  FAIL: " + label); }
    }

    // ---- content-type keys (plain classes; the flow core does not require Unity Screen/Frame) ----
    private sealed class Home { }
    private sealed class Play { }
    private sealed class Pause { }
    private sealed class Settings { }
    private sealed class SubEntry { }
    private sealed class SubDetail { }
    private sealed class A { }
    private sealed class B { }

    private enum Nav { Play, Pause, Resume, Open, Next, Enter, Go }

    // ---- recording sink: captures the router calls the runner would make ----
    private sealed class RecordingSink : INavigationSink
    {
        public readonly List<string> Calls = new List<string>();
        public string Last => Calls.Count > 0 ? Calls[Calls.Count - 1] : null;
        public void SwitchScreen(Type t) => Calls.Add("screen:" + t.Name);
        public void OpenFrame(Type t) => Calls.Add("frame:" + t.Name);
    }

    public static int Main()
    {
        BuilderGraphTests();
        RunnerBackStackTests();
        SubFlowTests();
        CycleDetectionTests();
        MisuseTests();

        Console.WriteLine();
        Console.WriteLine($"ScreenRouter flow tests: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void BuilderGraphTests()
    {
        // Typed-action flow: Home --Play--> Play(screen), Home --Open--> Settings(frame, forward ref).
        var flow = NavigationFlow.Define<Nav>("Main")
            .Screen<Home>()
                .OnAction(Nav.Play, t => t.Screen<Play>())
                .OnAction(Nav.Open, t => t.Frame<Settings>())
            .Build();

        Check(flow.Name == "Main", "flow keeps its name");
        Check(flow.EntryStep != null && flow.EntryStep.ContentType == typeof(Home), "entry step is first authored");
        Check(flow.EntryStep.IsScreen, "Home is a screen node");
        Check(flow.StepCount == 3, "auto-created forward-ref targets are nodes (Home+Play+Settings)");

        var home = flow.GetStep(typeof(Home));
        Check(home.EdgeCount == 2, "Home has two action edges");
        Check(home.TryGetTransition(Nav.Play, out var toPlay), "Play edge present");
        Check(!toPlay.EntersSubFlow && toPlay.TargetStep.ContentType == typeof(Play), "Play edge targets Play step");
        Check(toPlay.TargetStep.IsScreen, "Play target is a screen");
        Check(home.TryGetTransition(Nav.Open, out var toSettings), "Open edge present");
        Check(!toSettings.TargetStep.IsScreen, "Settings target is a frame");
        Check(!home.TryGetTransition(Nav.Pause, out _), "absent action reports no edge");

        // String-keyed flow variant.
        var strFlow = NavigationFlow.Define("StrFlow")
            .Screen<A>().OnAction("go", t => t.Frame<B>())
            .Build();
        Check(strFlow.GetStep(typeof(A)).TryGetTransition("go", out var g) && g.TargetStep.ContentType == typeof(B),
            "string action keys wire correctly");

        // Re-opening the same node type returns the same underlying step (no duplicate node).
        var reuse = NavigationFlow.Define<Nav>("Reuse")
            .Screen<Home>().OnAction(Nav.Play, t => t.Screen<Play>())
            .Screen<Play>().OnAction(Nav.Pause, t => t.Frame<Pause>())
            .Build();
        Check(reuse.StepCount == 3, "reopening Play did not duplicate the node");
    }

    private static void RunnerBackStackTests()
    {
        var flow = NavigationFlow.Define<Nav>("Main")
            .Screen<Home>().OnAction(Nav.Play, t => t.Screen<Play>())
            .Screen<Play>().OnAction(Nav.Open, t => t.Frame<Settings>())
            .Build();

        var sink = new RecordingSink();
        var runner = new NavigationFlowRunner<Nav>(flow, sink);

        Check(!runner.IsRunning, "not running before Start");
        runner.Start();
        Check(runner.IsRunning, "running after Start");
        Check(sink.Last == "screen:Home", "Start routes to entry screen");
        Check(runner.Depth == 0, "root depth is 0");
        Check(runner.CurrentStep.ContentType == typeof(Home), "current step is Home");

        Check(runner.Execute(Nav.Play), "Execute Play succeeds");
        Check(sink.Last == "screen:Play", "Play routed to Play screen");
        Check(runner.CurrentStep.ContentType == typeof(Play), "current step advanced to Play");

        Check(runner.Execute(Nav.Open), "Execute Open succeeds");
        Check(sink.Last == "frame:Settings", "Open routed to Settings frame");

        Check(!runner.Execute(Nav.Pause), "unknown action returns false");
        Check(sink.Last == "frame:Settings", "unknown action did not route");

        // Back unwinds the level's history: Settings -> Play -> Home, then nothing.
        Check(runner.Back(), "Back from Settings succeeds");
        Check(sink.Last == "screen:Play" && runner.CurrentStep.ContentType == typeof(Play), "Back restored Play");
        Check(runner.Back(), "Back from Play succeeds");
        Check(sink.Last == "screen:Home" && runner.CurrentStep.ContentType == typeof(Home), "Back restored Home");
        Check(!runner.Back(), "Back at root entry returns false");
        Check(runner.CurrentStep.ContentType == typeof(Home), "current step unchanged when Back fails");

        // Stop clears state; Start can run again.
        runner.Stop();
        Check(!runner.IsRunning, "not running after Stop");
        runner.Start();
        Check(runner.IsRunning && runner.CurrentStep.ContentType == typeof(Home), "restart re-enters entry");
    }

    private static void SubFlowTests()
    {
        // Sub-flow composed by builder reference: Home --Enter--> [SubEntry --Next--> SubDetail].
        var sub = NavigationFlow.Define<Nav>("Sub")
            .Screen<SubEntry>().OnAction(Nav.Next, t => t.Frame<SubDetail>());

        var root = NavigationFlow.Define<Nav>("Root")
            .Screen<Home>().OnAction(Nav.Enter, sub)
            .Build();

        var sink = new RecordingSink();
        var runner = new NavigationFlowRunner<Nav>(root, sink);
        runner.Start();
        Check(runner.Depth == 0 && sink.Last == "screen:Home", "root entry at depth 0");

        Check(runner.Execute(Nav.Enter), "Enter sub-flow succeeds");
        Check(runner.Depth == 1, "depth increments inside sub-flow");
        Check(sink.Last == "screen:SubEntry", "sub-flow routed to its entry");
        Check(runner.ActiveFlow.Name == "Sub", "active flow is the sub-flow");

        Check(runner.Execute(Nav.Next), "advance within sub-flow");
        Check(sink.Last == "frame:SubDetail" && runner.Depth == 1, "sub-flow advanced, still depth 1");

        // Back unwinds the sub-flow's own history first...
        Check(runner.Back(), "Back within sub-flow");
        Check(sink.Last == "screen:SubEntry" && runner.Depth == 1, "back to sub entry, still in sub-flow");

        // ...then exits to the parent level, re-routing to the parent's current step.
        Check(runner.Back(), "Back exits sub-flow");
        Check(runner.Depth == 0, "depth back to 0 after leaving sub-flow");
        Check(sink.Last == "screen:Home", "re-routed to parent current step on exit");

        Check(!runner.Back(), "Back at root entry after sub-flow returns false");

        // A sub-flow instance reused by two parents is shared, and each entry runs on its own scope.
        var builtSub = NavigationFlow.Define<Nav>("Shared").Screen<SubEntry>().Build();
        var twoParents = NavigationFlow.Define<Nav>("Two")
            .Screen<Home>().OnAction(Nav.Enter, builtSub)
            .Screen<Play>().OnAction(Nav.Open, builtSub)
            .Build();
        Check(twoParents.GetStep(typeof(Home)).TryGetTransition(Nav.Enter, out var e1)
            && twoParents.GetStep(typeof(Play)).TryGetTransition(Nav.Open, out var e2)
            && ReferenceEquals(e1.SubFlow, e2.SubFlow), "shared sub-flow instance reused across parents");
    }

    private static void CycleDetectionTests()
    {
        // Direct self sub-flow reference.
        bool selfThrew = false;
        try
        {
            var self = NavigationFlow.Define<Nav>("Self");
            self.Screen<Home>().OnAction(Nav.Go, self);
            self.Build();
        }
        catch (InvalidOperationException) { selfThrew = true; }
        Check(selfThrew, "self sub-flow reference throws at build");

        // Mutual A -> B -> A cycle.
        bool mutualThrew = false;
        try
        {
            var a = NavigationFlow.Define<Nav>("A");
            var b = NavigationFlow.Define<Nav>("B");
            a.Screen<Home>().OnAction(Nav.Go, b);
            b.Screen<Play>().OnAction(Nav.Go, a);
            a.Build();
        }
        catch (InvalidOperationException) { mutualThrew = true; }
        Check(mutualThrew, "mutual sub-flow cycle throws at build");

        // A diamond (shared sub-flow reached by two paths) is NOT a cycle and must build fine.
        bool diamondOk = true;
        try
        {
            var leaf = NavigationFlow.Define<Nav>("Leaf");
            leaf.Screen<SubEntry>();
            var mid1 = NavigationFlow.Define<Nav>("Mid1");
            mid1.Screen<A>().OnAction(Nav.Go, leaf);
            var mid2 = NavigationFlow.Define<Nav>("Mid2");
            mid2.Screen<B>().OnAction(Nav.Go, leaf);
            var top = NavigationFlow.Define<Nav>("Top")
                .Screen<Home>().OnAction(Nav.Play, mid1).OnAction(Nav.Pause, mid2);
            top.Build();
        }
        catch (InvalidOperationException) { diamondOk = false; }
        Check(diamondOk, "diamond sub-flow graph builds without false cycle");

        // Intra-flow action cycle (A --go--> B --go--> A) is legal navigation, not a build cycle.
        bool intraOk = true;
        try
        {
            NavigationFlow.Define<Nav>("Intra")
                .Screen<A>().OnAction(Nav.Go, t => t.Screen<B>())
                .Screen<B>().OnAction(Nav.Go, t => t.Screen<A>())
                .Build();
        }
        catch (InvalidOperationException) { intraOk = false; }
        Check(intraOk, "intra-flow action cycle is allowed");
    }

    private static void MisuseTests()
    {
        var flow = NavigationFlow.Define<Nav>("M").Screen<Home>().Build();

        // Null-arg fail-fast.
        bool ctorThrew = false;
        try { new NavigationFlowRunner<Nav>(flow, null); } catch (ArgumentNullException) { ctorThrew = true; }
        Check(ctorThrew, "null sink throws in ctor");

        var runner = new NavigationFlowRunner<Nav>(flow, new RecordingSink());

        // Execute / Back before Start are usage errors (fail-fast).
        bool execThrew = false;
        try { runner.Execute(Nav.Play); } catch (InvalidOperationException) { execThrew = true; }
        Check(execThrew, "Execute before Start throws");

        bool backThrew = false;
        try { runner.Back(); } catch (InvalidOperationException) { backThrew = true; }
        Check(backThrew, "Back before Start throws");

        runner.Start();
        bool doubleThrew = false;
        try { runner.Start(); } catch (InvalidOperationException) { doubleThrew = true; }
        Check(doubleThrew, "double Start throws");

        // OnAction lambda that selects no target is an authoring error.
        bool noTargetThrew = false;
        try { NavigationFlow.Define<Nav>("Bad").Screen<Home>().OnAction(Nav.Go, t => t).Build(); }
        catch (InvalidOperationException) { noTargetThrew = true; }
        Check(noTargetThrew, "OnAction with no target selected throws");
    }
}
#endif
