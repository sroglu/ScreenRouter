using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// The engine-free description of how a frame tolerates other frames stacking on top of it.
    /// A frame that <see cref="Blocks"/> stacking rejects everything except the types named in
    /// its <see cref="Whitelist"/>. The <see cref="ForcesOnTop"/> flag belongs to the INCOMING
    /// frame and, when set, bypasses whatever the current top says.
    /// </summary>
    public readonly struct StackingRules
    {
        /// <summary>When true, this frame refuses to have others stacked over it unless whitelisted.</summary>
        public bool Blocks { get; }

        private readonly HashSet<Type> _whitelist;

        /// <summary>Incoming types explicitly permitted even when <see cref="Blocks"/> is set.</summary>
        public IReadOnlyCollection<Type> Whitelist => _whitelist;

        /// <summary>When true this frame ignores the current top's rules and always stacks.</summary>
        public bool ForcesOnTop { get; }

        public StackingRules(bool blocks, IEnumerable<Type> whitelist, bool forcesOnTop)
        {
            Blocks = blocks;
            ForcesOnTop = forcesOnTop;
            _whitelist = whitelist == null ? new HashSet<Type>() : new HashSet<Type>(whitelist);
        }

        /// <summary>Would this frame (acting as the current top) let <paramref name="incoming"/> land on it?</summary>
        public bool PermitsOnTop(Type incoming)
        {
            if (!Blocks) return true;
            return _whitelist != null && _whitelist.Contains(incoming);
        }
    }

    /// <summary>Pure decision helper for whether a new frame may join the stack.</summary>
    public static class StackingEvaluator
    {
        /// <summary>
        /// Decides whether <paramref name="incoming"/> may open given the current top's rules.
        /// The incoming frame's own force-on-top flag wins outright; an empty stack always accepts.
        /// </summary>
        /// <param name="topRules">Rules of the frame currently on top, or null when the stack is empty.</param>
        /// <param name="incoming">The type being opened.</param>
        /// <param name="incomingForcesOnTop">The incoming frame's force-on-top flag.</param>
        public static bool CanStack(StackingRules? topRules, Type incoming, bool incomingForcesOnTop)
        {
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));
            if (incomingForcesOnTop) return true;
            if (topRules == null) return true;
            return topRules.Value.PermitsOnTop(incoming);
        }
    }
}
