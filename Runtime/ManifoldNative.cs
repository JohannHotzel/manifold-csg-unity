// P/Invoke declarations for the Manifold C API (manifoldc, Manifold v3.5.3).
// Only a subset of the API - add more functions following the same pattern,
// signatures are in bindings/c/include/manifold/manifoldc.h.
//
// Memory model of the C API: the caller allocates memory (manifold_alloc_*),
// the function constructs the object in it (placement new) and returns the same
// pointer. Release with manifold_delete_* (destructor + free).
using System;
using System.Runtime.InteropServices;

namespace ManifoldCSG.Native
{
    // The structs below mirror C structs field by field, in the same order
    [StructLayout(LayoutKind.Sequential)]
    internal struct ManifoldPair
    {
        public IntPtr First;
        public IntPtr Second;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Vec3d
    {
        public double X;
        public double Y;
        public double Z;
    }

    // ManifoldMeshGLOptions: optional arrays for manifold_meshgl_w_options. Unused ones stay null,
    // the C side reads every field.
    [StructLayout(LayoutKind.Sequential)]
    internal struct MeshGLOptions
    {
        public IntPtr RunIndices;
        public UIntPtr RunIndicesLength;
        public IntPtr RunOriginalIds;
        public UIntPtr RunOriginalIdsLength;
        public IntPtr MergeFromVert;
        public IntPtr MergeToVert;
        public UIntPtr MergeVertsLength;
        public IntPtr HalfedgeTangents;
    }

    // Internal: raw handles can crash Unity when misused, so only the wrappers in this assembly call them
    internal static class ManifoldNative
    {
        const string Lib = "manifoldc";

        // --- Memory -----------------------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_alloc_manifold();
        [DllImport(Lib)] public static extern IntPtr manifold_alloc_manifold_vec();
        [DllImport(Lib)] public static extern IntPtr manifold_alloc_meshgl();
        [DllImport(Lib)] public static extern IntPtr manifold_alloc_box();
        [DllImport(Lib)] public static extern void manifold_delete_manifold(IntPtr m);
        [DllImport(Lib)] public static extern void manifold_delete_manifold_vec(IntPtr v);
        [DllImport(Lib)] public static extern void manifold_delete_meshgl(IntPtr m);
        [DllImport(Lib)] public static extern void manifold_delete_box(IntPtr b);

        // --- Constructors -----------------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_empty(IntPtr mem);
        [DllImport(Lib)] public static extern IntPtr manifold_copy(IntPtr mem, IntPtr m);
        [DllImport(Lib)] public static extern IntPtr manifold_cube(IntPtr mem, double x, double y, double z, int center);
        [DllImport(Lib)] public static extern IntPtr manifold_sphere(IntPtr mem, double radius, int circularSegments);
        [DllImport(Lib)] public static extern IntPtr manifold_cylinder(IntPtr mem, double height, double radiusLow,
                                                                        double radiusHigh, int circularSegments, int center);
        [DllImport(Lib)] public static extern IntPtr manifold_of_meshgl(IntPtr mem, IntPtr mesh);

        // --- Transforms -------------------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_translate(IntPtr mem, IntPtr m, double x, double y, double z);
        [DllImport(Lib)] public static extern IntPtr manifold_rotate(IntPtr mem, IntPtr m, double x, double y, double z);
        [DllImport(Lib)] public static extern IntPtr manifold_scale(IntPtr mem, IntPtr m, double x, double y, double z);
        // 3x4 matrix, column by column: three basis vectors, then the translation
        [DllImport(Lib)] public static extern IntPtr manifold_transform(IntPtr mem, IntPtr m,
                                                                         double x1, double y1, double z1,
                                                                         double x2, double y2, double z2,
                                                                         double x3, double y3, double z3,
                                                                         double x4, double y4, double z4);

