using System;
using UnityEngine;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// A <see cref="System.Type"/> reference that survives Unity serialization by storing its
    /// assembly-qualified name. Use it wherever an inspector field needs to point at a screen or
    /// frame class. Resolution is cached lazily; the cache is dropped whenever Unity replays the
    /// serialized string so edited values take effect without a domain reload.
    /// </summary>
    [Serializable]
    public struct SerializableType : IEquatable<SerializableType>, ISerializationCallbackReceiver
    {
        [SerializeField] private string _qualifiedName;

        [NonSerialized] private Type _resolved;
        [NonSerialized] private bool _resolvedOnce;

        public SerializableType(Type type)
        {
            _qualifiedName = type != null ? type.AssemblyQualifiedName : string.Empty;
            _resolved = type;
            _resolvedOnce = true;
        }

        /// <summary>The stored assembly-qualified name (empty when unset).</summary>
        public string QualifiedName => _qualifiedName ?? string.Empty;

        public bool IsAssigned => !string.IsNullOrEmpty(_qualifiedName);

        /// <summary>The resolved runtime type, or null when unset / unresolvable.</summary>
        public Type Value
        {
            get
            {
                if (!_resolvedOnce)
                {
                    _resolved = string.IsNullOrEmpty(_qualifiedName) ? null : Type.GetType(_qualifiedName);
                    _resolvedOnce = true;
                }
                return _resolved;
            }
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // Force a re-resolve on next access against the freshly restored string.
            _resolvedOnce = false;
            _resolved = null;
        }

        public bool Equals(SerializableType other) =>
            string.Equals(QualifiedName, other.QualifiedName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SerializableType other && Equals(other);

        public override int GetHashCode() => QualifiedName.GetHashCode();

        public override string ToString() => Value != null ? Value.Name : "<unassigned>";

        public static bool operator ==(SerializableType a, SerializableType b) => a.Equals(b);
        public static bool operator !=(SerializableType a, SerializableType b) => !a.Equals(b);

        public static implicit operator Type(SerializableType t) => t.Value;
    }
}
