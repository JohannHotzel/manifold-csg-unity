// C# wrapper around the Manifold C API: one Manifold object owns one native Manifold.
// Free of UnityEngine, so it can also be used from other threads.
//
// Every operation returns a NEW Manifold and leaves the old one as it is. Each Manifold holds native
// memory: call Dispose (or put it in a using block) as soon as you no longer need it.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManifoldCSG.Native;

namespace ManifoldCSG
{
    /// <summary>
    /// Raw mesh data: interleaved vertex properties (the first 3 are the position) and triangle indices.
    /// When an instance is reused, its arrays can be longer than the data - go by the counts.
    /// </summary>
    public sealed class MeshData
    {
        public int NumProp;           // values per vertex (>= 3)
        public int NumVert;
        public int NumTri;
        public int NumRun;
        public float[] VertProps;     // NumVert * NumProp values
        public uint[] Tris;           // NumTri * 3 indices, same outward orientation as Unity (see ManifoldUnity)
        public uint[] RunIndex;       // NumRun + 1 run boundaries (in index units) ...
        public uint[] RunOriginalId;  // ... and the original id of each run, which decides its material
    }

    public sealed class Manifold : IDisposable
    {
        IntPtr _ptr;   // the native object

        Manifold(IntPtr ptr)
        {
            _ptr = ptr;
        }

        // Safety net: frees the native memory if Dispose was forgotten
        ~Manifold()
        {
            Free();
        }

        public void Dispose()
        {
            Free();
            GC.SuppressFinalize(this);
        }

        void Free()
        {
            if (_ptr == IntPtr.Zero) return;
            ManifoldNative.manifold_delete_manifold(_ptr);
            _ptr = IntPtr.Zero;
        }

        internal IntPtr Ptr
        {
            get
            {
                if (_ptr == IntPtr.Zero) throw new ObjectDisposedException("Manifold");
                return _ptr;
            }
        }

        // The C API builds every new Manifold in memory the caller allocates first
        static IntPtr Alloc()
        {
            return ManifoldNative.manifold_alloc_manifold();
        }

        // Wraps the result of a native call on this Manifold. GC.KeepAlive(this) keeps the garbage
        // collector from freeing this object while the native call was still using it.
        Manifold Result(IntPtr result)
        {
            GC.KeepAlive(this);
            return new Manifold(result);
        }

        /// <summary>
        /// Computes the result now and returns its status. Booleans are lazy: Manifold only works when a
        /// result is first used, and errors come back as a status rather than an exception. Cheap once
        /// computed - the result is kept. Every query below calls it first.
        /// Note: a C++ exception inside Manifold still takes Unity down - the C API does not catch them.
        /// </summary>
        public ManifoldError Evaluate()
        {
            ManifoldError status = ManifoldNative.manifold_status(Ptr);
            GC.KeepAlive(this);
            return status;
        }

        // --- Constructors ---------------------------------------------------------

        public static Manifold Empty()
        {
            return new Manifold(ManifoldNative.manifold_empty(Alloc()));
        }

        public static Manifold Cube(double x, double y, double z, bool center = true)
        {
            int centered = center ? 1 : 0;   // the C API takes an int
            return new Manifold(ManifoldNative.manifold_cube(Alloc(), x, y, z, centered));
        }

        public static Manifold Sphere(double radius, int segments = 0)
        {
            return new Manifold(ManifoldNative.manifold_sphere(Alloc(), radius, segments));
        }

        /// <summary>A cylinder (or cone) along +Z. radiusHigh -1 = same as radiusLow.</summary>
        public static Manifold Cylinder(double height, double radiusLow, double radiusHigh = -1, int segments = 0,
                                        bool center = false)
        {
            int centered = center ? 1 : 0;
            return new Manifold(ManifoldNative.manifold_cylinder(Alloc(), height, radiusLow, radiusHigh, segments, centered));
        }

