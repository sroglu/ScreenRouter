using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// Drives a <see cref="NavigationFlow{TAction}"/>, translating declarative actions into router
    /// calls through an <see cref="INavigationSink"/>. Holds a stack of <see cref="FlowScope{TAction}"/>
    /// levels — one per active flow — so sub-flows nest with independent back-stacks, restoring the
    /// history / back-navigation the imperative router alone does not track.
    /// </summary>
    /// <typeparam name="TAction">Action key type — must match the flow's action type.</typeparam>
    public sealed class NavigationFlowRunner<TAction>
    {
        private readonly NavigationFlow<TAction> _rootFlow;
        private readonly INavigationSink _sink;
        // Last element is the top-of-stack (active) scope.
        private readonly List<FlowScope<TAction>> _scopes = new List<FlowScope<TAction>>();
        private bool _started;

        public NavigationFlowRunner(NavigationFlow<TAction> rootFlow, INavigationSink sink)
        {
            _rootFlow = rootFlow ?? throw new ArgumentNullException(nameof(rootFlow));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        /// <summary>True once <see cref="Start"/> has run and a scope is active.</summary>
        public bool IsRunning => _started && _scopes.Count > 0;

        /// <summary>How deep in sub-flows the runner is (0 = the root flow).</summary>
        public int Depth => _scopes.Count > 0 ? _scopes.Count - 1 : 0;

        /// <summary>The flow at the active level (root or a sub-flow), or null before Start.</summary>
        public NavigationFlow<TAction> ActiveFlow => Top?.Flow;

        /// <summary>The current step at the active level, or null before Start.</summary>
        public FlowStep<TAction> CurrentStep => Top?.CurrentStep;

        private FlowScope<TAction> Top => _scopes.Count > 0 ? _scopes[_scopes.Count - 1] : null;

        /// <summary>Enter the root flow at its entry step and route to it.</summary>
        public void Start()
        {
            if (_started)
                throw new InvalidOperationException("NavigationFlowRunner is already started.");
            _started = true;
            EnterFlow(_rootFlow);
        }

        /// <summary>
        /// Take an action from the current step: navigate to its target step (pushing the current one
        /// onto this level's back-stack) or enter a sub-flow. Returns false when the current step has
        /// no edge for the action.
        /// </summary>
        public bool Execute(TAction action)
        {
            RequireRunning();
            var scope = Top;
            var current = scope.CurrentStep;
            if (!current.TryGetTransition(action, out var transition))
                return false;

            if (transition.EntersSubFlow)
            {
                EnterFlow(transition.SubFlow);
            }
            else
            {
                scope.PushHistory(current);
                scope.CurrentStep = transition.TargetStep;
                Navigate(transition.TargetStep);
            }
            return true;
        }

        /// <summary>
        /// Navigate back. Unwinds this level's back-stack first; when it is empty and the runner is
        /// inside a sub-flow, exits to the parent level and re-routes to its current step. Returns
        /// false at the root entry with nothing left to unwind.
        /// </summary>
        public bool Back()
        {
            RequireRunning();
            var scope = Top;

            if (scope.TryPopHistory(out var previous))
            {
                scope.CurrentStep = previous;
                Navigate(previous);
                return true;
            }

            if (_scopes.Count > 1)
            {
                _scopes.RemoveAt(_scopes.Count - 1);   // leave the sub-flow
                var parent = Top;
                Navigate(parent.CurrentStep);
                return true;
            }

            return false;
        }

        /// <summary>Stop the runner and clear every level. It may be started again.</summary>
        public void Stop()
        {
            _scopes.Clear();
            _started = false;
        }

        private void EnterFlow(NavigationFlow<TAction> flow)
        {
            var entry = flow.EntryStep
                ?? throw new InvalidOperationException($"Flow '{flow.Name}' has no steps to enter.");
            var scope = new FlowScope<TAction>(flow) { CurrentStep = entry };
            _scopes.Add(scope);
            Navigate(entry);
        }

        private void Navigate(FlowStep<TAction> step)
        {
            if (step.IsScreen) _sink.SwitchScreen(step.ContentType);
            else _sink.OpenFrame(step.ContentType);
        }

        private void RequireRunning()
        {
            if (!IsRunning)
                throw new InvalidOperationException(
                    "NavigationFlowRunner is not running. Call Start() first.");
        }
    }
}
