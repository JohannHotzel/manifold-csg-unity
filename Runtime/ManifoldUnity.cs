// Bridge between UnityEngine.Mesh and Manifold.
// Call FromUnityMesh and ToUnityMesh on the main thread only (Unity's mesh API);
// everything in between (booleans, GetMeshData) may also run on other threads.
using System;
using System.Collections.Generic;
using ManifoldCSG.Native;
using UnityEngine;
using UnityEngine.Rendering;

namespace ManifoldCSG
{
    public static class ManifoldUnity
    {
        // Vertex layout in and out of Manifold: position (0-2), normal (3-5), UV (6-7).
        // CalculateNormals always writes into 3-5, so the UVs sit behind the normals. Manifold carries
        // them through every boolean and interpolates them on split triangles, which keeps textures in
        // place. The layout is byte for byte a Unity vertex buffer with position, normal and UV0.
        const int NumPropWithUV = 8;
        const int NumPropWithNormals = 6;
        const int UVOffset = 6;

        /// <summary>
        /// Unity mesh -> Manifold with normals (and UVs if the mesh has any). The whole mesh becomes one
        /// original. Welds seams automatically; UV seams stay intact.
        /// Normals: edges sharper than sharpAngle (degrees) stay hard - or keepNormals uses the mesh's own
        /// normals as they are (falls back to calculating them if the mesh has none).
        /// Throws InvalidOperationException with a readable message if the mesh cannot be used.
        /// </summary>
        public static Manifold FromUnityMesh(Mesh mesh, float sharpAngle = 50f, bool keepNormals = false)
        {
            uint firstId;
            return Import(mesh, sharpAngle, keepNormals, false, out firstId);
        }

        /// <summary>
        /// Like FromUnityMesh, but every submesh keeps an original id of its own: submesh s has
        /// firstSubMeshId + s, in every result this body ends up in. That is how materials survive.
        /// </summary>
        public static Manifold FromUnityMesh(Mesh mesh, out uint firstSubMeshId, float sharpAngle = 50f,
                                             bool keepNormals = false)
        {
            return Import(mesh, sharpAngle, keepNormals, true, out firstSubMeshId);
        }

        static Manifold Import(Mesh mesh, float sharpAngle, bool keepNormals, bool idPerSubMesh, out uint firstId)
        {
            // Without normals in the mesh there is nothing to keep
            if (mesh != null && !mesh.HasVertexAttribute(VertexAttribute.Normal)) keepNormals = false;

            float[] vertices;
            int numProp;
            uint[] triangles;
            uint[] subMeshStarts;
            ReadMesh(mesh, keepNormals, out vertices, out numProp, out triangles, out subMeshStarts);

            uint[] runIndex = null;
            uint[] ids = null;
            firstId = 0;
            if (idPerSubMesh)
            {
                firstId = Manifold.ReserveIds((uint)mesh.subMeshCount);
                ids = new uint[mesh.subMeshCount];
                for (int s = 0; s < ids.Length; s++) ids[s] = firstId + (uint)s;
                runIndex = subMeshStarts;
            }

            Manifold imported = Manifold.FromMeshData(vertices, numProp, triangles, runIndex, ids, true);
            if (keepNormals) return imported;
            Manifold withNormals = imported.CalculateNormals(sharpAngle);
            imported.Dispose();
            return withNormals;
        }

        // Why a mesh cannot be read for Manifold, or null if it can. Whether it is closed only shows on import.
        internal static string CheckReadable(Mesh mesh)
        {
            if (mesh == null) return "There is no mesh.";
            if (!mesh.isReadable) return "'" + mesh.name + "' is not readable - enable Read/Write in its import settings.";
            if (mesh.vertexCount == 0) return "'" + mesh.name + "' has no vertices.";
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    return "'" + mesh.name + "' has a submesh made of " + mesh.GetTopology(s) + " - only triangles work.";
            }
            return null;
        }