        /// <summary>
        /// From positions + indices. merge = true welds vertices along seam edges
        /// (UV and normal seams), which are common in Unity meshes.
        /// </summary>
        public static Manifold FromMeshData(float[] vertProps, int numProp, uint[] tris, bool merge = true)
        {
            return FromMeshData(vertProps, numProp, tris, null, null, merge);
        }

        /// <summary>
        /// Like FromMeshData, but in several triangle runs that each keep their own original id through every
        /// operation, e.g. one per submesh. runIndex holds the start of every run in index units plus the end
        /// (runs + 1 values, multiples of 3), runOriginalIds one id per run in ascending order - reserve them
        /// with ReserveIds. Throws InvalidOperationException with a readable message if the mesh is unusable.
        /// </summary>
        public static Manifold FromMeshData(float[] vertProps, int numProp, uint[] tris,
                                            uint[] runIndex, uint[] runOriginalIds, bool merge = true)
        {
            if (vertProps == null) throw new ArgumentNullException("vertProps");
            if (tris == null) throw new ArgumentNullException("tris");
            if (numProp < 3 || vertProps.Length % numProp != 0)
                throw new ArgumentException("vertProps must hold numProp (>= 3) values per vertex.");
            if (tris.Length % 3 != 0) throw new ArgumentException("tris must hold 3 indices per triangle.");
            CheckRuns(runIndex, runOriginalIds, tris.Length);

            IntPtr mesh = CreateMeshGL(vertProps, numProp, tris, runIndex, runOriginalIds);
            try
            {
                if (merge)
                {
                    IntPtr merged = ManifoldNative.manifold_meshgl_merge(ManifoldNative.manifold_alloc_meshgl(), mesh);
                    ManifoldNative.manifold_delete_meshgl(mesh);
                    mesh = merged;
                }
                Manifold result = new Manifold(ManifoldNative.manifold_of_meshgl(Alloc(), mesh));
                ManifoldError status = result.Evaluate();
                if (status != ManifoldError.NoError)
                {
                    result.Dispose();
                    throw new InvalidOperationException(Describe(status));
                }
                if (result.Volume < 0)
                {
                    // Manifold does NOT report inverted triangle winding as an error
                    result.Dispose();
                    throw new InvalidOperationException("The mesh is inside out (negative volume) - flip its winding.");
                }
                return result;
            }
            finally
            {
                ManifoldNative.manifold_delete_meshgl(mesh);
            }
        }

        static IntPtr CreateMeshGL(float[] vertProps, int numProp, uint[] tris, uint[] runIndex, uint[] runOriginalIds)
        {
            UIntPtr vertexCount = (UIntPtr)(vertProps.Length / numProp);
            UIntPtr triangleCount = (UIntPtr)(tris.Length / 3);
            IntPtr memory = ManifoldNative.manifold_alloc_meshgl();
            if (runIndex == null)
                return ManifoldNative.manifold_meshgl(memory, vertProps, vertexCount, (UIntPtr)numProp, tris, triangleCount);

            // The C side copies the run arrays, so they only need to stay pinned (unmoved) during the call
            GCHandle index = GCHandle.Alloc(runIndex, GCHandleType.Pinned);
            GCHandle ids = GCHandle.Alloc(runOriginalIds, GCHandleType.Pinned);
            try
            {
                MeshGLOptions options = new MeshGLOptions();
                options.RunIndices = index.AddrOfPinnedObject();
                options.RunIndicesLength = (UIntPtr)runIndex.Length;
                options.RunOriginalIds = ids.AddrOfPinnedObject();
                options.RunOriginalIdsLength = (UIntPtr)runOriginalIds.Length;
                return ManifoldNative.manifold_meshgl_w_options(memory, vertProps, vertexCount, (UIntPtr)numProp,
                                                                tris, triangleCount, ref options);
            }
            finally
            {
                index.Free();
                ids.Free();
            }
        }

