using System;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// The immutable answer a single guard hands back for an open request. Build one with the
    /// factory helpers rather than a constructor so intent reads clearly at the call site.
    /// </summary>
    public readonly struct GuardDecision : IEquatable<GuardDecision>
    {
        public GuardVerdict Verdict { get; }

        /// <summary>Only meaningful when <see cref="Verdict"/> is <see cref="GuardVerdict.Redirect"/>.</summary>
        public Type RedirectTarget { get; }

        private GuardDecision(GuardVerdict verdict, Type redirectTarget)
        {
            Verdict = verdict;
            RedirectTarget = redirectTarget;
        }

        /// <summary>Let the request through untouched.</summary>
        public static GuardDecision Pass() => new GuardDecision(GuardVerdict.Allow, null);

        /// <summary>Veto the request; nothing opens.</summary>
        public static GuardDecision Block() => new GuardDecision(GuardVerdict.Reject, null);

        /// <summary>Swap the request for a different content type.</summary>
        public static GuardDecision Reroute(Type target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target), "A reroute must name a destination type.");
            return new GuardDecision(GuardVerdict.Redirect, target);
        }

        /// <summary>Swap the request for a different content type.</summary>
        public static GuardDecision Reroute<T>() => Reroute(typeof(T));

        public bool Equals(GuardDecision other) =>
            Verdict == other.Verdict && RedirectTarget == other.RedirectTarget;

        public override bool Equals(object obj) => obj is GuardDecision other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((int)Verdict * 397) ^ (RedirectTarget?.GetHashCode() ?? 0));
    }
}
