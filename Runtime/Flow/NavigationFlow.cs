using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// An immutable, declarative graph of screen/frame steps joined by action-keyed edges. Author one
    /// with the fluent <see cref="NavigationFlow.Define{TAction}(string)"/> builder, then drive it
    /// with a <see cref="NavigationFlowRunner{TAction}"/>. Flows compose: an edge may enter a nested
    /// sub-flow that carries its own back-stack, so back navigation is local to each flow level.
    /// </summary>
    /// <typeparam name="TAction">Action key type — an enum for compile-time safety, or string.</typeparam>
    public sealed class NavigationFlow<TAction>
    {
        private readonly List<FlowStep<TAction>> _steps;
        private readonly Dictionary<Type, FlowStep<TAction>> _byType;

        /// <summary>The authored name (used in diagnostics such as cycle reports).</summary>
        public string Name { get; }

        /// <summary>The first authored step — where a runner lands when it enters this flow.</summary>
        public FlowStep<TAction> EntryStep => _steps.Count > 0 ? _steps[0] : null;

        /// <summary>Total number of distinct step nodes in the flow.</summary>
        public int StepCount => _steps.Count;

        /// <summary>The step nodes in authored order (entry first). Read-only.</summary>
        public IReadOnlyList<FlowStep<TAction>> Steps => _steps;

        internal NavigationFlow(
            string name,
            List<FlowStep<TAction>> steps,
            Dictionary<Type, FlowStep<TAction>> byType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _byType = byType ?? throw new ArgumentNullException(nameof(byType));
        }

        /// <summary>Resolve the step registered for a content type, or null when absent.</summary>
        public FlowStep<TAction> GetStep(Type contentType) =>
            _byType.TryGetValue(contentType, out var step) ? step : null;
    }

    /// <summary>Entry point for the fluent flow builder.</summary>
    public static class NavigationFlow
    {
        /// <summary>
        /// Begin a flow keyed by a typed action. An <c>enum</c> gives compile-time-safe action keys.
        /// </summary>
        public static NavigationFlowBuilder<TAction> Define<TAction>(string name) =>
            new NavigationFlowBuilder<TAction>(name);

        /// <summary>
        /// Begin a flow keyed by string action names — flexible, but not compile-time checked.
        /// </summary>
        public static NavigationFlowBuilder<string> Define(string name) =>
            new NavigationFlowBuilder<string>(name);
    }
}
