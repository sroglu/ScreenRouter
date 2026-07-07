using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter.Unity
{
    /// <summary>
    /// Where a content instance lives in the hierarchy. A <see cref="ContentKind.Screen"/> is a
    /// full-screen backdrop parented under the screen root; a <see cref="ContentKind.Frame"/> is a
    /// modal that stacks above it under the frame root. Matches the router's screen-vs-frame model
    /// (one active screen, a stack of frames on top).
    /// </summary>
    public enum ContentKind
    {
        Screen,
        Frame
    }

    /// <summary>
    /// The prefab to instantiate for a content type and where it belongs in the stack. Returned by a
    /// resolver so the renderer stays agnostic of how bindings are stored (code registry, inspector
    /// list, addressables, a DI container...).
    /// </summary>
    public readonly struct PrefabBinding
    {
        public readonly GameObject Prefab;
        public readonly ContentKind Kind;

        public PrefabBinding(GameObject prefab, ContentKind kind)
        {
            Prefab = prefab;
            Kind = kind;
        }
    }

    /// <summary>
    /// Optional hook a prefab's root component can implement to receive its owning
    /// <see cref="ContentBase"/> the moment the view is instantiated (before it is shown). Lets a
    /// uGUI or UI-Toolkit prefab drive itself from the content's state without the renderer knowing
    /// anything about the concrete UI. Purely optional — prefabs without it just get shown/hidden.
    /// </summary>
    public interface IContentPresenter
    {
        void Bind(ContentBase content);
    }

    /// <summary>
    /// A code-driven prefab-per-content-type map usable directly as a resolver
    /// (<c>renderer = new PrefabContentRenderer(root, frameRoot, registry.Resolve)</c>). Register is
    /// chainable. Resolving an unregistered type is a configuration error and fails fast.
    /// </summary>
    public sealed class PrefabContentRegistry
    {
        private readonly Dictionary<Type, PrefabBinding> _bindings = new Dictionary<Type, PrefabBinding>();

        public PrefabContentRegistry Register<T>(GameObject prefab, ContentKind kind) where T : ContentBase
            => Register(typeof(T), prefab, kind);

        public PrefabContentRegistry Register(Type contentType, GameObject prefab, ContentKind kind)
        {
            _bindings[contentType] = new PrefabBinding(prefab, kind);
            return this;
        }

        public PrefabBinding Resolve(Type contentType)
        {
            if (_bindings.TryGetValue(contentType, out var binding)) return binding;
            throw new InvalidOperationException(
                "PrefabContentRegistry: no prefab registered for content type '" + contentType.FullName +
                "'. Register it before navigating to it.");
        }
    }
}
