// Tools converted for use: their Manifold shape with normals and an original id of their own.
// Internal helpers of CsgBody and CsgShape.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ManifoldCSG
{
    internal sealed class ToolEntry : IDisposable
    {
        // The kinds of faces a tool can leave in a body. A Material is a kind of its own.
        internal static readonly object Interior = new object();
        internal static readonly object Outside = new object();

        public Manifold Shape;

        object _firstKind;
        Dictionary<object, Manifold> _variants;

        public ToolEntry(Manifold shape)
        {
            // Every face of the tool has to carry one id that belongs to this tool alone. Imported meshes
            // report no id of their own (-1), so they get a fresh one here.
            if (shape.OriginalId < 0)
            {
                Manifold original = shape.AsOriginal();
                shape.Dispose();
                shape = original;
            }
            Shape = shape;
        }

        /// <summary>
        /// The shape with an id that stands for this kind of face only. A body maps each id to one material,
        /// so the same tool used once for Subtract (interior) and once for Union (outside) needs two ids.
        /// The first kind gets the shape itself; any other kind a copy with a fresh id, made once.
        /// </summary>
        public Manifold ForKind(object kind)
        {
            if (_firstKind == null) _firstKind = kind;
            if (_firstKind == kind) return Shape;

            if (_variants == null) _variants = new Dictionary<object, Manifold>();
            Manifold variant;
            if (!_variants.TryGetValue(kind, out variant))
            {
                variant = Shape.AsOriginal();
                _variants.Add(kind, variant);
            }
            return variant;
        }

        public void Dispose()
        {
            if (Shape != null) Shape.Dispose();
            Shape = null;
            if (_variants != null)
            {
                foreach (Manifold variant in _variants.Values) variant.Dispose();
            }
            _variants = null;
        }
    }

    // Meshes used as tools, converted once. A ConditionalWeakTable does not keep its keys alive:
    // when a mesh is gone, its entry goes too (and the native memory with the garbage collector).
    internal static class ToolCache
    {
        static ConditionalWeakTable<Mesh, ToolEntry> _entries = new ConditionalWeakTable<Mesh, ToolEntry>();
        static ConditionalWeakTable<Mesh, string> _errors = new ConditionalWeakTable<Mesh, string>();

        public static ToolEntry Of(Mesh mesh, out string error)
        {
            ToolEntry entry;
            if (_entries.TryGetValue(mesh, out entry))
            {
                error = null;
                return entry;
            }
            if (_errors.TryGetValue(mesh, out error)) return null;

            try
            {
                entry = new ToolEntry(ManifoldUnity.FromUnityMesh(mesh));
                _entries.Add(mesh, entry);
                return entry;
            }
            catch (Exception e)
            {
                error = e.Message;
                _errors.Add(mesh, error);
                return null;
            }
        }

        public static void Clear()
        {
            _entries = new ConditionalWeakTable<Mesh, ToolEntry>();
            _errors = new ConditionalWeakTable<Mesh, string>();
        }
    }
}
