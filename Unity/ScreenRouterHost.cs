using System;
using System.Collections.Generic;
using UnityEngine;

namespace PFound.ScreenRouter.Unity
{
    /// <summary>
    /// Scene bootstrap that wires a <see cref="ScreenRouter"/> to a <see cref="PrefabContentRenderer"/>
    /// from inspector data, so a scene can navigate without any hand-written glue. Drop it on a
    /// GameObject, assign the screen/frame roots and a prefab per content type, then drive it in code
    /// via <see cref="Router"/> (e.g. <c>host.Router.SwitchScreen&lt;MainMenu&gt;()</c>).
    ///
    /// The router and renderer are built in <see cref="Awake"/> and exposed as properties. Override
    /// the content factory (<see cref="ContentFactory"/>) before Awake to give content instances
    /// constructor dependencies from your DI container.
    /// </summary>
    public class ScreenRouterHost : MonoBehaviour
    {
        [Tooltip("Parent transform for full-screen content. Usually a Canvas (uGUI) or a UIDocument host.")]
        [SerializeField] private Transform _screenRoot;

        [Tooltip("Parent for stacked modal frames. Order it ABOVE the screen root so frames render on top. " +
                 "Leave empty to reuse the screen root.")]
        [SerializeField] private Transform _frameRoot;

        [Tooltip("Prefab + kind per content type. ContentType is the assembly-qualified name of a ContentBase subclass.")]
        [SerializeField] private List<Binding> _bindings = new List<Binding>();

        /// <summary>The wired router. Built in <see cref="Awake"/>.</summary>
        public ScreenRouter Router { get; private set; }

        /// <summary>The wired renderer. Built in <see cref="Awake"/>.</summary>
        public PrefabContentRenderer Renderer { get; private set; }

        /// <summary>
        /// Optional content factory injected into the renderer; set it before <see cref="Awake"/>
        /// runs (e.g. from an earlier-executing installer) to resolve content through DI. When null,
        /// the renderer falls back to <see cref="Activator"/>.
        /// </summary>
        public Func<Type, ContentBase> ContentFactory { get; set; }

        protected virtual void Awake() => Build();

        /// <summary>(Re)builds the router and renderer from the current inspector data.</summary>
        public void Build()
        {
            var registry = new PrefabContentRegistry();
            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                registry.Register(b.ResolveType(), b.Prefab, b.Kind);
            }

            var frameRoot = _frameRoot ? _frameRoot : _screenRoot;
            Renderer = new PrefabContentRenderer(_screenRoot, frameRoot, registry.Resolve, ContentFactory);
            Router = new ScreenRouter(Renderer);
        }

        /// <summary>Inspector row binding a content type name to a prefab and its stack position.</summary>
        [Serializable]
        public struct Binding
        {
            [Tooltip("Assembly-qualified name of a ContentBase subclass, e.g. \"MyGame.MainMenu, MyGame\".")]
            public string ContentType;
            public GameObject Prefab;
            public ContentKind Kind;

            public Type ResolveType()
            {
                var type = Type.GetType(ContentType);
                if (type == null)
                    throw new InvalidOperationException(
                        "ScreenRouterHost: could not resolve content type '" + ContentType +
                        "'. Use the assembly-qualified name (Type.AssemblyQualifiedName).");
                return type;
            }
        }
    }
}