        // Reads a readable mesh: positions, normals and UVs as far as needed, all submeshes one after another,
        // and where each submesh starts (in index units, plus the end). The layout is x, y, z, nx, ny, nz, u, v
        // with UVs; x, y, z, nx, ny, nz when only normals are kept; x, y, z otherwise.
        // Unity's winding (clockwise, left-handed) gives algebraically the same outward orientation as
        // Manifold, so nothing is flipped. FromMeshData throws on negative volume if a mesh is inverted after all.
        static void ReadMesh(Mesh mesh, bool keepNormals, out float[] vertices, out int numProp,
                             out uint[] triangles, out uint[] subMeshStarts)
        {
            string problem = CheckReadable(mesh);
            if (problem != null) throw new InvalidOperationException(problem);

            Vector3[] positions = mesh.vertices;
            Vector2[] uvs = null;
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) uvs = mesh.uv;

            // Kept normals need the slot anyway. Otherwise the mesh's own normals only keep the slot apart until
            // CalculateNormals overwrites it: Manifold merges vertices whose values are all equal, and with an
            // empty slot two faces meeting at a hard edge with the same UVs would end up sharing one normal.
            Vector3[] normals = null;
            if (mesh.HasVertexAttribute(VertexAttribute.Normal) && (keepNormals || uvs != null)) normals = mesh.normals;

            if (uvs != null) numProp = NumPropWithUV;
            else if (normals != null) numProp = NumPropWithNormals;
            else numProp = 3;

