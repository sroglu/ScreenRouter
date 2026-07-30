using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>The terminal outcome of walking the guard chain for a request.</summary>
    public enum GuardResolutionKind
    {
        Approved = 0,
        Denied = 1,
        RedirectLoop = 2,
    }

    /// <summary>Result of resolving an open request against all registered guards.</summary>
    public readonly struct GuardResolution
    {
        public GuardResolutionKind Kind { get; }

        /// <summary>The type that actually won the chain (after any reroutes) when approved,
        /// or the type at which resolution gave up when a loop was detected.</summary>
        public Type Target { get; }

        private GuardResolution(GuardResolutionKind kind, Type target)
        {
            Kind = kind;
            Target = target;
        }

        public bool Approved => Kind == GuardResolutionKind.Approved;

        internal static GuardResolution Ok(Type target) => new GuardResolution(GuardResolutionKind.Approved, target);
        internal static GuardResolution No() => new GuardResolution(GuardResolutionKind.Denied, null);
        internal static GuardResolution Loop(Type at) => new GuardResolution(GuardResolutionKind.RedirectLoop, at);
    }

    /// <summary>
    /// Engine-free store and resolver for content guards. Guards registered for a type are
    /// AND-ed: the first that blocks or reroutes short-circuits the vote. A reroute restarts
    /// resolution against the destination type; a set of already-visited types is tracked so a
    /// cycle (A reroutes to B reroutes back to A) is reported instead of looping forever.
    /// </summary>
    public sealed class GuardRegistry
    {
        private readonly Dictionary<Type, List<Func<GuardDecision>>> _byType =
            new Dictionary<Type, List<Func<GuardDecision>>>();

        public void Add(Type type, Func<GuardDecision> guard)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (!_byType.TryGetValue(type, out var list))
            {
                list = new List<Func<GuardDecision>>();
                _byType[type] = list;
            }
            list.Add(guard);
        }

        /// <summary>Removes one specific guard delegate. Returns false if it was not present.</summary>
        public bool Remove(Type type, Func<GuardDecision> guard)
        {
            if (type == null || guard == null) return false;
            if (!_byType.TryGetValue(type, out var list)) return false;
            bool removed = list.Remove(guard);
            if (list.Count == 0) _byType.Remove(type);
            return removed;
        }

        /// <summary>Drops every guard registered against a type.</summary>
        public void RemoveAll(Type type)
        {
            if (type != null) _byType.Remove(type);
        }

        public bool HasAny(Type type) => type != null && _byType.ContainsKey(type);

        /// <summary>
        /// Resolves the effective target for a requested type. Follows reroutes until a type is
        /// approved, denied, or a reroute cycle is detected.
        /// </summary>
        public GuardResolution Resolve(Type requested)
        {
            if (requested == null) throw new ArgumentNullException(nameof(requested));

            var seen = new HashSet<Type>();
            var current = requested;

            while (true)
            {
                if (!seen.Add(current))
                    return GuardResolution.Loop(current);

                var decision = Vote(current);
                switch (decision.Verdict)
                {
                    case GuardVerdict.Allow:
                        return GuardResolution.Ok(current);
                    case GuardVerdict.Reject:
                        return GuardResolution.No();
                    case GuardVerdict.Redirect:
                        current = decision.RedirectTarget;
                        break;
                }
            }
        }

        /// <summary>Combines all guards for a single type; the first non-pass vote wins.</summary>
        private GuardDecision Vote(Type type)
        {
            if (!_byType.TryGetValue(type, out var list))
                return GuardDecision.Pass();

            // Snapshot the count so a guard that mutates the registry cannot corrupt iteration.
            int count = list.Count;
            for (int i = 0; i < count && i < list.Count; i++)
            {
                var decision = list[i].Invoke();
                if (decision.Verdict != GuardVerdict.Allow)
                    return decision;
            }
            return GuardDecision.Pass();
        }
    }
}
