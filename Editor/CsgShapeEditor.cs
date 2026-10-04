// Inspector of CsgShape: shows only the settings of the chosen source, how many triangles the shape has,
// and whether a mesh can be used (with a Read/Write button).
using System;
using UnityEditor;

namespace ManifoldCSG
{
    [CustomEditor(typeof(CsgShape)), CanEditMultipleObjects]
    class CsgShapeEditor : Editor
    {
        SerializedProperty _source;
        SerializedProperty _primitive;
        SerializedProperty _segments;
        SerializedProperty _mesh;
        SerializedProperty _normals;
        SerializedProperty _sharpAngle;
        SerializedProperty _material;

        void OnEnable()
        {
            _source = serializedObject.FindProperty("source");
            _primitive = serializedObject.FindProperty("primitive");
            _segments = serializedObject.FindProperty("segments");
            _mesh = serializedObject.FindProperty("mesh");
            _normals = serializedObject.FindProperty("normals");
            _sharpAngle = serializedObject.FindProperty("sharpAngle");
            _material = serializedObject.FindProperty("material");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_source);
            bool mixed = _source.hasMultipleDifferentValues;   // several shapes selected with different sources
            bool fromMesh = _source.enumValueIndex == (int)CsgShape.ShapeSource.Mesh;
            if (mixed || !fromMesh)
            {
                EditorGUILayout.PropertyField(_primitive);
                EditorGUILayout.PropertyField(_segments);
            }
            if (mixed || fromMesh)
            {
                EditorGUILayout.PropertyField(_mesh);
                EditorGUILayout.PropertyField(_normals);
            }
            if (mixed || !fromMesh || _normals.enumValueIndex == (int)NormalMode.Recalculate)
                EditorGUILayout.PropertyField(_sharpAngle);
            EditorGUILayout.PropertyField(_material);
            serializedObject.ApplyModifiedProperties();

            if (targets.Length != 1 || mixed) return;
            CsgShape shape = (CsgShape)target;
            EditorGUILayout.Space();
            if (fromMesh)
            {
                if (CsgEditorGUI.MeshStatus(shape.mesh)) shape.Refresh();
                return;
            }
            try
            {
                string text = shape.Shape.NumTri.ToString("N0") + " triangles at unit size, +Y points out of the surface. " +
                              "Every cut adds about half of them to the target - keep them low for shapes that cut " +
                              "again and again.";
                EditorGUILayout.HelpBox(text, MessageType.Info);
            }
            catch (Exception e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Error);
            }
        }
    }
}