        // Wrong run data is not always caught by Manifold, so it must never reach the native side
        static void CheckRuns(uint[] runIndex, uint[] runOriginalIds, int indexCount)
        {
            if (runIndex == null && runOriginalIds == null) return;
            if (runIndex == null || runOriginalIds == null || runOriginalIds.Length == 0 ||
                runIndex.Length != runOriginalIds.Length + 1)
                throw new ArgumentException("Expected one original id per run and one more run boundary than ids.");
            if (runIndex[0] != 0 || runIndex[runIndex.Length - 1] != indexCount)
                throw new ArgumentException("Runs must start at 0 and end at the index count.");
            for (int i = 1; i < runIndex.Length; i++)
            {
                if (runIndex[i] < runIndex[i - 1] || runIndex[i] % 3 != 0)
                    throw new ArgumentException("Run boundaries must ascend in steps of whole triangles.");
            }
            for (int i = 1; i < runOriginalIds.Length; i++)
            {
                if (runOriginalIds[i] <= runOriginalIds[i - 1])
                    throw new ArgumentException("Original ids must ascend.");
            }
        }

        /// <summary>A readable explanation of a status, for warnings and the Inspector.</summary>
        public static string Describe(ManifoldError status)
        {
            switch (status)
            {
                case ManifoldError.NoError:
                    return "OK";
                case ManifoldError.NotManifold:
                    return "The mesh is not closed: every edge must belong to exactly two triangles " +
                           "(planes, quads and open meshes do not work).";
                case ManifoldError.NonFiniteVertex:
                    return "The mesh has vertices that are not finite numbers (NaN or infinity).";
                case ManifoldError.VertexIndexOutOfBounds:
                    return "A triangle references a vertex that does not exist.";
                case ManifoldError.ResultTooLarge:
                    return "The result is too large for Manifold.";
                default:
                    return "Manifold rejected the mesh: " + status;
            }
        }

        // --- Transforms (each returns a new Manifold) ------------------------------------

