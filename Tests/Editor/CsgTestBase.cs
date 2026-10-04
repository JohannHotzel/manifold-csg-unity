// Shared setup for the ManifoldCSG tests: the test objects stand far away from anything in the open
// scene, and everything created here is destroyed after each test.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ManifoldCSG.Tests
{
    public abstract class CsgTestBase
    {
        protected static readonly Vector3 Origin = new Vector3(0, 1000, 0);

        List<Object> _created = new List<Object>();

        /// <summary>Remembers an object, so it is destroyed after the test.</summary>
        protected void DestroyLater(Object obj)
        {
            _created.Add(obj);
        }

        /// <summary>A Unity primitive at Origin + offset.</summary>
        protected GameObject Primitive(PrimitiveType type, Vector3 offset)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = "CSG Test " + type;
            go.transform.position = Origin + offset;
            DestroyLater(go);
            return go;
        }

        /// <summary>A Unity cube (1 m, with a BoxCollider) at Origin.</summary>
        protected GameObject Cube()
        {
            return Primitive(PrimitiveType.Cube, Vector3.zero);
        }

        protected GameObject Cube(Vector3 offset)
        {
            return Primitive(PrimitiveType.Cube, offset);
        }

        /// <summary>A cube with a CsgBody at Origin.</summary>
        protected CsgBody Body()
        {
            return Cube().AddComponent<CsgBody>();
        }

        protected CsgBody Body(Vector3 offset)
        {
            return Cube(offset).AddComponent<CsgBody>();
        }

        /// <summary>A 12-segment sphere shape.</summary>
        protected CsgShape Shape()
        {
            return Shape(CsgShape.Primitive.Sphere);
        }

        protected CsgShape Shape(CsgShape.Primitive primitive)
        {
            CsgShape shape = ScriptableObject.CreateInstance<CsgShape>();
            shape.primitive = primitive;
            DestroyLater(shape);
            return shape;
        }

        protected Material NewMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.name = name;
            DestroyLater(material);
            return material;
        }

        /// <summary>A readable copy of a mesh.</summary>
        protected Mesh CopyOf(Mesh mesh)
        {
            Mesh copy = Object.Instantiate(mesh);
            DestroyLater(copy);
            return copy;
        }

        /// <summary>A placement relative to Origin.</summary>
        protected static Matrix4x4 At(Vector3 offset, float size)
        {
            return Matrix4x4.TRS(Origin + offset, Quaternion.identity, Vector3.one * size);
        }

        protected static Matrix4x4 At(Vector3 offset, Vector3 scale)
        {
            return Matrix4x4.TRS(Origin + offset, Quaternion.identity, scale);
        }

        protected static Mesh MeshOf(Component component)
        {
            return component.GetComponent<MeshFilter>().sharedMesh;
        }

        protected static Mesh MeshOf(GameObject go)
        {
            return go.GetComponent<MeshFilter>().sharedMesh;
        }

        protected static int Triangles(Mesh mesh, int subMesh)
        {
            return (int)mesh.GetIndexCount(subMesh) / 3;
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object obj in _created)
            {
                if (obj == null) continue;   // destroyed during the test
                // A CsgBody's own mesh is not destroyed in Edit mode (no OnDestroy there), so do it here
                GameObject go = obj as GameObject;
                if (go != null)
                {
                    MeshFilter filter = go.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name.EndsWith("(CSG)"))
                        Object.DestroyImmediate(filter.sharedMesh);
                }
                Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }
    }
}
