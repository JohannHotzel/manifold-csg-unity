// CsgBody colliders and mass.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ManifoldCSG.Tests
{
    public class CsgColliderTests : CsgTestBase
    {
        // A sphere cut into the +X face of the body
        bool Cut(CsgBody body)
        {
            Vector3 position = body.transform.position + new Vector3(0.5f, 0, 0);
            return body.Subtract(Shape(), Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * 0.6f));
        }

        [Test]
        public void Auto_DisablesTheBoxCollider_AndFollowsTheShape()
        {
            CsgBody body = Body();
            BoxCollider box = body.GetComponent<BoxCollider>();
            SphereCollider trigger = body.gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;

            Cut(body);

            Assert.IsTrue(box != null && !box.enabled, "disabled, not destroyed");
            Assert.IsTrue(trigger.enabled, "triggers are left alone");
            MeshCollider meshCollider = body.GetComponent<MeshCollider>();
            Assert.IsFalse(meshCollider.convex);
            Assert.AreSame(MeshOf(body), meshCollider.sharedMesh);

            body.ResetShape();

            Assert.IsTrue(box.enabled);
            Assert.IsTrue(trigger.enabled);
            Assert.IsFalse(meshCollider.enabled, "the added MeshCollider waits, disabled, for the next change");

            Cut(body);   // right after a reset, as CsgDemo does every frame
            Assert.IsTrue(meshCollider.enabled);
            Assert.AreSame(meshCollider, body.GetComponent<MeshCollider>());
            Assert.IsFalse(box.enabled);
        }

        [Test]
        public void Auto_UsesConvex_OnADynamicRigidbody()
        {
            CsgBody body = Body();
            body.gameObject.AddComponent<Rigidbody>();
            Assert.AreEqual(ColliderMode.Convex, body.EffectiveColliderMode);

            Cut(body);   // the test fails on any error Unity logs, e.g. about a non-convex collider

            MeshCollider meshCollider = body.GetComponent<MeshCollider>();
            Assert.IsTrue(meshCollider.convex);
            Assert.Less(meshCollider.sharedMesh.vertexCount, MeshOf(body).vertexCount, "cooked from the hull, not the whole mesh");
        }

        [Test]
        public void Auto_OnAKinematicRigidbody_StaysExact()
        {
            CsgBody body = Body();
            Rigidbody rigidbody = body.gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            Cut(body);

            Assert.IsFalse(body.GetComponent<MeshCollider>().convex);
        }

        [Test]
        public void Exact_OnADynamicRigidbody_FallsBackToConvex()
        {
            CsgBody body = Body();
            body.colliders = ColliderMode.Exact;
            body.gameObject.AddComponent<Rigidbody>();
            LogAssert.Expect(LogType.Warning, new Regex("using Convex"));

            Cut(body);

            Assert.IsTrue(body.GetComponent<MeshCollider>().convex);
        }

        [Test]
        public void Auto_AddsNothing_WithoutACollider()
        {
            CsgBody body = Body();
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            Assert.AreEqual(ColliderMode.Keep, body.EffectiveColliderMode);

            Cut(body);

            Assert.IsNull(body.GetComponent<Collider>());
        }

        [Test]
        public void Keep_LeavesTheCollidersAlone()
        {
            CsgBody body = Body();
            body.colliders = ColliderMode.Keep;

            Cut(body);

            Assert.IsTrue(body.GetComponent<BoxCollider>().enabled);
            Assert.IsNull(body.GetComponent<MeshCollider>());
        }

        [Test]
        public void AMeshCollider_IsReusedAndRestored()
        {
            CsgBody body = Body();
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            MeshCollider meshCollider = body.gameObject.AddComponent<MeshCollider>();
            Mesh authored = meshCollider.sharedMesh;

            Cut(body);
            Assert.AreSame(meshCollider, body.GetComponent<MeshCollider>());
            Assert.AreSame(MeshOf(body), meshCollider.sharedMesh);

            body.ResetShape();
            Assert.AreSame(meshCollider, body.GetComponent<MeshCollider>());
            Assert.AreSame(authored, meshCollider.sharedMesh);
        }

        [Test]
        public void Mass_FollowsTheVolume_AndComesBackWithReset()
        {
            CsgBody body = Body();
            Rigidbody rigidbody = body.gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = 10;

            // Takes away the half with x > 0
            body.Subtract(Shape(CsgShape.Primitive.Box), At(new Vector3(0.5f, 0, 0), new Vector3(1, 2, 2)));
            Assert.AreEqual(5, rigidbody.mass, 1e-3);

            body.ResetShape();
            Assert.AreEqual(10, rigidbody.mass, 1e-5);
        }

        [Test]
        public void Mass_StaysWhenSwitchedOff()
        {
            CsgBody body = Body();
            body.scaleMassWithVolume = false;
            Rigidbody rigidbody = body.gameObject.AddComponent<Rigidbody>();
            rigidbody.mass = 10;

            body.Subtract(Shape(CsgShape.Primitive.Box), At(new Vector3(0.5f, 0, 0), new Vector3(1, 2, 2)));

            Assert.AreEqual(10, rigidbody.mass);
        }

        [Test]
        public void ExactCollider_LetsRaycastsThroughHoles()
        {
            CsgBody body = Body();
            Assert.IsTrue(body.Subtract(Shape(CsgShape.Primitive.Box), At(Vector3.zero, new Vector3(0.4f, 0.4f, 3))));
            GameObject behind = Cube(new Vector3(0, 0, 3));
            Physics.SyncTransforms();
            RaycastHit hit;

            Assert.IsTrue(Physics.Raycast(new Ray(Origin + new Vector3(0, 0, -5), Vector3.forward), out hit));
            Assert.AreEqual(behind, hit.collider.gameObject, "through the tunnel");

            Assert.IsTrue(Physics.Raycast(new Ray(Origin + new Vector3(0.35f, 0, -5), Vector3.forward), out hit));
            Assert.AreEqual(body.gameObject, hit.collider.gameObject);
            Assert.AreEqual(4.5f, hit.distance, 1e-3);
        }
    }
}
