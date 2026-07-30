using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// The runtime state of one active flow level (the root flow, or a sub-flow entered on top of it):
    /// the current step plus a private back-stack of the steps visited within this level. Each level
    /// keeps its own history, so <c>Back()</c> unwinds within a sub-flow before it ever exits to the
    /// parent.
    /// </summary>
    internal sealed class FlowScope<TAction>
    {
        private readonly Stack<FlowStep<TAction>> _history = new Stack<FlowStep<TAction>>();

        public NavigationFlow<TAction> Flow { get; }
        public FlowStep<TAction> CurrentStep { get; set; }

        /// <summary>Number of steps that can be unwound within this level.</summary>
        public int HistoryDepth => _history.Count;

        public FlowScope(NavigationFlow<TAction> flow)
        {
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }

        public void PushHistory(FlowStep<TAction> step) => _history.Push(step);

        public bool TryPopHistory(out FlowStep<TAction> step)
        {
            if (_history.Count == 0) { step = null; return false; }
            step = _history.Pop();
            return true;
        }
    }
}
