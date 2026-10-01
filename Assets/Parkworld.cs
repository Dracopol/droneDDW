using System.Collections.Generic;
using UnityEngine;
public class ParkWorld : MonoBehaviour
{
    public enum Group { Always, Nature, Efficiency }
    enum Kind { Tree, Pine, Bush, Flower, Rock, Butterfly, Bird, Pond, Bench, Lamp, Hub, Warehouse, House, Pad, Tower, Drone }

    struct Placement
    {
        public Kind kind; public Group group; public Vector3 pos;
        public float yaw, scale, threshold; public int variant;
    }

    class Item
    {
        public Transform tr; public GameObject go; public Vector3 baseScale;
        public Group group; public float threshold, cur, delay, wait;
    }

    class Instance { public Transform root; public List<Item> items = new List<Item>(); }

    [Header("Link")]
    [Tooltip("Drag your lever here. Its onValueChanged is hooked up automatically.")]
    public LeverHandLever lever;
    [Tooltip("Tick to test without a lever (use the slider below).")]
    public bool useInspectorValue = false;
    [Range(0f, 1f)] public float natureValue = 0.5f;

    [Header("World")]
    public int seed = 7;
    public float parkRadius = 30f;
    public float growTime = 0.7f;
    [Tooltip("How fast sky/ground colours blend (per second, 0-1 scale).")]
    public float environmentBlendSpeed = 0.8f;

    [Header("Nature amounts (at 100% nature)")]
    public int treeCount = 26;
    public int pineCount = 14;
    public int bushCount = 24;
    public int flowerCount = 80;
    public int rockCount = 10;
    public int butterflyCount = 8;
    public int birdCount = 6;

    [Header("Efficiency amounts (at 100% efficiency)")]
    public int droneCount = 10;
    public int houseCount = 8;
    public int padCount = 6;
    public int towerCount = 4;
    public float droneSpeed = 6f;

    [Header("Look: nature end")]
    public Color skyNature = new Color(0.47f, 0.78f, 1f);
    public Color grass = new Color(0.30f, 0.62f, 0.25f);
    public Color cloudNature = new Color(1f, 1f, 1f);
    public Color sunNature = new Color(1f, 0.96f, 0.85f);
    public float fogNature = 0.003f;

    [Header("Look: efficiency end")]
    public Color skyEfficiency = new Color(0.78f, 0.62f, 0.48f);
    public Color concrete = new Color(0.42f, 0.42f, 0.45f);
    public Color cloudEfficiency = new Color(0.45f, 0.40f, 0.38f);
    public Color sunEfficiency = new Color(1f, 0.7f, 0.45f);
    public float fogEfficiency = 0.012f;

    public Light sun;

    public Transform WorldRoot { get; private set; }
    public float ShownNature { get; private set; }

    float natureTarget = 0.5f;
    readonly List<Placement> layout = new List<Placement>();
    readonly List<(Vector2 p, float r)> taken = new List<(Vector2 p, float r)>();
    readonly List<(Vector3 pos, float thr)> targetList = new List<(Vector3 pos, float thr)>();
    readonly List<Instance> instances = new List<Instance>();
    System.Random rng;
    Vector3 hubTop;
    Material groundMat, cloudMat;
    Camera cam;

    static readonly Color[] flowerColors =
    {
        new Color(1f, 0.35f, 0.4f), new Color(1f, 0.85f, 0.25f), new Color(0.75f, 0.45f, 1f),
        new Color(1f, 1f, 1f), new Color(1f, 0.6f, 0.2f), new Color(0.4f, 0.6f, 1f)
    };
    void Awake()
    {
        WorldRoot = new GameObject("ParkRoot").transform;
        WorldRoot.SetParent(transform, false);

        groundMat = ProceduralModels.NewMat(grass);
        cloudMat = ProceduralModels.NewMat(cloudNature);

        if (sun == null) sun = RenderSettings.sun;
        if (sun == null)
        {
            var g = new GameObject("Sun");
            sun = g.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            g.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;
        }

        GenerateLayout();
        BuildInstance(WorldRoot, false);
        BuildClouds();

        natureTarget = lever != null ? lever.Value01 : natureValue;
        ShownNature = natureTarget;
    }

