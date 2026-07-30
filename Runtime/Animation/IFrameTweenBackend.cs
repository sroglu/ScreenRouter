using System;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Abstraction over whatever drives content open/close transitions. The router only knows this
    /// contract, so the concrete tween engine is a swap. Every method is keyed on the content
    /// TARGET so lifetime is target-bound: starting a transition on a target implicitly cancels any
    /// in-flight one on that same target, and a destroyed target must never receive a callback.
    ///
    /// A production build backed by a tween library (e.g. PrimeTween) implements this with
    /// target-bound property tweens and leaves <see cref="Tick"/> as a no-op, since such libraries
    /// self-drive. The bundled <see cref="ManualFrameTweenBackend"/> is dependency-free and is
    /// advanced by the host each frame via <see cref="Tick"/>.
    /// </summary>
    public interface IFrameTweenBackend
    {
        bool IsPlaying(ContentBase target);

        /// <summary>
        /// Plays <paramref name="preset"/> on <paramref name="target"/>, cancelling any in-flight
        /// transition on it first. A disabled or zero-length preset snaps to the end pose and
        /// invokes <paramref name="onComplete"/> synchronously.
        /// </summary>
        void Play(ContentBase target, FrameAnimationPreset preset, Action onComplete);

        /// <summary>Cancels the transition on a target without firing its completion callback.</summary>
        void Stop(ContentBase target);

        void StopAll();

        /// <summary>Advances time-driven backends. No-op for self-driving tween libraries.</summary>
        void Tick(float deltaTime);
    }
}
