// CsgBody: the three operations, materials, normals, reset and empty bodies.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ManifoldCSG.Tests
{
    public class CsgBodyTests : CsgTestBase
    {
        [Test]
        public void Subtract_RemovesExactlyTheToolVolume()
        {
            CsgBody body = Body();
            CsgShape sphere = Shape();
            double toolVolume = sphere.Shape.Volume * 0.5 * 0.5 * 0.5;

            bool done = body.Subtract(sphere, At(Vector3.zero, 0.5f));

            Assert.IsTrue(done, body.LastError);
            Assert.AreEqual(1 - toolVolume, body.Volume, 1e-4);
            Assert.AreEqual(1, body.Operations);
        }

        [Test]
        public void ThreeOperations_AddUpLikeSets()
        {
            CsgShape sphere = Shape();
            double toolVolume = sphere.Shape.Volume * 0.8 * 0.8 * 0.8;
            CsgBody subtracted = Body();
            CsgBody united = Body(new Vector3(5, 0, 0));
            CsgBody intersected = Body(new Vector3(10, 0, 0));

            Assert.IsTrue(subtracted.Subtract(sphere, SameSpotOn(subtracted)));
            Assert.IsTrue(united.Union(sphere, SameSpotOn(united)));
            Assert.IsTrue(intersected.Intersect(sphere, SameSpotOn(intersected)));

            Assert.Greater(intersected.Volume, 0);
            Assert.Less(intersected.Volume, toolVolume);
            Assert.AreEqual(1, subtracted.Volume + intersected.Volume, 1e-4, "A - B and A ^ B make up A");
            Assert.AreEqual(1 + toolVolume, united.Volume + intersected.Volume, 1e-4, "A + B and A ^ B make up A and B");
        }

        // The same tool at the same spot of cubes that stand apart
        static Matrix4x4 SameSpotOn(CsgBody body)
        {
            Vector3 position = body.transform.position + new Vector3(0.4f, 0.3f, 0.2f);
            return Matrix4x4.TRS(position, Quaternion.Euler(10, 20, 30), 0.8f * Vector3.one);
        }

        [Test]
        public void CutFaces_GetTheInteriorMaterial()
        {
            CsgBody body = Body();
            Material outside = body.GetComponent<MeshRenderer>().sharedMaterial;
            Material interior = NewMaterial("Interior");
            body.InteriorMaterial = interior;

            body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));

            CollectionAssert.AreEqual(new Material[] { outside, interior }, body.GetComponent<MeshRenderer>().sharedMaterials);
            Assert.AreEqual(2, MeshOf(body).subMeshCount);
            Assert.Greater(Triangles(MeshOf(body), 1), 0);
        }

        [Test]
        public void WithoutInteriorMaterial_CutFacesShowTheOutside()
        {
            CsgBody body = Body();
            Material outside = body.GetComponent<MeshRenderer>().sharedMaterial;

            body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));

            CollectionAssert.AreEqual(new Material[] { outside, outside }, body.GetComponent<MeshRenderer>().sharedMaterials);
        }

        [Test]
        public void InteriorMaterial_CanChangeLater()
        {
            CsgBody body = Body();
            body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));
            Material interior = NewMaterial("Interior");

            body.InteriorMaterial = interior;

            Assert.AreEqual(interior, body.GetComponent<MeshRenderer>().sharedMaterials[1]);
        }

        [Test]
        public void Union_ToolFacesShowTheOutside()
        {
            CsgBody body = Body();
            body.InteriorMaterial = NewMaterial("Interior");

            body.Union(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));

            Assert.AreEqual(1, MeshOf(body).subMeshCount);
            Assert.AreEqual(1, body.GetComponent<MeshRenderer>().sharedMaterials.Length);
        }

        [Test]
        public void ToolMaterial_WinsInEveryOperation()
        {
            CsgBody body = Body();
            body.InteriorMaterial = NewMaterial("Interior");
            Material paint = NewMaterial("Paint");
            CsgShape sphere = Shape();
            sphere.material = paint;

            body.Subtract(sphere, At(new Vector3(0.5f, 0, 0), 0.6f));
            body.Union(sphere, At(new Vector3(-0.5f, 0, 0), 0.6f));

            Material[] materials = body.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(2, materials.Length);
            Assert.AreEqual(paint, materials[1]);
        }

        [Test]
        public void SameTool_SubtractThenUnion_KeepsTheirFacesApart()
        {
            CsgBody body = Body();
            body.InteriorMaterial = NewMaterial("Interior");
            CsgShape sphere = Shape();
            body.Subtract(sphere, At(new Vector3(0.5f, 0, 0), 0.6f));
            int interiorTriangles = Triangles(MeshOf(body), 1);
            int outsideTriangles = Triangles(MeshOf(body), 0);

            body.Union(sphere, At(new Vector3(-0.5f, 0, 0), 0.6f));

            Assert.AreEqual(interiorTriangles, Triangles(MeshOf(body), 1), "the added sphere must not show the interior");
            Assert.Greater(Triangles(MeshOf(body), 0), outsideTriangles);
        }

        [Test]
        public void MeshWithTwoMaterials_KeepsBothAfterACut()
        {
            // A cube mesh whose first three faces are submesh 0 and the other three submesh 1
            GameObject cube = Cube();
            Mesh mesh = CopyOf(MeshOf(cube));
            int[] triangles = mesh.triangles;
            int[] firstHalf = new int[18];
            int[] secondHalf = new int[triangles.Length - 18];
            System.Array.Copy(triangles, 0, firstHalf, 0, 18);
            System.Array.Copy(triangles, 18, secondHalf, 0, secondHalf.Length);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(firstHalf, 0);
            mesh.SetTriangles(secondHalf, 1);
            cube.GetComponent<MeshFilter>().sharedMesh = mesh;

            Material first = NewMaterial("First");
            Material second = NewMaterial("Second");
            Material interior = NewMaterial("Interior");
            cube.GetComponent<MeshRenderer>().sharedMaterials = new Material[] { first, second };
            List<Vector3>[] facesBefore = { FaceNormals(mesh, 0), FaceNormals(mesh, 1) };
            CsgBody body = cube.AddComponent<CsgBody>();
            body.InteriorMaterial = interior;

            // Near a corner, so the cut touches three faces of both submeshes
            Assert.IsTrue(body.Subtract(Shape(), At(new Vector3(0.45f, 0.42f, 0.47f), 0.6f)));

            CollectionAssert.AreEqual(new Material[] { first, second, interior }, cube.GetComponent<MeshRenderer>().sharedMaterials);
            Mesh result = MeshOf(body);
            for (int s = 0; s < 2; s++)
            {
                Assert.Greater(Triangles(result, s), 0);
                foreach (Vector3 normal in FaceNormals(result, s))
                    Assert.IsTrue(Contains(facesBefore[s], normal), "a triangle of submesh " + s + " lies on a face of the other submesh");
            }
        }

        // The direction every triangle of a submesh faces
        static List<Vector3> FaceNormals(Mesh mesh, int subMesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.GetTriangles(subMesh);
            List<Vector3> normals = new List<Vector3>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]];
                Vector3 b = vertices[indices[i + 1]];
                Vector3 c = vertices[indices[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude > 1e-12f) normals.Add(normal.normalized);
            }
            return normals;
        }

        static bool Contains(List<Vector3> directions, Vector3 direction)
        {
            foreach (Vector3 d in directions)
            {
                if (Vector3.Dot(d, direction) > 0.999f) return true;
            }
            return false;
        }

        [Test]
        public void CutFaceNormals_PointIntoTheHole([Values] NormalMode mode)
        {
            CsgBody body = Body();
            body.transform.rotation = Quaternion.Euler(0, 30, 0);
            body.normals = mode;
            Vector3 toolCenter = body.transform.TransformPoint(new Vector3(0.5f, 0.1f, -0.05f));
            Matrix4x4 placement = Matrix4x4.TRS(toolCenter, Quaternion.Euler(37, 11, 59), new Vector3(0.6f, 0.5f, 0.7f));

            Assert.IsTrue(body.Subtract(Shape(), placement));

            Mesh mesh = MeshOf(body);
            Vector3 center = body.transform.InverseTransformPoint(toolCenter);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] cutFaces = mesh.GetTriangles(1);
            foreach (int i in cutFaces)
            {
                Assert.AreEqual(1, normals[i].magnitude, 1e-3);
                Assert.Greater(Vector3.Dot(normals[i], center - vertices[i]), 0, "a cut face normal points into the material");
            }
            Assert.Greater(cutFaces.Length, 30);
        }

        [Test]
        public void KeepImportedNormals_SurviveTheCut()
        {
            CsgBody body = BodyWithAllNormalsUp();
            body.normals = NormalMode.KeepImported;

            Assert.IsTrue(body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f)));

            Mesh result = MeshOf(body);
            Vector3[] normals = result.normals;
            foreach (int i in result.GetTriangles(0))
                Assert.Greater(Vector3.Dot(normals[i], Vector3.up), 0.999f);
        }

        [Test]
        public void RecalculatedNormals_FollowTheGeometry()
        {
            CsgBody body = BodyWithAllNormalsUp();

            Assert.IsTrue(body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f)));

            Mesh result = MeshOf(body);
            Vector3[] normals = result.normals;
            int sideways = 0;
            foreach (int i in result.GetTriangles(0))
            {
                if (Vector3.Dot(normals[i], Vector3.up) < 0.5f) sideways++;
            }
            Assert.Greater(sideways, 0);
        }

        // A cube whose normals all point up - deliberately odd, so kept and recalculated normals differ
        CsgBody BodyWithAllNormalsUp()
        {
            GameObject cube = Cube();
            Mesh mesh = CopyOf(MeshOf(cube));
            Vector3[] up = new Vector3[mesh.vertexCount];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            cube.GetComponent<MeshFilter>().sharedMesh = mesh;
            return cube.AddComponent<CsgBody>();
        }

        [Test]
        public void ResetShape_PutsEverythingBack()
        {
            CsgBody body = Body();
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            Mesh authoredMesh = MeshOf(body);
            Material[] authoredMaterials = renderer.sharedMaterials;
            body.InteriorMaterial = NewMaterial("Interior");
            body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));

            body.ResetShape();

            Assert.AreSame(authoredMesh, MeshOf(body));
            CollectionAssert.AreEqual(authoredMaterials, renderer.sharedMaterials);
            Assert.AreEqual(1, body.Volume, 1e-5);
            Assert.IsTrue(body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f)), "still usable after a reset");
        }

        int _changes;

        void CountChange(CsgBody body)
        {
            _changes++;
        }

        [Test]
        public void Changed_IsRaisedForEveryChange()
        {
            CsgBody body = Body();
            _changes = 0;
            body.Changed += CountChange;

            body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f));
            body.ResetShape();

            Assert.AreEqual(2, _changes);
        }

        [Test]
        public void WhenEmpty_Destroy_RemovesTheObject()
        {
            CsgBody body = Body();
            GameObject go = body.gameObject;

            bool done = body.Subtract(Shape(CsgShape.Primitive.Box), At(Vector3.zero, 3));

            Assert.IsTrue(done);
            Assert.IsTrue(go == null);
        }

        [Test]
        public void WhenEmpty_Deactivate_HidesTheObjectUntilReset()
        {
            CsgBody body = Body();
            body.whenEmpty = EmptyAction.Deactivate;

            body.Subtract(Shape(CsgShape.Primitive.Box), At(Vector3.zero, 3));
            Assert.IsFalse(body.gameObject.activeSelf);

            body.ResetShape();
            Assert.IsTrue(body.gameObject.activeSelf);
        }

        [Test]
        public void WhenEmpty_Keep_LeavesAnEmptyMeshWithoutCollider()
        {
            CsgBody body = Body();
            body.whenEmpty = EmptyAction.Keep;

            body.Subtract(Shape(CsgShape.Primitive.Box), At(Vector3.zero, 3));

            Assert.IsTrue(body.IsEmpty);
            Assert.AreEqual(0, body.TriangleCount);
            Assert.IsFalse(body.GetComponent<MeshCollider>().enabled);
            Assert.IsFalse(body.GetComponent<BoxCollider>().enabled);
        }
    }
}
