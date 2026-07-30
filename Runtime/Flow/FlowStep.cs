using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// One node in a navigation flow: a screen or frame content type plus the action-keyed edges
    /// leading out of it. A step is populated while the builder assembles the graph; once
    /// <see cref="NavigationFlowBuilder{TAction}.Build"/> returns, the graph is treated as immutable
    /// (edges are exposed read-only).
    /// </summary>
    /// <typeparam name="TAction">Action key type — an enum for compile-time safety, or string.</typeparam>
    public sealed class FlowStep<TAction>
    {
        private readonly Dictionary<TAction, FlowTransition<TAction>> _edges =
            new Dictionary<TAction, FlowTransition<TAction>>();

        /// <summary>The content type (a Screen or Frame subclass) this step lands on.</summary>
        public Type ContentType { get; }

        /// <summary>True when <see cref="ContentType"/> is a screen; false when it is a frame.</summary>
        public bool IsScreen { get; }

        /// <summary>Number of outgoing action edges.</summary>
        public int EdgeCount => _edges.Count;

        internal FlowStep(Type contentType, bool isScreen)
        {
            ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
            IsScreen = isScreen;
        }

        /// <summary>Look up the transition an action triggers from this step, if any.</summary>
        public bool TryGetTransition(TAction action, out FlowTransition<TAction> transition) =>
            _edges.TryGetValue(action, out transition);

        internal void SetEdge(TAction action, FlowTransition<TAction> transition) =>
            _edges[action] = transition;
    }

    /// <summary>
    /// The destination of an action edge: either another <see cref="FlowStep{TAction}"/> in the same
    /// flow, or the entry into a nested sub-flow (which the runner enters on its own back-stack).
    /// Exactly one of <see cref="TargetStep"/> / <see cref="SubFlow"/> is set.
    /// </summary>
    public sealed class FlowTransition<TAction>
    {
        /// <summary>The step this edge navigates to (null when the edge enters a sub-flow).</summary>
        public FlowStep<TAction> TargetStep { get; }

        /// <summary>The sub-flow this edge enters (null when the edge targets a step).</summary>
        public NavigationFlow<TAction> SubFlow { get; }

        /// <summary>True when this edge enters a nested sub-flow rather than a sibling step.</summary>
        public bool EntersSubFlow => SubFlow != null;

        private FlowTransition(FlowStep<TAction> targetStep, NavigationFlow<TAction> subFlow)
        {
            TargetStep = targetStep;
            SubFlow = subFlow;
        }

        internal static FlowTransition<TAction> ToStep(FlowStep<TAction> step) =>
            new FlowTransition<TAction>(step ?? throw new ArgumentNullException(nameof(step)), null);

        internal static FlowTransition<TAction> ToSubFlow(NavigationFlow<TAction> flow) =>
            new FlowTransition<TAction>(null, flow ?? throw new ArgumentNullException(nameof(flow)));
    }
}
