using System;
using System.Collections.Generic;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Two-tier lookup for content definitions, kept engine-free. Runtime registrations SHADOW
    /// whatever the central config supplies for the same type; if neither tier knows the type the
    /// caller is told so. The config tier is provided as a plain lookup delegate so this helper
    /// stays free of any ScriptableObject dependency.
    /// </summary>
    public sealed class DefinitionRegistry<TDef> where TDef : class
    {
        private readonly Dictionary<Type, TDef> _runtime = new Dictionary<Type, TDef>();
        private readonly Func<Type, TDef> _fallback;

        /// <param name="configFallback">Returns the config-tier definition for a type, or null.</param>
        public DefinitionRegistry(Func<Type, TDef> configFallback)
        {
            _fallback = configFallback ?? (_ => null);
        }

        /// <summary>Adds or replaces a runtime registration for a type.</summary>
        public void Register(Type type, TDef definition)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _runtime[type] = definition;
        }

        /// <summary>Removes a runtime registration. Returns false if none was present.</summary>
        public bool Unregister(Type type)
        {
            return type != null && _runtime.Remove(type);
        }

        public bool HasRuntime(Type type) => type != null && _runtime.ContainsKey(type);

        /// <summary>True when either tier can supply a definition for the type.</summary>
        public bool CanResolve(Type type) => TryResolve(type, out _);

        public bool TryResolve(Type type, out TDef definition)
        {
            if (type == null)
            {
                definition = null;
                return false;
            }
            if (_runtime.TryGetValue(type, out definition))
                return true;
            definition = _fallback(type);
            return definition != null;
        }

        /// <summary>Resolves or throws with a clear message naming the missing type.</summary>
        public TDef Resolve(Type type)
        {
            if (!TryResolve(type, out var def))
                throw new KeyNotFoundException(
                    $"No definition registered for '{type}'. Add it to the router config or register it at runtime.");
            return def;
        }
    }
}