            vertices = new float[positions.Length * numProp];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z))
                    throw new InvalidOperationException("'" + mesh.name + "' has vertices that are not finite numbers (NaN or infinity).");

                int start = i * numProp;
                vertices[start] = p.x;
                vertices[start + 1] = p.y;
                vertices[start + 2] = p.z;
                if (normals != null)
                {
                    vertices[start + 3] = normals[i].x;
                    vertices[start + 4] = normals[i].y;
                    vertices[start + 5] = normals[i].z;
                }
                if (uvs != null)
                {
                    vertices[start + UVOffset] = uvs[i].x;
                    vertices[start + UVOffset + 1] = uvs[i].y;
                }
            }

            List<uint> allTriangles = new List<uint>();
            List<int> subMeshTriangles = new List<int>();
            subMeshStarts = new uint[mesh.subMeshCount + 1];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                subMeshStarts[s] = (uint)allTriangles.Count;
                mesh.GetTriangles(subMeshTriangles, s);
                foreach (int index in subMeshTriangles) allTriangles.Add((uint)index);
            }
            subMeshStarts[mesh.subMeshCount] = (uint)allTriangles.Count;
            triangles = allTriangles.ToArray();
        }

        /// <summary>A copy of the manifold, moved by a Unity matrix (e.g. worldToLocal * localToWorld). Mirroring is allowed.</summary>
        public static Manifold Transform(Manifold manifold, Matrix4x4 m)
        {
            // Manifold wants the upper 3x4 part, column by column
            double[] columns =
            {
                m.m00, m.m10, m.m20,
                m.m01, m.m11, m.m21,
                m.m02, m.m12, m.m22,
                m.m03, m.m13, m.m23,
            };
            return manifold.Transform(columns);
        }

        // A matrix with NaN, infinity or a scale of 0 must never reach Manifold
        internal static bool IsUsable(Matrix4x4 m)
        {
            for (int i = 0; i < 16; i++)
            {
                if (!float.IsFinite(m[i])) return false;
            }
            return Mathf.Abs(m.determinant) > 1e-12f;
        }

        /// <summary>Axis-aligned bounds in the manifold's own space.</summary>
        public static Bounds GetBounds(Manifold manifold)
        {
            Vec3d min;
            Vec3d max;
            manifold.GetBounds(out min, out max);
            Bounds bounds = new Bounds();
            bounds.SetMinMax(new Vector3((float)min.X, (float)min.Y, (float)min.Z),
                             new Vector3((float)max.X, (float)max.Y, (float)max.Z));
            return bounds;
        }

        static readonly VertexAttributeDescriptor[] PositionNormalUV =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
        };
        static readonly VertexAttributeDescriptor[] PositionNormal =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
        };
        static readonly VertexAttributeDescriptor[] PositionOnly =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        };

        // Reused between calls (main thread only), so converting creates no garbage
        static float[] _vertexBuffer;
        static uint[] _indexBuffer;
        static int[] _subMeshSize = new int[2];
        static int[] _writePosition = new int[2];

        /// <summary>
        /// MeshData -> Unity mesh. subMeshOfId says which submesh the faces of each original id go to
        /// (e.g. outer surface = 0, cut surface = 1); ids that are not in it go to submesh 0.
        /// Normals and UVs are used if present. Pass target to overwrite an existing mesh.
        /// Faces from a body without UVs (e.g. a Manifold.Sphere tool) get UV (0, 0).
        /// </summary>
        public static Mesh ToUnityMesh(MeshData data, Dictionary<uint, int> subMeshOfId = null, int subMeshCount = 1,
                                       Mesh target = null)
        {
            Mesh mesh = target;
            if (mesh == null) mesh = new Mesh();
            mesh.Clear();

            // Manifold's interleaved x, y, z, nx, ny, nz (, u, v) is byte for byte a Unity vertex buffer
            // with position, normal (and UV0), so it goes in as is. Other property counts are packed first.
            int vertexCount = data.NumVert;
            int numProp = data.NumProp;
            bool hasNormals = numProp >= NumPropWithNormals;
            bool hasUV = numProp >= NumPropWithUV;

            int stride;
            VertexAttributeDescriptor[] layout;
            if (hasUV)
            {
                stride = NumPropWithUV;
                layout = PositionNormalUV;
            }
            else if (hasNormals)
            {
                stride = NumPropWithNormals;
                layout = PositionNormal;
            }
            else
            {
                stride = 3;
                layout = PositionOnly;
            }

            float[] vertices = data.VertProps;
            if (numProp != stride)
            {
                EnsureLength(ref _vertexBuffer, vertexCount * stride);
                vertices = _vertexBuffer;
                for (int i = 0; i < vertexCount; i++)
                    Array.Copy(data.VertProps, i * numProp, vertices, i * stride, stride);
            }
            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetVertexBufferData(vertices, 0, 0, vertexCount * stride);

            // One index range per submesh. Runs are ranges of triangles with one original id;
            // with a single submesh Manifold's index array is already in the right order.
            int subMeshes = Math.Max(1, subMeshCount);
            int indexCount = data.NumTri * 3;
            EnsureLength(ref _subMeshSize, subMeshes);
            Array.Clear(_subMeshSize, 0, subMeshes);
            uint[] indices = data.Tris;
            if (subMeshes == 1 || subMeshOfId == null || data.NumRun == 0)
            {
                _subMeshSize[0] = indexCount;
            }
            else
            {
                // First count how many indices each submesh gets ...
                for (int run = 0; run < data.NumRun; run++)
                {
                    int length = (int)(data.RunIndex[run + 1] - data.RunIndex[run]);
                    _subMeshSize[SubMeshOf(data, run, subMeshOfId, subMeshes)] += length;
                }

                // ... then copy every run behind the ones already in its submesh
                EnsureLength(ref _indexBuffer, indexCount);
                indices = _indexBuffer;
                EnsureLength(ref _writePosition, subMeshes);
                _writePosition[0] = 0;
                for (int s = 1; s < subMeshes; s++) _writePosition[s] = _writePosition[s - 1] + _subMeshSize[s - 1];
                for (int run = 0; run < data.NumRun; run++)
                {
                    int start = (int)data.RunIndex[run];
                    int length = (int)data.RunIndex[run + 1] - start;
                    int s = SubMeshOf(data, run, subMeshOfId, subMeshes);
                    Array.Copy(data.Tris, start, indices, _writePosition[s], length);
                    _writePosition[s] += length;
                }
            }

            MeshUpdateFlags fast = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;
            mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indexCount, fast);
            mesh.subMeshCount = subMeshes;
            int subMeshStart = 0;
            for (int s = 0; s < subMeshes; s++)
            {
                mesh.SetSubMesh(s, new SubMeshDescriptor(subMeshStart, _subMeshSize[s]), fast);
                subMeshStart += _subMeshSize[s];
            }

            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int SubMeshOf(MeshData data, int run, Dictionary<uint, int> subMeshOfId, int subMeshCount)
        {
            int subMesh;
            if (!subMeshOfId.TryGetValue(data.RunOriginalId[run], out subMesh)) subMesh = 0;
            return Mathf.Clamp(subMesh, 0, subMeshCount - 1);
        }

        // Makes sure the buffer holds at least length values; it grows by half so it rarely has to
        static void EnsureLength<T>(ref T[] buffer, int length)
        {
            if (buffer != null && buffer.Length >= length) return;
            int oldLength = 0;
            if (buffer != null) oldLength = buffer.Length;
            buffer = new T[Math.Max(length, oldLength * 3 / 2)];
        }
    }
}
