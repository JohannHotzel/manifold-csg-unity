// Inspector of CsgBody: below the settings it says whether the mesh can be used and what will happen to
// the colliders, and fixes the two most common problems (Read/Write off, no collider) with one click.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ManifoldCSG
{
    [CustomEditor(typeof(CsgBody)), CanEditMultipleObjects]
    class CsgBodyEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (targets.Length != 1) return;
            CsgBody body = (CsgBody)target;
            EditorGUILayout.Space();

            if (body.Operations > 0)
            {
                string stats = body.Operations + " operations, " + body.TriangleCount.ToString("N0") + " triangles.";
                EditorGUILayout.HelpBox(stats, MessageType.None);
            }
            else
            {
                CsgEditorGUI.MeshStatus(body.GetComponent<MeshFilter>().sharedMesh);
            }
            if (body.LastError != null) EditorGUILayout.HelpBox(body.LastError, MessageType.Error);

            Colliders(body);

            bool rigidbodyOnParent = body.GetComponent<Rigidbody>() == null && body.GetComponentInParent<Rigidbody>() != null;
            if (body.scaleMassWithVolume && rigidbodyOnParent)
            {
                EditorGUILayout.HelpBox("Scale Mass With Volume only works with a Rigidbody on this object, not on a parent.",
                                        MessageType.Info);
            }
        }

        static void Colliders(CsgBody body)
        {
            ColliderMode mode = body.EffectiveColliderMode;
            string text;
            if (body.colliders == ColliderMode.Auto) text = "Colliders: Auto, which means " + mode + " here. ";
            else text = "Colliders: " + mode + ". ";

            if (mode == ColliderMode.Keep)
            {
                if (HasSolidCollider(body))
                {
                    EditorGUILayout.HelpBox(text + "The colliders stay as they are, so raycasts do not see the holes.",
                                            MessageType.Info);
                    return;
                }
                EditorGUILayout.HelpBox(text + "This object has no collider, so raycasts will not find it.",
                                        MessageType.Warning);
                if (GUILayout.Button("Add MeshCollider")) Undo.AddComponent<MeshCollider>(body.gameObject);
                return;
            }

            if (mode == ColliderMode.Exact) text += "A MeshCollider follows the shape exactly";
            else text += "A convex MeshCollider follows the shape: holes are filled, also for raycasts";
            if (body.colliders == ColliderMode.Exact && mode == ColliderMode.Convex)
                text += " (Exact does not work on a non-kinematic Rigidbody)";
            if (body.Operations == 0) text += FirstChange(body);
            EditorGUILayout.HelpBox(text + ".", MessageType.Info);
        }

        // What the first operation will do to the colliders that are there now
        static string FirstChange(CsgBody body)
        {
            List<string> disabled = new List<string>();
            bool hasMeshCollider = false;
            foreach (Collider c in body.GetComponents<Collider>())
            {
                if (c.isTrigger) continue;
                if (!hasMeshCollider && c is MeshCollider)
                {
                    hasMeshCollider = true;
                    continue;
                }
                if (c.enabled) disabled.Add(ObjectNames.NicifyVariableName(c.GetType().Name));
            }

            string text;
            if (hasMeshCollider) text = ". Its MeshCollider is reused";
            else text = ". A MeshCollider is added on the first change";
            if (disabled.Count > 0) text += ", the " + string.Join(" and ", disabled) + " is disabled until ResetShape";
            return text;
        }

        static bool HasSolidCollider(CsgBody body)
        {
            foreach (Collider c in body.GetComponents<Collider>())
            {
                if (c.enabled && !c.isTrigger) return true;
            }
            return false;
        }
    }
}