        public Manifold Translate(double x, double y, double z)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_translate(Alloc(), Ptr, x, y, z));
        }

        public Manifold Rotate(double xDegrees, double yDegrees, double zDegrees)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_rotate(Alloc(), Ptr, xDegrees, yDegrees, zDegrees));
        }

        public Manifold Scale(double x, double y, double z)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_scale(Alloc(), Ptr, x, y, z));
        }

        public Manifold Copy()
        {
            Evaluate();
            return Result(ManifoldNative.manifold_copy(Alloc(), Ptr));
        }

        /// <summary>
        /// Affine transform, a 3x4 matrix column by column (m[0..2] = X axis ... m[9..11] = translation).
        /// For a Unity Matrix4x4 use ManifoldUnity.Transform.
        /// </summary>
        public Manifold Transform(double[] m)
        {
            if (m == null || m.Length != 12) throw new ArgumentException("Expected 12 values (3x4, column by column).");
            Evaluate();
            return Result(ManifoldNative.manifold_transform(Alloc(), Ptr, m[0], m[1], m[2], m[3], m[4], m[5],
                                                            m[6], m[7], m[8], m[9], m[10], m[11]));
        }

        // --- Booleans -----------------------------------------------------------------

        /// <summary>This Manifold combined with another one. Lazy - see Evaluate.</summary>
        public Manifold Boolean(Manifold other, CsgOperation operation)
        {
            IntPtr result = ManifoldNative.manifold_boolean(Alloc(), Ptr, other.Ptr, operation);
            GC.KeepAlive(other);
            return Result(result);
        }

        /// <summary>Many parts in one go, e.g. a wall minus all impacts.</summary>
        public static Manifold Batch(IList<Manifold> parts, CsgOperation operation)
        {
            IntPtr list = ManifoldNative.manifold_manifold_empty_vec(ManifoldNative.manifold_alloc_manifold_vec());
            try
            {
                foreach (Manifold part in parts)
                    ManifoldNative.manifold_manifold_vec_push_back(list, part.Ptr);   // copies the part
                Manifold result = new Manifold(ManifoldNative.manifold_batch_boolean(Alloc(), list, operation));
                GC.KeepAlive(parts);
                return result;
            }
            finally
            {
                ManifoldNative.manifold_delete_manifold_vec(list);
            }
        }

        /// <summary>Splits by a cutter into what is inside it and what is outside - cheaper than two booleans.</summary>
        public void Split(Manifold cutter, out Manifold inside, out Manifold outside)
        {
            Evaluate();
            cutter.Evaluate();
            ManifoldPair pair = ManifoldNative.manifold_split(Alloc(), Alloc(), Ptr, cutter.Ptr);
            GC.KeepAlive(this);
            GC.KeepAlive(cutter);
            inside = new Manifold(pair.First);
            outside = new Manifold(pair.Second);
        }

        public Manifold TrimByPlane(double normalX, double normalY, double normalZ, double offset)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_trim_by_plane(Alloc(), Ptr, normalX, normalY, normalZ, offset));
        }

        public Manifold Hull()
        {
            Evaluate();
            return Result(ManifoldNative.manifold_hull(Alloc(), Ptr));
        }

        public Manifold Simplify(double tolerance = 0)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_simplify(Alloc(), Ptr, tolerance));
        }

        public Manifold SetTolerance(double tolerance)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_set_tolerance(Alloc(), Ptr, tolerance));
        }

        /// <summary>A copy that counts as a new original, with a fresh id for its faces.</summary>
        public Manifold AsOriginal()
        {
            Evaluate();
            return Result(ManifoldNative.manifold_as_original(Alloc(), Ptr));
        }

        /// <summary>
        /// Writes normals into the first three properties (MeshGL channels 3-5).
        /// Edges with an angle greater than minSharpAngle (degrees) stay sharp.
        /// </summary>
        public Manifold CalculateNormals(double minSharpAngle = 50)
        {
            Evaluate();
            return Result(ManifoldNative.manifold_calculate_normals(Alloc(), Ptr, 0, minSharpAngle));
        }

        /// <summary>Splits into the parts that do not touch each other (e.g. debris).</summary>
        public Manifold[] Decompose()
        {
            Evaluate();
            IntPtr list = ManifoldNative.manifold_decompose(ManifoldNative.manifold_alloc_manifold_vec(), Ptr);
            GC.KeepAlive(this);
            try
            {
                int count = (int)ManifoldNative.manifold_manifold_vec_length(list);
                Manifold[] parts = new Manifold[count];
                for (int i = 0; i < count; i++)
                    parts[i] = new Manifold(ManifoldNative.manifold_manifold_vec_get(Alloc(), list, (UIntPtr)i));
                return parts;
            }
            finally
            {
                ManifoldNative.manifold_delete_manifold_vec(list);
            }
        }

        // --- Queries --------------------------------------------------------------------

        public bool IsEmpty
        {
            get
            {
                Evaluate();
                bool empty = ManifoldNative.manifold_is_empty(Ptr) != 0;
                GC.KeepAlive(this);
                return empty;
            }
        }

        public int NumTri
        {
            get
            {
                Evaluate();
                int count = (int)ManifoldNative.manifold_num_tri(Ptr);
                GC.KeepAlive(this);
                return count;
            }
        }

        public int NumVert
        {
            get
            {
                Evaluate();
                int count = (int)ManifoldNative.manifold_num_vert(Ptr);
                GC.KeepAlive(this);
                return count;
            }
        }

        /// <summary>Vertex properties besides the position, e.g. 3 for normals, 5 for normals and UVs.</summary>
        public int NumProp
        {
            get
            {
                Evaluate();
                int count = (int)ManifoldNative.manifold_num_prop(Ptr);
                GC.KeepAlive(this);
                return count;
            }
        }

        public double Volume
        {
            get
            {
                Evaluate();
                double volume = ManifoldNative.manifold_volume(Ptr);
                GC.KeepAlive(this);
                return volume;
            }
        }

        public double SurfaceArea
        {
            get
            {
                Evaluate();
                double area = ManifoldNative.manifold_surface_area(Ptr);
                GC.KeepAlive(this);
                return area;
            }
        }

        /// <summary>The id of an original Manifold, -1 for a result of operations (and for imported meshes).</summary>
        public int OriginalId
        {
            get
            {
                Evaluate();
                int id = ManifoldNative.manifold_original_id(Ptr);
                GC.KeepAlive(this);
                return id;
            }
        }

        /// <summary>Reserves count original ids in a row and returns the first.</summary>
        public static uint ReserveIds(uint count)
        {
            return ManifoldNative.manifold_reserve_ids(count);
        }

        public static void SetCircularSegments(int segments)
        {
            ManifoldNative.manifold_set_circular_segments(segments);
        }

        // Public as ManifoldUnity.GetBounds
        internal void GetBounds(out Vec3d min, out Vec3d max)
        {
            Evaluate();
            IntPtr box = ManifoldNative.manifold_bounding_box(ManifoldNative.manifold_alloc_box(), Ptr);
            GC.KeepAlive(this);
            try
            {
                min = ManifoldNative.manifold_box_min(box);
                max = ManifoldNative.manifold_box_max(box);
            }
            finally
            {
                ManifoldNative.manifold_delete_box(box);
            }
        }

        // --- Export -----------------------------------------------------------------------

        /// <summary>
        /// Exports the mesh. Pass the MeshData from the previous call as into to reuse its arrays
        /// (they only ever grow), which avoids garbage when exporting again and again.
        /// normalIdx 0 treats the first three properties as normals and turns every part's normals along with
        /// that part - needed when normals were imported rather than calculated. -1 does that only when all
        /// parts got their normals from CalculateNormals.
        /// </summary>
        public MeshData GetMeshData(MeshData into = null, int normalIdx = -1)
        {
            Evaluate();
            IntPtr mesh;
            if (normalIdx >= 0)
                mesh = ManifoldNative.manifold_get_meshgl_w_normals(ManifoldNative.manifold_alloc_meshgl(), Ptr, normalIdx);
            else
                mesh = ManifoldNative.manifold_get_meshgl(ManifoldNative.manifold_alloc_meshgl(), Ptr);
            GC.KeepAlive(this);

            try
            {
                MeshData data = into;
                if (data == null) data = new MeshData();
                data.NumProp = (int)ManifoldNative.manifold_meshgl_num_prop(mesh);
                int props = (int)ManifoldNative.manifold_meshgl_vert_properties_length(mesh);
                int indices = (int)ManifoldNative.manifold_meshgl_tri_length(mesh);
                int runs = (int)ManifoldNative.manifold_meshgl_run_original_id_length(mesh);
                int runBounds = (int)ManifoldNative.manifold_meshgl_run_index_length(mesh);
                data.NumVert = data.NumProp > 0 ? props / data.NumProp : 0;
                data.NumTri = indices / 3;
                data.NumRun = runs;

                // The copy functions write exactly as many values as the mesh has
                EnsureLength(ref data.VertProps, props);
                EnsureLength(ref data.Tris, indices);
                EnsureLength(ref data.RunIndex, runBounds);
                EnsureLength(ref data.RunOriginalId, runs);
                if (props > 0) ManifoldNative.manifold_meshgl_vert_properties(data.VertProps, mesh);
                if (indices > 0) ManifoldNative.manifold_meshgl_tri_verts(data.Tris, mesh);
                if (runBounds > 0) ManifoldNative.manifold_meshgl_run_index(data.RunIndex, mesh);
                if (runs > 0) ManifoldNative.manifold_meshgl_run_original_id(data.RunOriginalId, mesh);
                return data;
            }
            finally
            {
                ManifoldNative.manifold_delete_meshgl(mesh);
            }
        }

        // Makes sure the array holds at least length values. It grows by half each time, so exporting
        // a growing mesh again and again rarely allocates.
        static void EnsureLength<T>(ref T[] array, int length)
        {
            if (array != null && array.Length >= length) return;
            int oldLength = 0;
            if (array != null) oldLength = array.Length;
            array = new T[Math.Max(length, oldLength * 3 / 2)];
        }
    }
}
