using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Base authoring record shared by screens and frames: which content type it configures, the
    /// prefab to spawn, and how the router should pool the resulting instance.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The ContentBase subclass this definition drives.")]
        private SerializableType _contentType;

        [SerializeField]
        [Tooltip("Prefab carrying the content behaviour and an IContentRenderer component.")]
        private GameObject _prefab;

        [SerializeField]
        [Tooltip("Lifetime policy applied once the content closes.")]
        private PoolingType _pooling = PoolingType.Ephemeral;

        /// <summary>The resolved content type this definition applies to.</summary>
        public Type ContentType => _contentType.Value;

        public GameObject Prefab => _prefab;

        public PoolingType Pooling => _pooling;

        /// <summary>Populates the shared fields on a runtime-created instance (used by factories).</summary>
        protected void InitializeShared(Type contentType, GameObject prefab, PoolingType pooling)
        {
            _contentType = new SerializableType(contentType);
            _prefab = prefab;
            _pooling = pooling;
        }
    }
}
