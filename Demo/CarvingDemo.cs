// Click-to-carve sample: a click cuts the chosen tool shape out of the object under the mouse.
// Left click carves, right drag orbits, the wheel zooms.
// Every object to carve has a CsgBody (its Interior Material colors the cuts) and a MeshCollider.
using System.Diagnostics;
using ManifoldCSG;
using UnityEngine;
using UnityEngine.InputSystem;

public class CarvingDemo : MonoBehaviour
{
    public CsgShape[] tools;      // one button per CSG Shape asset
    public Transform objectsRoot;
    public MeshFilter preview;    // shows the tool - and is where it cuts
    public float size = 0.6f;
    public float depth = 0.25f;   // how far the tool sinks into the surface, as a share of its size
    public float twist;

    static readonly Rect Panel = new Rect(10, 10, 260, 210);

    int _tool;
    string _status = "";
    float _yaw;
    float _pitch = 26.6f;
    float _distance = 13.4f;

    void Update()
    {
        Mouse mouse = Mouse.current;
        OrbitCamera(mouse);

        // Over the panel the mouse belongs to the buttons and sliders (IMGUI counts y from the top)
        Vector2 pointer = mouse.position.ReadValue();
        if (Panel.Contains(new Vector2(pointer.x, Screen.height - pointer.y)))
        {
            preview.gameObject.SetActive(false);
            return;
        }

        // The object under the mouse
        RaycastHit hit;
        CsgBody body = null;
        if (Physics.Raycast(Camera.main.ScreenPointToRay(pointer), out hit))
            body = hit.collider.GetComponent<CsgBody>();
        preview.gameObject.SetActive(body != null);
        if (body == null) return;

        // The tool on the surface: +Y along the normal, sunk in by depth, turned by twist
        preview.sharedMesh = tools[_tool].PreviewMesh;
        preview.transform.position = hit.point - hit.normal * (depth * size);
        preview.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, twist, 0);
        preview.transform.localScale = Vector3.one * size;

        if (mouse.leftButton.wasPressedThisFrame) Carve(body);
    }

    // Cuts the tool out exactly where the preview stands
    void Carve(CsgBody body)
    {
        Stopwatch watch = Stopwatch.StartNew();
        if (body.Subtract(tools[_tool], preview.transform.localToWorldMatrix))
            _status = body.name + ": " + watch.Elapsed.TotalMilliseconds.ToString("F1") + " ms, " + body.TriangleCount + " triangles";
    }

    void ResetAll()
    {
        foreach (CsgBody body in objectsRoot.GetComponentsInChildren<CsgBody>()) body.ResetShape();
        _status = "";
    }

    // Right drag orbits around the objects, the wheel zooms
    void OrbitCamera(Mouse mouse)
    {
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * 0.3f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.3f, -10, 89);
        }
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0) _distance = Mathf.Max(3, _distance * 0.9f);
        if (scroll < 0) _distance = Mathf.Min(40, _distance * 1.1f);

        Vector3 center = new Vector3(0, 1, 0);
        Transform cam = Camera.main.transform;
        cam.position = center + Quaternion.Euler(_pitch, _yaw, 0) * new Vector3(0, 0, -_distance);
        cam.LookAt(center);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(Panel, GUI.skin.box);
        GUILayout.Label("Left click: carve · Right drag: orbit\nWheel: zoom");

        string[] toolNames = new string[tools.Length];
        for (int i = 0; i < tools.Length; i++) toolNames[i] = tools[i].name;
        _tool = GUILayout.SelectionGrid(_tool, toolNames, 3);

        size = Slider("Size", size, 0.1f, 2);
        depth = Slider("Depth", depth, -0.5f, 1);
        twist = Slider("Twist", twist, 0, 360);
        if (GUILayout.Button("Reset")) ResetAll();
        GUILayout.Label(_status);
        GUILayout.EndArea();
    }

    static float Slider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label + ": " + value.ToString("0.##"), GUILayout.Width(90));
        value = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.EndHorizontal();
        return value;
    }
}
