// One-call boolean operations between two Unity meshes - mesh in, mesh out, nothing else changes:
//
//     workpiece.sharedMesh = Csg.Subtract(workpiece, tool);
//
// The result lives in the local space of A, so it can be assigned to A's MeshFilter as is. It keeps
// A's submeshes; the faces from B join A's first submesh, or with separateToolFaces get one more
// submesh at the end for a material of their own. All native memory is released before returning.
// Both meshes are imported on every call - for repeated changes to one object use a CsgBody.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ManifoldCSG
{
    public static class Csg
    {
        public static Mesh Subtract(MeshFilter a, MeshFilter b, bool separateToolFaces = false, Mesh target = null)
        {
            return Apply(CsgOperation.Subtract, a, b, separateToolFaces, target);
        }

        public static Mesh Union(MeshFilter a, MeshFilter b, bool separateToolFaces = false, Mesh target = null)
        {
            return Apply(CsgOperation.Union, a, b, separateToolFaces, target);
        }

        public static Mesh Intersect(MeshFilter a, MeshFilter b, bool separateToolFaces = false, Mesh target = null)
        {
            return Apply(CsgOperation.Intersect, a, b, separateToolFaces, target);
        }

        /// <summary>
        /// Combines the meshes of two scene objects, where they stand, and returns the result in A's local
        /// space. Pass the previous result as target to overwrite it instead of creating a new mesh.
        /// Returns null and logs a warning if it cannot be built.
        /// </summary>
        public static Mesh Apply(CsgOperation operation, MeshFilter a, MeshFilter b, bool separateToolFaces = false,
                                 Mesh target = null)
        {
            if (a == null || b == null)
            {
                Debug.LogWarning("Csg." + operation + ": a MeshFilter is missing.");
                return null;
            }
            // Right to left: B out of its own space into the world, from there into A's space
            Matrix4x4 bToA = a.transform.worldToLocalMatrix * b.transform.localToWorldMatrix;
            return Apply(operation, a.sharedMesh, b.sharedMesh, bToA, separateToolFaces, target);
        }

        /// <summary>Combines two meshes, with bToA placing B in A's local space. The result uses A's space.</summary>
        public static Mesh Apply(CsgOperation operation, Mesh a, Mesh b, Matrix4x4 bToA, bool separateToolFaces = false,
                                 Mesh target = null)
        {
            Manifold shapeA = null;
            Manifold shapeB = null;
            Manifold placedB = null;
            Manifold result = null;
            try
            {
                if (!ManifoldUnity.IsUsable(bToA))
                    throw new InvalidOperationException("the placement is degenerate (a scale of 0, or numbers that are not finite)");

                // Both meshes get ids of their own per submesh, which tell their faces apart in the result
                uint firstA;
                uint firstB;
                shapeA = ManifoldUnity.FromUnityMesh(a, out firstA);
                shapeB = ManifoldUnity.FromUnityMesh(b, out firstB);
                placedB = ManifoldUnity.Transform(shapeB, bToA);
                result = shapeA.Boolean(placedB, operation);
                ManifoldError status = result.Evaluate();
                if (status != ManifoldError.NoError) throw new InvalidOperationException(Manifold.Describe(status));

                // A's submeshes stay where they are; B's faces go to submesh 0 or to one more at the end
                int toolSubMesh = 0;
                if (separateToolFaces) toolSubMesh = a.subMeshCount;
                Dictionary<uint, int> subMeshOfId = new Dictionary<uint, int>();
                for (int s = 0; s < a.subMeshCount; s++) subMeshOfId[firstA + (uint)s] = s;
                for (int s = 0; s < b.subMeshCount; s++) subMeshOfId[firstB + (uint)s] = toolSubMesh;

                int subMeshCount = a.subMeshCount;
                if (separateToolFaces) subMeshCount++;
                return ManifoldUnity.ToUnityMesh(result.GetMeshData(null, 0), subMeshOfId, subMeshCount, target);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Csg." + operation + " failed ('" + NameOf(a) + "' and '" + NameOf(b) + "'): " + e.Message);
                return null;
            }
            finally
            {
                // The native memory is freed on every way out
                if (shapeA != null) shapeA.Dispose();
                if (shapeB != null) shapeB.Dispose();
                if (placedB != null) placedB.Dispose();
                if (result != null) result.Dispose();
            }
        }

        /// <summary>
        /// Can this mesh be used for CSG - readable, made of triangles, closed, not inside out? message says
        /// what is wrong, or how many triangles it has. Imports the mesh to find out, so it is not free.
        /// </summary>
        public static bool Check(Mesh mesh, out string message)
        {
            message = ManifoldUnity.CheckReadable(mesh);
            if (message != null) return false;
            try
            {
                Manifold shape = ManifoldUnity.FromUnityMesh(mesh);
                message = "Closed mesh, " + shape.NumTri.ToString("N0") + " triangles.";
                shape.Dispose();
                return true;
            }
            catch (Exception e)
            {
                message = e.Message;
                return false;
            }
        }

        /// <summary>Forgets the meshes converted for use as tools - call it after changing such a mesh.</summary>
        public static void ClearCache()
        {
            ToolCache.Clear();
        }

        static string NameOf(Mesh mesh)
        {
            if (mesh == null) return "null";
            return mesh.name;
        }
    }
}
