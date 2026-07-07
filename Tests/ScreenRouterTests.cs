using System;
using System.Collections.Generic;
using PFound.ScreenRouter;

// Standalone mono/csc runner (pure C#) — parity oracle for the router core.
internal static class ScreenRouterTests
{
    private static int s_passed, s_failed;
    private static readonly List<string> Log = new List<string>();

    private static void Check(bool cond, string name)
    {
        if (cond) s_passed++;
        else { s_failed++; Console.WriteLine("  FAIL: " + name); }
    }

    private sealed class FakeRenderer : IContentRenderer
    {
        public ContentBase Create(Type t) => (ContentBase)Activator.CreateInstance(t);
        public void Show(ContentBase c) => Log.Add(c.GetType().Name + ".Show");
        public void Hide(ContentBase c) => Log.Add(c.GetType().Name + ".Hide");
        public void Destroy(ContentBase c) => Log.Add(c.GetType().Name + ".Destroy");
    }

    private abstract class TestContent : ContentBase
    {
        protected override void OnOpening() => Log.Add(N("Opening"));
        protected override void OnOpen() => Log.Add(N("Open"));
        protected override void OnClosing() => Log.Add(N("Closing"));
        protected override void OnClose() => Log.Add(N("Close"));
        protected override void OnBecomeBackground() => Log.Add(N("Background"));
        protected override void OnBecomeForeground() => Log.Add(N("Foreground"));
        private string N(string hook) => GetType().Name + "." + hook;
    }

    private sealed class ScreenA : TestContent { }
    private sealed class ScreenB : TestContent { }
    private sealed class FrameX : TestContent { }
    private sealed class Cancelled : TestContent { }
    private sealed class Redirected : TestContent { }
    private sealed class RedirectTarget : TestContent { }

    public static int Main()
    {
        // open screen: Opening -> Show -> Open
        Log.Clear();
        var r = new ScreenRouter(new FakeRenderer());
        r.SwitchScreen<ScreenA>();
        Check(string.Join(",", Log) == "ScreenA.Opening,ScreenA.Show,ScreenA.Open", "SwitchScreen open lifecycle order");
        Check(r.GetActiveScreen<ScreenA>() != null, "GetActiveScreen returns active screen");

        // switch screen closes old then opens new
        Log.Clear();
        r.SwitchScreen<ScreenB>();
        Check(string.Join(",", Log) == "ScreenA.Closing,ScreenA.Hide,ScreenA.Destroy,ScreenA.Close,ScreenB.Opening,ScreenB.Show,ScreenB.Open",
            "SwitchScreen closes previous then opens next");

        // open frame backgrounds the screen
        Log.Clear();
        r.OpenFrame<FrameX>();
        Check(string.Join(",", Log) == "ScreenB.Background,FrameX.Opening,FrameX.Show,FrameX.Open", "OpenFrame backgrounds screen, opens frame");
        Check(r.IsScreenOpen<FrameX>() && r.FrameCount == 1, "IsScreenOpen/FrameCount track the frame");

        // close frame foregrounds the screen
        Log.Clear();
        r.CloseFrame();
        Check(string.Join(",", Log) == "FrameX.Closing,FrameX.Hide,FrameX.Destroy,FrameX.Close,ScreenB.Foreground", "CloseFrame closes frame, foregrounds screen");
        Check(r.FrameCount == 0, "frame stack empty after close");

        // guard cancel: navigation is a no-op
        Log.Clear();
        var rc = new ScreenRouter(new FakeRenderer());
        rc.SwitchScreen<ScreenA>();
        Log.Clear();
        rc.RegisterGuard<Cancelled>(() => NavigationGuardResult.Cancel);
        rc.SwitchScreen<Cancelled>();
        Check(Log.Count == 0 && rc.GetActiveScreen<ScreenA>() != null, "guard Cancel blocks navigation (screen unchanged)");

        // guard redirect: lands on the redirect target
        var rr = new ScreenRouter(new FakeRenderer());
        rr.RegisterGuard<Redirected>(() => NavigationGuardResult.Redirect<RedirectTarget>());
        rr.SwitchScreen<Redirected>();
        Check(rr.GetActiveScreen<RedirectTarget>() != null && rr.GetActiveScreen<Redirected>() == null, "guard Redirect lands on the target");

        // queue opens a frame
        var rq = new ScreenRouter(new FakeRenderer());
        rq.SwitchScreen<ScreenA>();
        rq.QueueFrame<FrameX>();
        Check(rq.IsScreenOpen<FrameX>() && rq.FrameCount == 1, "QueueFrame opens the queued frame");

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"PFound.ScreenRouter: passed={s_passed} failed={s_failed}");
        return s_failed == 0 ? 0 : 1;
    }
}
