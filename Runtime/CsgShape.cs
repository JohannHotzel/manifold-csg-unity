// A tool shape: a primitive or any closed mesh. Built once and shared by every CsgBody it is applied to.
//
//     Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
//     body.Subtract(bullet, Matrix4x4.TRS(hit.point, rotation, Vector3.one * 0.3f));
//
// Primitives are unit size: they fit into a 1 x 1 x 1 box around the origin, +Y points out of the
// surface. A mesh is used at its own size, its pivot is the origin.
//
// Keep the resolution low for shapes that cut again and again. Every cut adds roughly the shape's
// triangle count to the target, and everything a cut costs grows with the target's triangle count:
// a 12-segment sphere (72 triangles) made the 100th shot into a wall about 6x cheaper than Unity's
// sphere (760 triangles).
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ManifoldCSG
{
    [CreateAssetMenu(menuName = "ManifoldCSG/CSG Shape", fileName = "CSG Shape")]
    public class CsgShape : ScriptableObject
    {
        public enum ShapeSource { Primitive, Mesh }
        public enum Primitive { Sphere, Box, Cylinder, Cone }

        public ShapeSource source = ShapeSource.Primitive;
        [FormerlySerializedAs("kind")]
        public Primitive primitive = Primitive.Sphere;
        [Tooltip("Circle resolution of sphere, cylinder and cone. 8-16 is plenty for bullet holes.")]
        [Range(4, 64)] public int segments = 12;
        [Tooltip("Any closed mesh with Read/Write enabled, used at its own size.")]
        public Mesh mesh;
        [Tooltip("Recalculate: from the geometry, edges sharper than Sharp Angle stay hard. Keep Imported: the mesh's own normals.")]
        public NormalMode normals = NormalMode.Recalculate;
        [Tooltip("Edges with a larger angle (degrees) stay sharp.")]
        [Range(0, 180)] public float sharpAngle = 50f;
        [Tooltip("Material of this shape's faces in every result, whatever the operation. " +
                 "Empty: the body decides - its interior material for Subtract and Intersect, its outside for Union.")]
        public Material material;

        ToolEntry _entry;      // the shape, built on first use
        string _error;         // why it could not be built
        Mesh _previewMesh;
        bool _previewOutdated = true;

        /// <summary>The shape at its own size, built on first use. Throws if the mesh cannot be used.</summary>
        public Manifold Shape
        {
            get
            {
                string error;
                ToolEntry entry = GetEntry(out error);
                if (entry == null) throw new InvalidOperationException(error);
                return entry.Shape;
            }
        }

        /// <summary>A Unity mesh of the shape, e.g. to show where it will cut. Belongs to the shape, built on first use.</summary>
        public Mesh PreviewMesh
        {
            get
            {
                if (_previewMesh == null || _previewOutdated)
                {
                    _previewMesh = ManifoldUnity.ToUnityMesh(Shape.GetMeshData(), null, 1, _previewMesh);
                    _previewMesh.name = name + " (preview)";
                    _previewOutdated = false;
                }
                return _previewMesh;
            }
        }

        /// <summary>Rebuilds the shape on its next use, e.g. after its mesh was changed or made readable.</summary>
        public void Refresh()
        {
            Release();
        }

        internal ToolEntry GetEntry(out string error)
        {
            if (_entry == null && _error == null)
            {
                try
                {
                    _entry = new ToolEntry(Build());
                }
                catch (Exception e)
                {
                    _error = "CSG Shape '" + name + "': " + e.Message;   // stays until the settings change
                }
            }
            error = _error;
            return _entry;
        }

        Manifold Build()
        {
            if (source == ShapeSource.Mesh)
                return ManifoldUnity.FromUnityMesh(mesh, sharpAngle, normals == NormalMode.KeepImported);

            Manifold raw;
            switch (primitive)
            {
                case Primitive.Box:
                    raw = Manifold.Cube(1, 1, 1);
                    break;
                case Primitive.Cylinder:
                    raw = Upright(Manifold.Cylinder(1, 0.5, 0.5, segments, true));
                    break;
                case Primitive.Cone:
                    raw = Upright(Manifold.Cylinder(1, 0.5, 0, segments, true));   // the tip points into the surface
                    break;
                default:
                    raw = Manifold.Sphere(0.5, segments);
                    break;
            }
            Manifold withNormals = raw.CalculateNormals(sharpAngle);
            raw.Dispose();
            return withNormals;
        }

        // Manifold cylinders run along +Z; this turns +Z into -Y
        static Manifold Upright(Manifold alongZ)
        {
            Manifold upright = alongZ.Rotate(90, 0, 0);
            alongZ.Dispose();
            return upright;
        }

        // Settings changed in the Inspector: rebuild on next use. Earlier cuts keep their faces and materials.
        void OnValidate()
        {
            Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Release()
        {
            if (_entry != null) _entry.Dispose();
            _entry = null;
            _error = null;
            _previewOutdated = true;   // rebuilt into the same mesh, so a MeshFilter showing it stays up to date
        }
    }
}
