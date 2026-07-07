using System;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// A modal overlay that lives on the router's frame stack. Beyond the shared content
    /// lifecycle it tracks a <see cref="VisibilityState"/> (the five-phase machine) and exposes a
    /// close-button hook so authored UI can request its own dismissal.
    /// </summary>
    public abstract class Frame : ContentBase
    {
        /// <summary>Current phase in the NotInitialized -> Opening -> Opened -> Closing -> Closed machine.</summary>
        public FrameState VisibilityState { get; internal set; } = FrameState.NotInitialized;

        /// <summary>True only in the fully-open resting phase.</summary>
        public bool IsOpened => VisibilityState == FrameState.Opened;

        /// <summary>True while opening or open (i.e. present and not on its way out).</summary>
        public bool IsActiveOnStack =>
            VisibilityState == FrameState.Opening || VisibilityState == FrameState.Opened;

        /// <summary>
        /// Invoked by authored close affordances (a close button, or a background click when the
        /// frame opts in). Default behaviour asks the router to close this exact instance.
        /// </summary>
        public virtual void OnCloseButtonClicked()
        {
            if (ScreenRouter.TryGetInstance(out var router))
                router.CloseFrame(this);
        }
    }

    /// <summary>
    /// A frame that receives a strongly-typed config, assigned before <c>OnOpening</c>. Reading
    /// <see cref="Config"/> before assignment throws; the router invalidates it on pooled reuse.
    /// </summary>
    public abstract class Frame<TConfig> : Frame
    {
        /// <summary>The config supplied to this open cycle. Throws if accessed before assignment.</summary>
        protected TConfig Config
        {
            get
            {
                if (!HasConfig)
                    throw new InvalidOperationException(
                        $"{GetType().Name}.Config read before it was assigned (assign happens before OnOpening).");
                return (TConfig)BoxedConfig;
            }
        }
    }
}
