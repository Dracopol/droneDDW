using System.Collections;
using UnityEngine;

public class ParkMiniature : MonoBehaviour
{
    [Tooltip("Found automatically if empty.")]
    public ParkWorld world;
    [Tooltip("Defaults to Camera.main (your VR headset).")]
    public Transform head;

    [Header("Size and placement")]
    [Tooltip("0.012 turns the 66 m park into a ~0.8 m model.")]
    public float miniatureScale = 0.012f;
    public float tableHeight = 0.95f;
    [Tooltip("x = right, z = forward, measured from your head when the table is placed.")]
    public Vector3 offsetFromHead = new Vector3(0f, 0f, 0.9f);
    public float placeDelay = 0.75f;
    [Tooltip("On: the model has the same compass orientation as the real park (marker rotates as you turn). " +
             "Off: the table turns to face you when placed.")]
    public bool alignWithWorld = true;

    Transform table, miniRoot, marker, effBar, natBar;
    const float BarMax = 0.14f;

    IEnumerator Start()
    {
        if (world == null) world = FindFirstObjectByType<ParkWorld>();
        if (world == null) { Debug.LogError("[MINIATURE] No ParkWorld found in the scene."); yield break; }

        Build();
        yield return new WaitForSeconds(placeDelay);
        Recenter();
    }

    void Build()
    {
        table = new GameObject("Miniature Table").transform;
        table.SetParent(transform, false);

        var wood = new Color(0.35f, 0.25f, 0.17f);
        ProceduralModels.Cyl(table, new Vector3(0, 0.015f, 0), 0.5f, 0.03f, wood);                      
        ProceduralModels.Cyl(table, new Vector3(0, tableHeight * 0.5f, 0), 0.12f, tableHeight, wood);  
        ProceduralModels.Cyl(table, new Vector3(0, tableHeight - 0.02f, 0), 1.0f, 0.04f,
                             new Color(0.2f, 0.2f, 0.22f));                                    

        miniRoot = new GameObject("MiniPark").transform;
        miniRoot.SetParent(table, false);
        miniRoot.localPosition = new Vector3(0, tableHeight + 0.002f, 0);
        miniRoot.localScale = Vector3.one * miniatureScale;
        world.BuildInstance(miniRoot, true);

        marker = ProceduralModels.Empty("YouAreHere", miniRoot).transform;
        ProceduralModels.Cyl(marker, new Vector3(0, 1.5f, 0), 0.5f, 3f, Color.white);
        ProceduralModels.Sph(marker, new Vector3(0, 3.2f, 0), 3f, new Color(1f, 0.9f, 0.1f));
        ProceduralModels.Cube(marker, new Vector3(0, 3.2f, 2.2f), new Vector3(0.9f, 0.9f, 2.4f), new Color(1f, 0.5f, 0.1f));

        effBar = ProceduralModels.Cube(table, Vector3.zero, new Vector3(0.05f, 0.01f, 0.05f), new Color(1f, 0.55f, 0.1f)).transform;
        natBar = ProceduralModels.Cube(table, Vector3.zero, new Vector3(0.05f, 0.01f, 0.05f), new Color(0.3f, 0.85f, 0.3f)).transform;
    }

    [ContextMenu("Recenter in front of me")]
    public void Recenter()
    {
        Transform h = GetHead();
        if (h == null || table == null || world == null) return;

        Vector3 fwd = Vector3.ProjectOnPlane(h.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector3 pos = h.position + right * offsetFromHead.x + fwd * offsetFromHead.z;
        pos.y = world.transform.position.y;         
        table.position = pos;
        table.rotation = alignWithWorld ? world.transform.rotation : Quaternion.LookRotation(fwd);
    }

    Transform GetHead()
    {
        if (head != null) return head;
        if (Camera.main != null) head = Camera.main.transform;
        return head;
    }

    void Update()
    {
        if (miniRoot == null || world == null || world.WorldRoot == null) return;

        Transform h = GetHead();
        if (h != null && marker != null)
        {
            Vector3 l = world.WorldRoot.InverseTransformPoint(h.position);
            marker.localPosition = new Vector3(l.x, 0f, l.z);
            marker.localEulerAngles = new Vector3(0f, h.eulerAngles.y - world.WorldRoot.eulerAngles.y, 0f);
        }

        float nature = world.ShownNature;
        SetBar(effBar, -0.12f, 1f - nature);
        SetBar(natBar, 0.12f, nature);
    }

    void SetBar(Transform bar, float x, float value)
    {
        float h = Mathf.Max(0.004f, BarMax * value);
        bar.localScale = new Vector3(0.05f, h, 0.05f);
        bar.localPosition = new Vector3(x, tableHeight + h * 0.5f, -0.45f);
    }
}