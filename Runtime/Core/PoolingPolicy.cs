namespace PFound.ScreenRouter
{
    /// <summary>
    /// Central, engine-free interpretation of what each <see cref="PoolingType"/> means for the
    /// router's bookkeeping. Keeping these decisions in one pure place means the reuse rules can
    /// be asserted directly without spinning up the engine.
    /// </summary>
    public static class PoolingPolicy
    {
        /// <summary>True when the instance must be torn down as soon as it finishes closing.</summary>
        public static bool DestroyOnClose(PoolingType pooling) => pooling == PoolingType.Ephemeral;

        /// <summary>True when a closed instance is parked for later reuse rather than destroyed.</summary>
        public static bool KeepForReuse(PoolingType pooling) => pooling != PoolingType.Ephemeral;

        /// <summary>True when a parked instance is subject to the idle-timeout reaper.</summary>
        public static bool ReclaimWhenIdle(PoolingType pooling) => pooling == PoolingType.Recyclable;

        /// <summary>
        /// True when a parked instance must be discarded on a scene swap. Only the app-lifetime tier
        /// survives; scene-lifetime and idle-recyclable instances are scene-local by design.
        /// </summary>
        public static bool DropOnSceneChange(PoolingType pooling) => pooling != PoolingType.AppLifetime;

        /// <summary>
        /// True when a previously-opened instance can be revived in place. Every non-ephemeral
        /// tier reuses its instance; ephemeral content is always built fresh.
        /// </summary>
        public static bool CanRevive(PoolingType pooling) => pooling != PoolingType.Ephemeral;
    }
}