        // --- Booleans and friends ---------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_boolean(IntPtr mem, IntPtr a, IntPtr b, CsgOperation op);
        [DllImport(Lib)] public static extern IntPtr manifold_batch_boolean(IntPtr mem, IntPtr vec, CsgOperation op);
        [DllImport(Lib)] public static extern ManifoldPair manifold_split(IntPtr memFirst, IntPtr memSecond, IntPtr a, IntPtr b);
        [DllImport(Lib)] public static extern IntPtr manifold_trim_by_plane(IntPtr mem, IntPtr m,
                                                                             double nx, double ny, double nz, double offset);
        [DllImport(Lib)] public static extern IntPtr manifold_hull(IntPtr mem, IntPtr m);
        [DllImport(Lib)] public static extern IntPtr manifold_simplify(IntPtr mem, IntPtr m, double tolerance);
        [DllImport(Lib)] public static extern IntPtr manifold_set_tolerance(IntPtr mem, IntPtr m, double tolerance);
        [DllImport(Lib)] public static extern IntPtr manifold_decompose(IntPtr mem, IntPtr m);
        [DllImport(Lib)] public static extern IntPtr manifold_as_original(IntPtr mem, IntPtr m);
        [DllImport(Lib)] public static extern IntPtr manifold_calculate_normals(IntPtr mem, IntPtr m,
                                                                                 int normalIdx, double minSharpAngle);

        // --- Vector of manifolds ----------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_manifold_empty_vec(IntPtr mem);
        [DllImport(Lib)] public static extern UIntPtr manifold_manifold_vec_length(IntPtr vec);
        [DllImport(Lib)] public static extern IntPtr manifold_manifold_vec_get(IntPtr mem, IntPtr vec, UIntPtr idx);
        [DllImport(Lib)] public static extern void manifold_manifold_vec_push_back(IntPtr vec, IntPtr m);

        // --- Queries ------------------------------------------------------------
        [DllImport(Lib)] public static extern int manifold_is_empty(IntPtr m);
        [DllImport(Lib)] public static extern ManifoldError manifold_status(IntPtr m);
        [DllImport(Lib)] public static extern UIntPtr manifold_num_tri(IntPtr m);
        [DllImport(Lib)] public static extern UIntPtr manifold_num_vert(IntPtr m);
        [DllImport(Lib)] public static extern UIntPtr manifold_num_prop(IntPtr m);
        [DllImport(Lib)] public static extern double manifold_volume(IntPtr m);
        [DllImport(Lib)] public static extern double manifold_surface_area(IntPtr m);
        [DllImport(Lib)] public static extern int manifold_original_id(IntPtr m);
        [DllImport(Lib)] public static extern uint manifold_reserve_ids(uint n);
        [DllImport(Lib)] public static extern IntPtr manifold_bounding_box(IntPtr mem, IntPtr m);
        [DllImport(Lib)] public static extern Vec3d manifold_box_min(IntPtr box);
        [DllImport(Lib)] public static extern Vec3d manifold_box_max(IntPtr box);
        [DllImport(Lib)] public static extern void manifold_set_circular_segments(int number);

        // --- MeshGL -------------------------------------------------------------
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl(IntPtr mem, float[] vertProps, UIntPtr nVerts,
                                                                      UIntPtr nProps, uint[] triVerts, UIntPtr nTris);
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_w_options(IntPtr mem, float[] vertProps, UIntPtr nVerts,
                                                                                UIntPtr nProps, uint[] triVerts, UIntPtr nTris,
                                                                                ref MeshGLOptions options);
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_merge(IntPtr mem, IntPtr mesh);
        [DllImport(Lib)] public static extern IntPtr manifold_get_meshgl(IntPtr mem, IntPtr m);
        // normalIdx 0 = the normal slot behind the position (MeshGL channels 3-5). Brings the normals of every
        // triangle run into the result's frame, including runs whose normals were imported, not calculated.
        [DllImport(Lib)] public static extern IntPtr manifold_get_meshgl_w_normals(IntPtr mem, IntPtr m, int normalIdx);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_num_prop(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_num_vert(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_num_tri(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_vert_properties_length(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_tri_length(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_run_index_length(IntPtr mesh);
        [DllImport(Lib)] public static extern UIntPtr manifold_meshgl_run_original_id_length(IntPtr mesh);
        // These copy the data into the given (large enough) array.
        // Arrays of blittable types are pinned automatically during the call, no unsafe needed.
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_vert_properties([Out] float[] mem, IntPtr mesh);
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_tri_verts([Out] uint[] mem, IntPtr mesh);
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_run_index([Out] uint[] mem, IntPtr mesh);
        [DllImport(Lib)] public static extern IntPtr manifold_meshgl_run_original_id([Out] uint[] mem, IntPtr mesh);
    }
}
