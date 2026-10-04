// Inspector pieces shared by CsgBody and CsgShape.
using UnityEditor;
using UnityEngine;

namespace ManifoldCSG
{
    static class CsgEditorGUI
    {
        // The last check, kept per mesh: importing a big mesh on every repaint would make the Inspector crawl
        static Mesh _mesh;
        static bool _readable;
        static bool _usable;
        static string _message;

        /// <summary>Says whether the mesh can be used and offers to enable Read/Write. True if it just did.</summary>
        public static bool MeshStatus(Mesh mesh)
        {
            bool readable = mesh != null && mesh.isReadable;
            if (mesh != _mesh || readable != _readable)
            {
                _mesh = mesh;
                _readable = readable;
                _usable = Csg.Check(mesh, out _message);
            }
            MessageType type = MessageType.Warning;
            if (_usable) type = MessageType.Info;
            EditorGUILayout.HelpBox(_message, type);

            if (mesh == null || mesh.isReadable) return false;
            ModelImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)) as ModelImporter;
            if (importer == null) return false;   // not from a model file, e.g. made in code
            if (!GUILayout.Button("Enable Read/Write")) return false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            _mesh = null;   // check again
            return true;
        }
    }
}