    void OnEnable() { if (lever != null && lever.onValueChanged != null) lever.onValueChanged.AddListener(SetNatureValue); }
    void OnDisable() { if (lever != null && lever.onValueChanged != null) lever.onValueChanged.RemoveListener(SetNatureValue); }
    public void SetNatureValue(float v) { natureTarget = Mathf.Clamp01(v); }

    void Update()
    {
        if (useInspectorValue) natureTarget = natureValue;
        ShownNature = Mathf.MoveTowards(ShownNature, natureTarget, Time.deltaTime * environmentBlendSpeed);
        ApplyEnvironment(ShownNature);
        UpdateItems();
    }

    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(r), Mathf.Cos(r)); }
    static float YawToward(Vector2 from, Vector2 to) { return Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg; }

    bool TrySlot(float minR, float maxR, float radius, out Vector2 p)
    {
        for (int i = 0; i < 50; i++)
        {
            float a = R(0f, 360f);
            float d = Mathf.Sqrt(R(minR * minR, maxR * maxR));
            p = Dir(a) * d;
            bool ok = true;
            foreach (var t in taken)
                if ((t.p - p).sqrMagnitude < (t.r + radius) * (t.r + radius)) { ok = false; break; }
            if (ok) { taken.Add((p, radius)); return true; }
        }
        p = default;
        return false;
    }

    void Add(Kind k, Group g, Vector2 p, float yaw, float scale, float thr, int variant = 0)
    {
        AddV(k, g, new Vector3(p.x, 0f, p.y), yaw, scale, thr, variant);
    }

    void AddV(Kind k, Group g, Vector3 pos, float yaw, float scale, float thr, int variant = 0)
    {
        layout.Add(new Placement { kind = k, group = g, pos = pos, yaw = yaw, scale = scale, threshold = thr, variant = variant });
    }
    void Scatter(Kind kind, Group g, int count, float minR, float maxR, float slotR,
                 float s0, float s1, float thrMax = 1f, bool faceCenter = false)
    {
        for (int k = 0; k < count; k++)
        {
            if (!TrySlot(minR, maxR, slotR, out Vector2 p)) continue;
            float yaw = faceCenter ? YawToward(p, Vector2.zero) : R(0f, 360f);
            float thr = (k + 1f) / count * thrMax;
            Add(kind, g, p, yaw, R(s0, s1), thr, rng.Next(1000));

            if (kind == Kind.Pad)
                targetList.Add((new Vector3(p.x, 0f, p.y), thr));
            if (kind == Kind.House)
                targetList.Add((new Vector3(p.x, 0f, p.y) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, 2.9f), thr));
        }
    }

    void GenerateLayout()
    {
        rng = new System.Random(seed);
        layout.Clear(); taken.Clear(); targetList.Clear();

        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            Vector2 p = Dir(a) * 5.5f;
            Add(Kind.Bench, Group.Always, p, a + 180f, 1f, 0f);
            taken.Add((p, 1.4f));
        }
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = Dir(i * 60f) * 9f;
            Add(Kind.Lamp, Group.Always, p, 0f, 1f, 0f);
            taken.Add((p, 0.6f));
        }

        Vector2 hub = new Vector2(-15f, 19f);
        Add(Kind.Hub, Group.Efficiency, hub, YawToward(hub, Vector2.zero), 1f, 0f);
        taken.Add((hub, 6.5f));
        hubTop = new Vector3(hub.x, 8.5f, hub.y);

        Scatter(Kind.Warehouse, Group.Efficiency, 1, 14f, 24f, 7.5f, 1f, 1f, 0.35f, true);
        Scatter(Kind.Pad, Group.Efficiency, padCount, 7f, 24f, 2.4f, 1f, 1f, 0.5f);
        Scatter(Kind.House, Group.Efficiency, houseCount, 10f, 26f, 3.8f, 0.95f, 1.1f, 0.8f, true);
        Scatter(Kind.Tower, Group.Efficiency, towerCount, 8f, 27f, 1.5f, 0.9f, 1.2f);

        for (int k = 0; k < droneCount; k++)
            AddV(Kind.Drone, Group.Efficiency, hubTop, 0f, 1f, (k + 1f) / droneCount, k);

        Scatter(Kind.Pond, Group.Nature, 1, 10f, 20f, 5.2f, 1f, 1f, 0.25f);
        Scatter(Kind.Tree, Group.Nature, treeCount, 5f, 28f, 1.9f, 0.85f, 1.35f);
        Scatter(Kind.Pine, Group.Nature, pineCount, 5f, 28f, 1.9f, 0.9f, 1.4f);
        Scatter(Kind.Bush, Group.Nature, bushCount, 4f, 28f, 1.1f, 0.8f, 1.4f);
        Scatter(Kind.Rock, Group.Nature, rockCount, 4f, 28f, 0.9f, 0.7f, 1.6f);
        Scatter(Kind.Flower, Group.Nature, flowerCount, 3.8f, 27f, 0.4f, 0.8f, 1.5f);

        for (int k = 0; k < butterflyCount; k++)
        {
            Vector2 c = Dir(R(0f, 360f)) * R(5f, 24f);
            AddV(Kind.Butterfly, Group.Nature, new Vector3(c.x, 1.4f, c.y), 0f, R(0.9f, 1.3f), (k + 1f) / butterflyCount, k);
        }
        for (int k = 0; k < birdCount; k++)
            AddV(Kind.Bird, Group.Nature, new Vector3(0f, R(14f, 22f), 0f), 0f, R(0.9f, 1.2f), (k + 1f) / birdCount, k);
    }

    List<Vector3> TargetsFor(float droneThr)
    {
        float min = float.MaxValue;
        foreach (var t in targetList) min = Mathf.Min(min, t.thr);
        float limit = Mathf.Max(droneThr, min);   
        var l = new List<Vector3>();
        foreach (var t in targetList) if (t.thr <= limit + 1e-4f) l.Add(t.pos);
        return l;
    }

    public void BuildInstance(Transform root, bool miniature)
    {
        var inst = new Instance { root = root };
        float d = parkRadius * 2f + 6f;

        ProceduralModels.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -0.1f, 0), new Vector3(d, 0.1f, d),
                              Color.white, default, groundMat);
        if (!miniature)
            ProceduralModels.Prim(PrimitiveType.Cylinder, root, new Vector3(0, -0.12f, 0), new Vector3(600f, 0.1f, 600f),
                                  Color.white, default, groundMat);

        ProceduralModels.Cyl(root, new Vector3(0, 0.02f, 0), 5.6f, 0.04f, new Color(0.62f, 0.6f, 0.58f)); 

        foreach (var p in layout) inst.items.Add(MakeItem(p, root));
        instances.Add(inst);
    }

    Item MakeItem(Placement p, Transform root)
    {
        GameObject go = null;
        switch (p.kind)
        {
            case Kind.Tree: go = ProceduralModels.Tree(root, false); break;
            case Kind.Pine: go = ProceduralModels.Tree(root, true); break;
            case Kind.Bush: go = ProceduralModels.Bush(root); break;
            case Kind.Rock: go = ProceduralModels.Rock(root); break;
            case Kind.Pond: go = ProceduralModels.Pond(root); break;
            case Kind.Bench: go = ProceduralModels.Bench(root); break;
            case Kind.Lamp: go = ProceduralModels.LampPost(root); break;
            case Kind.Hub: go = ProceduralModels.Hub(root); break;
            case Kind.Warehouse: go = ProceduralModels.Warehouse(root); break;
            case Kind.House: go = ProceduralModels.House(root); break;
            case Kind.Pad: go = ProceduralModels.Pad(root); break;
            case Kind.Tower: go = ProceduralModels.Tower(root); break;

            case Kind.Flower:
                go = ProceduralModels.Flower(root, flowerColors[p.variant % flowerColors.Length]);
                break;

            case Kind.Butterfly:
                go = ProceduralModels.Butterfly(root, flowerColors[p.variant % flowerColors.Length]);
                var bo = go.AddComponent<OrbitAgent>();
                bo.center = p.pos;
                bo.radiusX = Random.Range(1.5f, 3.5f);
                bo.radiusZ = Random.Range(1.5f, 3.5f);
                bo.speed = Random.Range(0.6f, 1.1f);
                bo.bob = 0.35f;
                bo.phase = p.variant * 1.7f;
                break;

            case Kind.Bird:
                go = ProceduralModels.Bird(root);
                var br = go.AddComponent<OrbitAgent>();
                br.center = p.pos;
                br.radiusX = Random.Range(12f, 26f);
                br.radiusZ = Random.Range(12f, 26f);
                br.speed = Random.Range(0.12f, 0.24f);
                br.bob = 1.5f;
                br.phase = p.variant * 2.3f;
                break;

            case Kind.Drone:
                go = ProceduralModels.Drone(root);
                go.AddComponent<DroneAgent>().Init(hubTop, TargetsFor(p.threshold), 14f, droneSpeed);
                break;
        }

        var t = go.transform;
        t.localPosition = p.pos;
        t.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
        t.localScale = t.localScale * p.scale;

        var item = new Item
        {
            tr = t,
            go = go,
            baseScale = t.localScale,
            group = p.group,
            threshold = p.threshold,
            cur = 0f,
            delay = Random.Range(0f, 0.5f)
        };
        item.wait = item.delay;
        t.localScale = item.baseScale * 0.001f;
        go.SetActive(false);
        return item;
    }

    void BuildClouds()
    {
        for (int i = 0; i < 10; i++)
        {
            var c = ProceduralModels.Empty("Cloud", WorldRoot,
                new Vector3(Random.Range(-130f, 130f), Random.Range(40f, 62f), Random.Range(-130f, 130f)));
            for (int j = 0; j < 4; j++)
                ProceduralModels.Sph(c.transform,
                    new Vector3(Random.Range(-10f, 10f), Random.Range(-1f, 2f), Random.Range(-6f, 6f)),
                    new Vector3(Random.Range(14f, 24f), Random.Range(5f, 8f), Random.Range(10f, 16f)),
                    Color.white).GetComponent<Renderer>().sharedMaterial = cloudMat;
            foreach (var r in c.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.AddComponent<CloudDrift>().speed = Random.Range(1f, 3f);
        }
    }
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void UpdateItems()
    {
        float dt = Time.deltaTime;
        float nature = natureTarget;
        float eff = 1f - natureTarget;

        foreach (var inst in instances)
            foreach (var item in inst.items)
            {
                float v = item.group == Group.Nature ? nature : eff;
                bool want = item.group == Group.Always || (v > 0.0001f && v >= item.threshold - 0.0001f);
                float goal = want ? 1f : 0f;

                if (Mathf.Approximately(item.cur, goal)) { item.wait = item.delay; continue; }
                item.wait -= dt;
                if (item.wait > 0f) continue;      

                item.cur = Mathf.MoveTowards(item.cur, goal, dt / growTime);

                bool active = item.cur > 0.001f;
                if (item.go.activeSelf != active) item.go.SetActive(active);
                if (!active) continue;

                float s = goal > 0f ? EaseOutBack(item.cur) : item.cur * item.cur * (3f - 2f * item.cur);
                item.tr.localScale = item.baseScale * Mathf.Max(0.001f, s);
            }
    }

    void ApplyEnvironment(float n)
    {
        float s = Mathf.SmoothStep(0f, 1f, n);
        Color sky = Color.Lerp(skyEfficiency, skyNature, s);

        groundMat.color = Color.Lerp(concrete, grass, s);
        cloudMat.color = Color.Lerp(cloudEfficiency, cloudNature, s);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = sky;
        RenderSettings.fogDensity = Mathf.Lerp(fogEfficiency, fogNature, s);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = sky * 0.85f;

        if (sun != null)
        {
            sun.color = Color.Lerp(sunEfficiency, sunNature, s);
            sun.intensity = Mathf.Lerp(0.9f, 1.2f, s);
        }

        if (cam == null) cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
        }
    }
}