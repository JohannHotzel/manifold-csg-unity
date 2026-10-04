// Tools of every kind, Csg one-shots and input checks.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ManifoldCSG.Tests
{
    public class CsgApiTests : CsgTestBase
    {
        [Test]
        public void MeshAndSceneObject_CutTheSame()
        {
            GameObject sphere = Primitive(PrimitiveType.Sphere, new Vector3(0.5f, 0.2f, 0));
            sphere.transform.localScale = Vector3.one * 0.6f;
            CsgBody byMesh = Body();
            CsgBody byObject = Body();

            Assert.IsTrue(byMesh.Subtract(MeshOf(sphere), sphere.transform.localToWorldMatrix));
            Assert.IsTrue(byObject.Subtract(sphere.GetComponent<MeshFilter>()));

            Assert.Less(byMesh.Volume, 0.99f);
            Assert.AreEqual(byMesh.Volume, byObject.Volume, 1e-6);
        }

        [Test]
        public void MeshShape_CutsLikeTheMesh()
        {
            Mesh sphereMesh = MeshOf(Primitive(PrimitiveType.Sphere, Vector3.zero));
            CsgShape shape = Shape();
            shape.source = CsgShape.ShapeSource.Mesh;
            shape.mesh = sphereMesh;
            CsgBody byShape = Body();
            CsgBody byMesh = Body();

            Assert.IsTrue(byShape.Subtract(shape, At(new Vector3(0.5f, 0, 0), 0.6f)));
            Assert.IsTrue(byMesh.Subtract(sphereMesh, At(new Vector3(0.5f, 0, 0), 0.6f)));

            Assert.AreEqual(byMesh.Volume, byShape.Volume, 1e-6);
        }

        [Test]
        public void Csg_KeepsSubmeshesAndCanSeparateToolFaces()
        {
            MeshFilter a = Cube().GetComponent<MeshFilter>();
            MeshFilter b = Primitive(PrimitiveType.Sphere, new Vector3(0.5f, 0, 0)).GetComponent<MeshFilter>();
            b.transform.localScale = Vector3.one * 0.6f;

            Mesh joined = Csg.Subtract(a, b);
            Mesh separate = Csg.Subtract(a, b, true);
            DestroyLater(joined);
            DestroyLater(separate);

            Assert.AreEqual(1, joined.subMeshCount);
            Assert.AreEqual(2, separate.subMeshCount);
            Assert.Greater(Triangles(separate, 1), 0);
            Assert.AreEqual(Triangles(joined, 0), Triangles(separate, 0) + Triangles(separate, 1));
        }

        [Test]
        public void Csg_ReusesTheTargetMesh()
        {
            MeshFilter a = Cube().GetComponent<MeshFilter>();
            MeshFilter b = Primitive(PrimitiveType.Sphere, new Vector3(0.5f, 0, 0)).GetComponent<MeshFilter>();
            Mesh first = Csg.Union(a, b);
            DestroyLater(first);

            Assert.AreSame(first, Csg.Intersect(a, b, false, first));
        }

        [Test]
        public void Csg_ReturnsNullForAnOpenMesh()
        {
            MeshFilter plane = Primitive(PrimitiveType.Plane, Vector3.zero).GetComponent<MeshFilter>();
            MeshFilter cube = Cube().GetComponent<MeshFilter>();
            LogAssert.Expect(LogType.Warning, new Regex("not closed"));

            Assert.IsNull(Csg.Subtract(plane, cube));
        }

        [Test]
        public void OpenMesh_FailsWithAReadableMessage()
        {
            CsgBody plane = Primitive(PrimitiveType.Plane, Vector3.zero).AddComponent<CsgBody>();
            LogAssert.Expect(LogType.Warning, new Regex("not closed"));

            bool done = plane.Subtract(Shape(), At(Vector3.zero, 1));

            Assert.IsFalse(done);
            StringAssert.Contains("not closed", plane.LastError);
            Assert.IsFalse(plane.Subtract(Shape(), At(Vector3.zero, 1)), "stays unusable, without another warning");
        }

        [Test]
        public void DegeneratePlacement_IsRejected_AndTheBodyStays()
        {
            CsgBody body = Body();
            LogAssert.Expect(LogType.Warning, new Regex("degenerate"));

            bool done = body.Subtract(Shape(), At(Vector3.zero, new Vector3(1, 0, 1)));

            Assert.IsFalse(done);
            Assert.AreEqual(0, body.Operations);
            Assert.IsTrue(body.Subtract(Shape(), At(new Vector3(0.5f, 0, 0), 0.6f)));
            Assert.IsNull(body.LastError, "a successful operation clears the error");
        }

        [Test]
        public void MissingTool_IsRejected()
        {
            CsgBody body = Body();
            LogAssert.Expect(LogType.Warning, new Regex("missing"));

            CsgShape noShape = null;
            Assert.IsFalse(body.Subtract(noShape, At(Vector3.zero, 1)));
        }

        [Test]
        public void Check_ExplainsWhatIsWrong()
        {
            Mesh cube = MeshOf(Cube());
            Mesh plane = MeshOf(Primitive(PrimitiveType.Plane, Vector3.zero));
            Mesh locked = CopyOf(cube);
            locked.UploadMeshData(true);   // true: no longer readable
            string message;

            Assert.IsTrue(Csg.Check(cube, out message));
            StringAssert.Contains("12 triangles", message);
            Assert.IsFalse(Csg.Check(plane, out message));
            StringAssert.Contains("not closed", message);
            Assert.IsFalse(Csg.Check(locked, out message));
            StringAssert.Contains("Read/Write", message);
            Assert.IsFalse(Csg.Check(null, out message));
        }

        [Test]
        public void FromMeshData_RejectsBrokenRuns()
        {
            // A tetrahedron
            float[] vertices = { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            uint[] triangles = { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 };

            ExpectRejected(vertices, triangles, new uint[] { 0, 5, 12 }, new uint[] { 1, 2 });   // not whole triangles
            ExpectRejected(vertices, triangles, new uint[] { 0, 6 }, new uint[] { 1 });          // does not reach the end
            ExpectRejected(vertices, triangles, new uint[] { 0, 6, 12 }, new uint[] { 2, 1 });   // ids not ascending
        }

        static void ExpectRejected(float[] vertices, uint[] triangles, uint[] runIndex, uint[] ids)
        {
            try
            {
                Manifold manifold = Manifold.FromMeshData(vertices, 3, triangles, runIndex, ids, true);
                manifold.Dispose();
            }
            catch (System.ArgumentException)
            {
                return;
            }
            Assert.Fail("Expected an ArgumentException");
        }

        [Test]
        public void SubMeshesKeepTheirIds()
        {
            uint firstId;
            Manifold manifold = ManifoldUnity.FromUnityMesh(MeshOf(Cube()), out firstId);

            MeshData data = manifold.GetMeshData(null, 0);
            manifold.Dispose();

            Assert.AreEqual(1, data.NumRun);
            Assert.AreEqual(firstId, data.RunOriginalId[0]);
        }
    }
}
