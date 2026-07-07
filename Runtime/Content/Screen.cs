using System;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// A full-screen destination. Only one screen is active at a time; switching screens closes
    /// the outgoing one before the incoming one opens.
    /// </summary>
    public abstract class Screen : ContentBase
    {
    }

    /// <summary>
    /// A screen that receives a strongly-typed config value, assigned by the router before
    /// <c>OnOpening</c>. Reading <see cref="Config"/> before it is set throws.
    /// </summary>
    public abstract class Screen<TConfig> : Screen
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
