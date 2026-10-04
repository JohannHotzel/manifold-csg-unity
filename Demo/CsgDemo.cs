// Minimal live CSG: a tool body circles through this object, and one operation is applied every frame,
// starting from the untouched shape each time. The scene shows all three operations side by side.
// Everything can be moved in the Scene view during Play mode, and the operation switched in the Inspector.
using ManifoldCSG;
using UnityEngine;

[RequireComponent(typeof(CsgBody))]
public class CsgDemo : MonoBehaviour
{
    [Tooltip("The body that is subtracted from this one (or added, or intersected with it).")]
    public MeshFilter tool;
    public CsgOperation operation = CsgOperation.Subtract;

    CsgBody _body;
    Vector3 _toolStart;
    GUIStyle _labelStyle;

    void Start()
    {
        _body = GetComponent<CsgBody>();
        _toolStart = tool.transform.position;
    }

    void Update()
    {
        // The tool circles through the body
        float t = Time.time * 0.6f;
        tool.transform.position = _toolStart + new Vector3(Mathf.Sin(t * 1.3f), 0, Mathf.Cos(t)) * 1.1f;

        // Back to the untouched shape, then the operation where the tool stands now -
        // a fraction of a millisecond at this mesh size
        _body.ResetShape();
        _body.Apply(operation, tool);
    }

    // The name of the operation above the body
    void OnGUI()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.fontSize = 22;
        }
        Vector3 above = transform.position + Vector3.up * (transform.lossyScale.y * 0.5f + 1f);
        Vector3 screen = Camera.main.WorldToScreenPoint(above);
        if (screen.z < 0) return;   // behind the camera
        // IMGUI counts y from the top of the screen
        Rect rect = new Rect(screen.x - 100, Screen.height - screen.y - 15, 200, 30);
        GUI.Label(rect, operation.ToString(), _labelStyle);
    }
}
