using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter.Flow
{
    /// <summary>
    /// Fluent, immutable-on-build builder for a <see cref="NavigationFlow{TAction}"/>. Nodes are
    /// added with <see cref="Screen{T}"/> / <see cref="Frame{T}"/> and wired with the action overloads
    /// on <see cref="StepChain"/>. Sub-flows are composed by referencing another builder; the whole
    /// builder graph is resolved to shared, immutable flows in <see cref="Build"/>, which throws on a
    /// circular sub-flow reference (a flow reachable from itself through sub-flow entries).
    /// </summary>
    /// <typeparam name="TAction">Action key type — an enum for compile-time safety, or string.</typeparam>
    public sealed class NavigationFlowBuilder<TAction>
    {
        private readonly string _name;
        private readonly List<StepDraft> _drafts = new List<StepDraft>();
        private readonly Dictionary<Type, StepDraft> _draftsByType = new Dictionary<Type, StepDraft>();

        internal NavigationFlowBuilder(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>Add (or reopen) a screen node keyed by content type.</summary>
        public StepChain Screen<T>() where T : class => AddNode(typeof(T), isScreen: true);

        /// <summary>Add (or reopen) a frame node keyed by content type.</summary>
        public StepChain Frame<T>() where T : class => AddNode(typeof(T), isScreen: false);

        /// <summary>Add (or reopen) a screen node by <see cref="Type"/>.</summary>
        public StepChain Screen(Type contentType) => AddNode(contentType, isScreen: true);

        /// <summary>Add (or reopen) a frame node by <see cref="Type"/>.</summary>
        public StepChain Frame(Type contentType) => AddNode(contentType, isScreen: false);

        /// <summary>
        /// Resolve the whole builder graph into immutable flows and validate it. Throws
        /// <see cref="InvalidOperationException"/> on a circular sub-flow reference.
        /// </summary>
        public NavigationFlow<TAction> Build() =>
            Resolve(
                this,
                new Dictionary<NavigationFlowBuilder<TAction>, NavigationFlow<TAction>>(),
                new HashSet<NavigationFlowBuilder<TAction>>());

        private StepChain AddNode(Type contentType, bool isScreen)
        {
            if (contentType == null) throw new ArgumentNullException(nameof(contentType));
            if (!_draftsByType.TryGetValue(contentType, out var draft))
            {
                draft = new StepDraft(contentType, isScreen);
                _drafts.Add(draft);
                _draftsByType[contentType] = draft;
            }
            return new StepChain(this, draft);
        }

        // Depth-first resolution with cycle detection over the sub-flow builder graph. `built`
        // memoises each builder to one flow instance (so a sub-flow reused by two parents is shared);
        // `path` holds the builders on the active recursion stack (re-entering one is a cycle).
        private static NavigationFlow<TAction> Resolve(
            NavigationFlowBuilder<TAction> builder,
            Dictionary<NavigationFlowBuilder<TAction>, NavigationFlow<TAction>> built,
            HashSet<NavigationFlowBuilder<TAction>> path)
        {
            if (built.TryGetValue(builder, out var already)) return already;
            if (!path.Add(builder))
                throw new InvalidOperationException(
                    $"Circular sub-flow reference detected involving flow '{builder._name}'.");

            var steps = new List<FlowStep<TAction>>();
            var byType = new Dictionary<Type, FlowStep<TAction>>();

            FlowStep<TAction> Node(Type type, bool isScreen)
            {
                if (!byType.TryGetValue(type, out var step))
                {
                    step = new FlowStep<TAction>(type, isScreen);
                    steps.Add(step);
                    byType[type] = step;
                }
                return step;
            }

            // Materialise every authored node first so intra-flow edges can point forward.
            foreach (var draft in builder._drafts)
                Node(draft.ContentType, draft.IsScreen);

            // Wire edges (auto-creating any step-edge target that was only named as a destination).
            foreach (var draft in builder._drafts)
            {
                var step = byType[draft.ContentType];
                foreach (var edge in draft.StepEdges)
                {
                    var target = Node(edge.TargetType, edge.TargetIsScreen);
                    step.SetEdge(edge.Action, FlowTransition<TAction>.ToStep(target));
                }
                foreach (var sub in draft.SubFlowEdges)
                {
                    var subFlow = sub.Flow ?? Resolve(sub.Builder, built, path);
                    step.SetEdge(sub.Action, FlowTransition<TAction>.ToSubFlow(subFlow));
                }
            }

            path.Remove(builder);
            var flow = new NavigationFlow<TAction>(builder._name, steps, byType);
            built[builder] = flow;
            return flow;
        }

        // ------------------------------------------------------------------ authoring drafts
        internal sealed class StepDraft
        {
            public readonly Type ContentType;
            public readonly bool IsScreen;
            public readonly List<StepEdge> StepEdges = new List<StepEdge>();
            public readonly List<SubFlowEdge> SubFlowEdges = new List<SubFlowEdge>();

            public StepDraft(Type contentType, bool isScreen)
            {
                ContentType = contentType;
                IsScreen = isScreen;
            }
        }

        internal readonly struct StepEdge
        {
            public readonly TAction Action;
            public readonly Type TargetType;
            public readonly bool TargetIsScreen;

            public StepEdge(TAction action, Type targetType, bool targetIsScreen)
            {
                Action = action;
                TargetType = targetType;
                TargetIsScreen = targetIsScreen;
            }
        }

        internal readonly struct SubFlowEdge
        {
            public readonly TAction Action;
            public readonly NavigationFlowBuilder<TAction> Builder;
            public readonly NavigationFlow<TAction> Flow;

            public SubFlowEdge(TAction action, NavigationFlowBuilder<TAction> builder, NavigationFlow<TAction> flow)
            {
                Action = action;
                Builder = builder;
                Flow = flow;
            }
        }

        /// <summary>
        /// Fluent handle for wiring the actions of a single step and adding further nodes. Returned
        /// by <see cref="Screen{T}"/> / <see cref="Frame{T}"/>.
        /// </summary>
        public sealed class StepChain
        {
            private readonly NavigationFlowBuilder<TAction> _owner;
            private readonly StepDraft _draft;

            internal StepChain(NavigationFlowBuilder<TAction> owner, StepDraft draft)
            {
                _owner = owner;
                _draft = draft;
            }

            /// <summary>
            /// Map an action to a transition targeting another step in this flow. The target is chosen
            /// inside the lambda with <c>t =&gt; t.Screen&lt;U&gt;()</c> / <c>t.Frame&lt;U&gt;()</c>;
            /// forward references are allowed (the node is auto-created at build).
            /// </summary>
            public StepChain OnAction(TAction action, Func<IFlowTarget, IFlowTarget> transition)
            {
                if (transition == null) throw new ArgumentNullException(nameof(transition));
                var capture = new FlowTargetCapture();
                transition(capture);
                if (capture.CapturedType == null)
                    throw new InvalidOperationException(
                        "OnAction transition did not select a Screen<T>() or Frame<T>() target.");
                _draft.StepEdges.Add(new StepEdge(action, capture.CapturedType, capture.CapturedIsScreen));
                return this;
            }

            /// <summary>Map an action to entering a nested sub-flow, composed from another builder.</summary>
            public StepChain OnAction(TAction action, NavigationFlowBuilder<TAction> subFlow)
            {
                if (subFlow == null) throw new ArgumentNullException(nameof(subFlow));
                _draft.SubFlowEdges.Add(new SubFlowEdge(action, subFlow, null));
                return this;
            }

            /// <summary>Map an action to entering an already-built sub-flow instance.</summary>
            public StepChain OnAction(TAction action, NavigationFlow<TAction> subFlow)
            {
                if (subFlow == null) throw new ArgumentNullException(nameof(subFlow));
                _draft.SubFlowEdges.Add(new SubFlowEdge(action, null, subFlow));
                return this;
            }

            /// <summary>
            /// Map an action to entering the sub-flow authored by another chain (its owning builder).
            /// A convenience so a sub-flow can be referenced mid-fluent without calling Build first.
            /// </summary>
            public StepChain OnAction(TAction action, StepChain subFlow)
            {
                if (subFlow == null) throw new ArgumentNullException(nameof(subFlow));
                return OnAction(action, subFlow._owner);
            }

            /// <summary>Add another screen node to the owning flow.</summary>
            public StepChain Screen<T>() where T : class => _owner.Screen<T>();

            /// <summary>Add another frame node to the owning flow.</summary>
            public StepChain Frame<T>() where T : class => _owner.Frame<T>();

            /// <summary>Add another screen node to the owning flow by <see cref="Type"/>.</summary>
            public StepChain Screen(Type contentType) => _owner.Screen(contentType);

            /// <summary>Add another frame node to the owning flow by <see cref="Type"/>.</summary>
            public StepChain Frame(Type contentType) => _owner.Frame(contentType);

            /// <summary>Resolve and validate the flow.</summary>
            public NavigationFlow<TAction> Build() => _owner.Build();
        }
    }

    /// <summary>
    /// Lambda target selector used by <see cref="NavigationFlowBuilder{TAction}.StepChain.OnAction(TAction, Func{IFlowTarget, IFlowTarget})"/>
    /// to name an edge's destination step by content type.
    /// </summary>
    public interface IFlowTarget
    {
        /// <summary>Target a screen step of type <typeparamref name="T"/>.</summary>
        IFlowTarget Screen<T>() where T : class;

        /// <summary>Target a frame step of type <typeparamref name="T"/>.</summary>
        IFlowTarget Frame<T>() where T : class;

        /// <summary>Target a screen step by <see cref="Type"/>.</summary>
        IFlowTarget Screen(Type contentType);

        /// <summary>Target a frame step by <see cref="Type"/>.</summary>
        IFlowTarget Frame(Type contentType);
    }

    internal sealed class FlowTargetCapture : IFlowTarget
    {
        public Type CapturedType { get; private set; }
        public bool CapturedIsScreen { get; private set; }

        public IFlowTarget Screen<T>() where T : class => Capture(typeof(T), true);
        public IFlowTarget Frame<T>() where T : class => Capture(typeof(T), false);
        public IFlowTarget Screen(Type contentType) => Capture(contentType, true);
        public IFlowTarget Frame(Type contentType) => Capture(contentType, false);

        private IFlowTarget Capture(Type type, bool isScreen)
        {
            CapturedType = type ?? throw new ArgumentNullException(nameof(type));
            CapturedIsScreen = isScreen;
            return this;
        }
    }
}
