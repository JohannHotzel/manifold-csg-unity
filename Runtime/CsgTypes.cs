// The enums of ManifoldCSG.
namespace ManifoldCSG
{
    /// <summary>The three boolean operations. The numbers are the ones Manifold uses itself.</summary>
    public enum CsgOperation
    {
        Union = 0,
        Subtract = 1,
        Intersect = 2,
    }

    /// <summary>Where the normals of an imported mesh come from.</summary>
    public enum NormalMode
    {
        /// <summary>Calculated from the geometry; edges sharper than the sharp angle stay hard.</summary>
        Recalculate,
        /// <summary>The mesh's own normals, e.g. authored smoothing. Falls back to Recalculate without normals.</summary>
        KeepImported,
    }

    /// <summary>How a CsgBody keeps its collider in step with its shape.</summary>
    public enum ColliderMode
    {
        /// <summary>Exact, or Convex on a non-kinematic Rigidbody. Nothing at all on an object without a collider.</summary>
        Auto,
        /// <summary>A non-convex MeshCollider of the current shape: holes are real for physics and raycasts.</summary>
        Exact,
        /// <summary>A convex MeshCollider of the current shape: works on dynamic Rigidbodies, holes are filled.</summary>
        Convex,
        /// <summary>The object's colliders stay as they are; nothing is rebuilt after a change.</summary>
        Keep,
    }

    /// <summary>What a CsgBody does when nothing is left of it.</summary>
    public enum EmptyAction
    {
        Destroy,
        Deactivate,
        Keep,
    }

    /// <summary>Status of a Manifold. Manifold reports errors as a status instead of throwing.</summary>
    public enum ManifoldError
    {
        NoError,
        NonFiniteVertex,
        NotManifold,
        VertexIndexOutOfBounds,
        PropertiesWrongLength,
        MissingPositionProperties,
        MergeVectorsDifferentLengths,
        MergeIndexOutOfBounds,
        TransformWrongLength,
        RunIndexWrongLength,
        FaceIdWrongLength,
        InvalidConstruction,
        ResultTooLarge,
        InvalidTangents,
        Cancelled,
    }
}
