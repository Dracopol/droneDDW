using System.Collections.Generic;
using UnityEngine;

public static class ProceduralModels
{
    static Material baseMat;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { baseMat = null; cache.Clear(); }

    public static Material NewMat(Color c)
    {
        if (baseMat == null)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMat = p.GetComponent<Renderer>().sharedMaterial;
            UnityEngine.Object.Destroy(p);
        }
        var m = new Material(baseMat);
        m.color = c;
        return m;
    }

    public static Material Mat(Color c)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c);
        if (!cache.TryGetValue(key, out var m) || m == null) { m = NewMat(c); cache[key] = m; }
        return m;
    }

    public static GameObject Empty(string name, Transform parent, Vector3 pos = default)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g;
    }

    public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
                                  Color color, Vector3 euler = default, Material mat = null)
    {
        var g = GameObject.CreatePrimitive(type);
        var col = g.GetComponent<Collider>();
        if (col) UnityEngine.Object.Destroy(col);
        var t = g.transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(euler);
        t.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat != null ? mat : Mat(color);
        return g;
    }

    public static GameObject Cube(Transform p, Vector3 pos, Vector3 size, Color c, Vector3 euler = default)
        => Prim(PrimitiveType.Cube, p, pos, size, c, euler);

    public static GameObject Cyl(Transform p, Vector3 pos, float dia, float height, Color c, Vector3 euler = default)
        => Prim(PrimitiveType.Cylinder, p, pos, new Vector3(dia, height * 0.5f, dia), c, euler);

    public static GameObject Sph(Transform p, Vector3 pos, float dia, Color c)
        => Prim(PrimitiveType.Sphere, p, pos, new Vector3(dia, dia, dia), c);

    public static GameObject Sph(Transform p, Vector3 pos, Vector3 size, Color c)
        => Prim(PrimitiveType.Sphere, p, pos, size, c);

    static readonly Color Brown = new Color(0.36f, 0.24f, 0.14f);
    static readonly Color Leaf1 = new Color(0.22f, 0.55f, 0.20f);
    static readonly Color Leaf2 = new Color(0.30f, 0.65f, 0.25f);
    static readonly Color Leaf3 = new Color(0.18f, 0.47f, 0.22f);
    static readonly Color Pine1 = new Color(0.10f, 0.36f, 0.20f);
    static readonly Color Pine2 = new Color(0.14f, 0.44f, 0.22f);
    static readonly Color StemG = new Color(0.25f, 0.55f, 0.20f);
    static readonly Color Yellow = new Color(1f, 0.85f, 0.2f);
    static readonly Color RockG = new Color(0.5f, 0.5f, 0.52f);
    static readonly Color Wood = new Color(0.55f, 0.38f, 0.2f);
    static readonly Color Metal = new Color(0.25f, 0.27f, 0.30f);
    static readonly Color Glow = new Color(1f, 0.92f, 0.6f);
    static readonly Color Water = new Color(0.2f, 0.5f, 0.75f);
    static readonly Color Lily = new Color(0.2f, 0.6f, 0.3f);
    static readonly Color Pink = new Color(1f, 0.5f, 0.7f);
    static readonly Color Concrete = new Color(0.55f, 0.58f, 0.62f);
    static readonly Color Glass = new Color(0.35f, 0.6f, 0.78f);
    static readonly Color Dark = new Color(0.15f, 0.16f, 0.18f);
    static readonly Color Red = new Color(1f, 0.2f, 0.2f);
    static readonly Color Green = new Color(0.2f, 1f, 0.4f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
    static readonly Color Cardboard = new Color(0.75f, 0.55f, 0.32f);
    static readonly Color Tape = new Color(0.92f, 0.82f, 0.6f);
    static readonly Color HouseWall = new Color(0.9f, 0.76f, 0.62f);
    static readonly Color HouseRoof = new Color(0.7f, 0.3f, 0.25f);
    static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
    static readonly Color Cyan = new Color(0.2f, 0.9f, 1f);
    static readonly Color PadYellow = new Color(1f, 0.8f, 0.1f);
    static readonly Color DroneDark = new Color(0.12f, 0.13f, 0.15f);
    static readonly Color DroneLight = new Color(0.85f, 0.87f, 0.9f);

    public static GameObject Tree(Transform parent, bool pine)
    {
        var r = Empty(pine ? "Pine" : "Tree", parent);
        Cyl(r.transform, new Vector3(0, 1.1f, 0), 0.35f, 2.2f, Brown);

        var f = Empty("Foliage", r.transform, new Vector3(0, 2f, 0));  
        if (!pine)
        {
            Sph(f.transform, new Vector3(0, 1.0f, 0), 2.7f, Leaf1);
            Sph(f.transform, new Vector3(0.6f, 1.9f, 0.2f), 1.9f, Leaf2);
            Sph(f.transform, new Vector3(-0.5f, 1.6f, -0.4f), 1.8f, Leaf3);
        }
        else
        {
            Sph(f.transform, new Vector3(0, 0.5f, 0), new Vector3(2.4f, 1.3f, 2.4f), Pine1);
            Sph(f.transform, new Vector3(0, 1.4f, 0), new Vector3(1.8f, 1.2f, 1.8f), Pine2);
            Sph(f.transform, new Vector3(0, 2.3f, 0), new Vector3(1.2f, 1.1f, 1.2f), Pine1);
        }
        var s = f.AddComponent<Sway>();
        s.angle = pine ? 1.5f : 2.5f;
        s.speed = 0.8f;
        return r;
    }

    public static GameObject Bush(Transform parent)
    {
        var r = Empty("Bush", parent);
        var f = Empty("Foliage", r.transform);
        Sph(f.transform, new Vector3(0, 0.4f, 0), 1.0f, Leaf3);
        Sph(f.transform, new Vector3(0.45f, 0.35f, 0.1f), 0.8f, Leaf1);
        Sph(f.transform, new Vector3(-0.4f, 0.32f, -0.15f), 0.75f, Leaf2);
        f.AddComponent<Sway>().angle = 1.2f;
        return r;
    }

    public static GameObject Flower(Transform parent, Color petal)
    {
        var r = Empty("Flower", parent);
        var pv = Empty("Pivot", r.transform);
        Cyl(pv.transform, new Vector3(0, 0.25f, 0), 0.025f, 0.5f, StemG);
        Sph(pv.transform, new Vector3(0, 0.52f, 0), 0.16f, petal);
        Sph(pv.transform, new Vector3(0, 0.57f, 0), 0.07f, Yellow);
        Sph(pv.transform, new Vector3(0.07f, 0.15f, 0), new Vector3(0.14f, 0.015f, 0.07f), StemG);
        var s = pv.AddComponent<Sway>();
        s.angle = 7f; s.speed = 1.6f;
        return r;
    }

    public static GameObject Rock(Transform parent)
    {
        var r = Empty("Rock", parent);
        Sph(r.transform, new Vector3(0, 0.22f, 0), new Vector3(0.95f, 0.55f, 0.75f), RockG);
        Sph(r.transform, new Vector3(0.5f, 0.12f, 0.2f), new Vector3(0.5f, 0.3f, 0.4f), RockG * 0.9f);
        return r;
    }

    public static GameObject Pond(Transform parent)
    {
        var r = Empty("Pond", parent);
        Cyl(r.transform, new Vector3(0, 0.05f, 0), 8f, 0.1f, Water);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a) * 4.1f, 0.15f, Mathf.Sin(a) * 4.1f);
            Sph(r.transform, p, new Vector3(0.9f + 0.3f * Mathf.Sin(i * 3f), 0.5f, 0.8f), RockG);
        }
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(Mathf.Sin(i * 2.1f) * 2f, 0.11f, Mathf.Cos(i * 1.7f) * 2f);
            Cyl(r.transform, p, 0.7f, 0.02f, Lily);
            if (i % 2 == 0) Sph(r.transform, p + new Vector3(0, 0.05f, 0), 0.14f, Pink);
        }
        for (int i = 0; i < 7; i++)
        {
            float a = 0.4f + i * 0.5f;
            var p = new Vector3(Mathf.Cos(a) * 3.5f, 0.6f, Mathf.Sin(a) * 3.5f);
            var reed = Cyl(r.transform, p, 0.04f, 1.2f, StemG);
            var s = reed.AddComponent<Sway>(); s.angle = 5f; s.speed = 1.2f;
        }
        return r;
    }

    public static GameObject Butterfly(Transform parent, Color wing)
    {
        var r = Empty("Butterfly", parent);
        Sph(r.transform, Vector3.zero, new Vector3(0.03f, 0.03f, 0.14f), Dark);
        var lp = Empty("WingL", r.transform, new Vector3(-0.015f, 0.01f, 0));
        var rp = Empty("WingR", r.transform, new Vector3(0.015f, 0.01f, 0));
        Cube(lp.transform, new Vector3(-0.07f, 0, 0), new Vector3(0.14f, 0.004f, 0.11f), wing);
        Cube(rp.transform, new Vector3(0.07f, 0, 0), new Vector3(0.14f, 0.004f, 0.11f), wing);
        var f = r.AddComponent<Flap>();
        f.left = lp.transform; f.right = rp.transform; f.speed = 16f; f.angle = 40f;
        r.transform.localScale = Vector3.one * 3f;
        return r;
    }

    public static GameObject Bird(Transform parent)
    {
        var r = Empty("Bird", parent);
        Sph(r.transform, Vector3.zero, new Vector3(0.3f, 0.28f, 0.6f), new Color(0.35f, 0.3f, 0.28f));
        Sph(r.transform, new Vector3(0, 0.08f, 0.33f), 0.18f, new Color(0.3f, 0.26f, 0.24f));
        Cube(r.transform, new Vector3(0, 0.07f, 0.45f), new Vector3(0.05f, 0.04f, 0.12f), Orange);
        Cube(r.transform, new Vector3(0, 0.03f, -0.42f), new Vector3(0.18f, 0.02f, 0.3f), new Color(0.3f, 0.26f, 0.24f));
        var lp = Empty("WingL", r.transform, new Vector3(-0.1f, 0.05f, 0.05f));
        var rp = Empty("WingR", r.transform, new Vector3(0.1f, 0.05f, 0.05f));
        Cube(lp.transform, new Vector3(-0.3f, 0, 0), new Vector3(0.6f, 0.02f, 0.28f), new Color(0.4f, 0.35f, 0.32f));
        Cube(rp.transform, new Vector3(0.3f, 0, 0), new Vector3(0.6f, 0.02f, 0.28f), new Color(0.4f, 0.35f, 0.32f));
        var f = r.AddComponent<Flap>();
        f.left = lp.transform; f.right = rp.transform; f.speed = 7f; f.angle = 32f;
        r.transform.localScale = Vector3.one * 2.4f;
        return r;
    }
    public static GameObject Bench(Transform parent)
    {
        var r = Empty("Bench", parent);
        Cube(r.transform, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 0.07f, 0.5f), Wood);
        Cube(r.transform, new Vector3(0, 0.85f, -0.24f), new Vector3(1.7f, 0.4f, 0.06f), Wood, new Vector3(-10, 0, 0));
        for (int sx = -1; sx <= 1; sx += 2)
            Cube(r.transform, new Vector3(sx * 0.75f, 0.25f, 0), new Vector3(0.07f, 0.5f, 0.45f), Metal);
        return r;
    }

    public static GameObject LampPost(Transform parent)
    {
        var r = Empty("Lamp", parent);
        Cyl(r.transform, new Vector3(0, 1.75f, 0), 0.1f, 3.5f, Metal);
        Cyl(r.transform, new Vector3(0, 0.1f, 0), 0.3f, 0.2f, Metal);
        Sph(r.transform, new Vector3(0, 3.65f, 0), 0.4f, Glow);
        Cyl(r.transform, new Vector3(0, 3.9f, 0), 0.5f, 0.06f, Metal);
        return r;
    }

    public static GameObject Drone(Transform parent)
    {
        var r = Empty("Drone", parent);
        var b = Empty("Body", r.transform);
        Cube(b.transform, Vector3.zero, new Vector3(0.45f, 0.12f, 0.45f), DroneDark);
        Sph(b.transform, new Vector3(0, 0.07f, 0), new Vector3(0.3f, 0.14f, 0.3f), DroneLight);
        Cube(b.transform, Vector3.zero, new Vector3(1.0f, 0.035f, 0.07f), DroneDark, new Vector3(0, 45, 0));
        Cube(b.transform, Vector3.zero, new Vector3(1.0f, 0.035f, 0.07f), DroneDark, new Vector3(0, -45, 0));

        const float k = 0.36f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var pos = new Vector3(sx * k, 0.05f, sz * k);
                Cyl(b.transform, pos, 0.09f, 0.06f, DroneLight);
                var rotor = Empty("Rotor", b.transform, pos + new Vector3(0, 0.05f, 0));
                Cube(rotor.transform, Vector3.zero, new Vector3(0.5f, 0.01f, 0.04f), Metal);
                Cube(rotor.transform, Vector3.zero, new Vector3(0.5f, 0.01f, 0.04f), Metal, new Vector3(0, 90, 0));
                rotor.AddComponent<Spinner>().speed = 1100f * (sx * sz);
            }

        Sph(b.transform, new Vector3(0, -0.08f, 0.1f), 0.1f, Dark);                   
        Sph(b.transform, new Vector3(0, -0.03f, 0.24f), 0.07f, Red).AddComponent<Pulse>().speed = 8f;
        Sph(b.transform, new Vector3(0, -0.03f, -0.24f), 0.07f, Green).AddComponent<Pulse>().speed = 8f;
        for (int sx = -1; sx <= 1; sx += 2)                                           
            Cube(b.transform, new Vector3(sx * 0.18f, -0.14f, 0), new Vector3(0.03f, 0.03f, 0.4f), Metal);

        r.transform.localScale = Vector3.one * 1.6f;
        return r;
    }

    public static GameObject Package(Transform parent)
    {
        var r = Empty("Package", parent);
        Cube(r.transform, Vector3.zero, new Vector3(0.3f, 0.25f, 0.3f), Cardboard);
        Cube(r.transform, Vector3.zero, new Vector3(0.32f, 0.255f, 0.06f), Tape);
        return r;
    }

    public static GameObject Hub(Transform parent)
    {
        var r = Empty("DeliveryHub", parent);
        Cube(r.transform, new Vector3(0, 4f, 0), new Vector3(6f, 8f, 6f), Concrete);
        for (int i = 0; i < 4; i++)
        {
            Cube(r.transform, new Vector3(0, 1.5f + i * 1.8f, 3.02f), new Vector3(5f, 0.9f, 0.05f), Glass);
            Cube(r.transform, new Vector3(0, 1.5f + i * 1.8f, -3.02f), new Vector3(5f, 0.9f, 0.05f), Glass);
        }
        Cube(r.transform, new Vector3(0, 8.1f, 0), new Vector3(6.4f, 0.2f, 6.4f), Dark);
        Cyl(r.transform, new Vector3(0, 8.25f, 0), 4.6f, 0.1f, Dark);
        Cyl(r.transform, new Vector3(0, 8.27f, 0), 3.9f, 0.1f, PadYellow);
        Cyl(r.transform, new Vector3(0, 8.29f, 0), 3.4f, 0.1f, Dark);
        Cube(r.transform, new Vector3(-0.5f, 8.36f, 0), new Vector3(0.25f, 0.02f, 1.6f), White);
        Cube(r.transform, new Vector3(0.5f, 8.36f, 0), new Vector3(0.25f, 0.02f, 1.6f), White);
        Cube(r.transform, new Vector3(0, 8.36f, 0), new Vector3(1f, 0.02f, 0.25f), White);
        Cyl(r.transform, new Vector3(2.6f, 10f, 2.6f), 0.12f, 3.6f, Metal);
        Sph(r.transform, new Vector3(2.6f, 11.9f, 2.6f), 0.35f, Red).AddComponent<Pulse>().speed = 4f;
        return r;
    }

    public static GameObject Warehouse(Transform parent)
    {
        var r = Empty("Warehouse", parent);
        Cube(r.transform, new Vector3(0, 2f, 0), new Vector3(10f, 4f, 6f), Concrete);
        Cube(r.transform, new Vector3(0, 4.15f, 0), new Vector3(10.4f, 0.3f, 6.4f), Dark);
        for (int i = -1; i <= 1; i++)
            Cube(r.transform, new Vector3(i * 3f, 1.3f, 3.03f), new Vector3(2.4f, 2.6f, 0.05f), Orange);
        for (int i = 0; i < 3; i++)
            Cube(r.transform, new Vector3(-6.5f, 0.6f + i * 1.2f * 0f, -1.5f + i * 1.6f), new Vector3(1.4f, 1.2f, 1.4f), Cardboard);
        return r;
    }

    public static GameObject House(Transform parent)
    {
        var r = Empty("House", parent);
        Cube(r.transform, new Vector3(0, 1.25f, 0), new Vector3(3.4f, 2.5f, 3.4f), HouseWall);
        Cube(r.transform, new Vector3(0, 2.5f, 0), new Vector3(2.4f, 2.4f, 3.8f), HouseRoof, new Vector3(0, 0, 45));
        Cube(r.transform, new Vector3(0, 0.8f, 1.71f), new Vector3(0.8f, 1.6f, 0.05f), Wood);
        Cube(r.transform, new Vector3(-1.0f, 1.4f, 1.71f), new Vector3(0.6f, 0.6f, 0.05f), Glass);
        Cube(r.transform, new Vector3(1.0f, 1.4f, 1.71f), new Vector3(0.6f, 0.6f, 0.05f), Glass);
        Cube(r.transform, new Vector3(1.0f, 3.6f, -0.6f), new Vector3(0.4f, 1.2f, 0.4f), RockG);
        Cube(r.transform, new Vector3(0, 0.02f, 2.8f), new Vector3(1.6f, 0.04f, 1.6f), PadYellow);  
        return r;
    }

    public static GameObject Pad(Transform parent)
    {
        var r = Empty("LandingPad", parent);
        Cyl(r.transform, new Vector3(0, 0.05f, 0), 3f, 0.1f, Dark);
        Cyl(r.transform, new Vector3(0, 0.07f, 0), 2.4f, 0.1f, PadYellow);
        Cyl(r.transform, new Vector3(0, 0.09f, 0), 2.0f, 0.1f, Dark);
        Cube(r.transform, new Vector3(0, 0.15f, 0), new Vector3(0.9f, 0.02f, 0.2f), White);
        return r;
    }

    public static GameObject Tower(Transform parent)
    {
        var r = Empty("ChargeTower", parent);
        Cube(r.transform, new Vector3(0, 0.3f, 0), new Vector3(1.4f, 0.6f, 1.4f), Concrete);
        Cyl(r.transform, new Vector3(0, 6f, 0), 0.5f, 12f, Metal);
        for (int i = 0; i < 3; i++) Cyl(r.transform, new Vector3(0, 3f + i * 3f, 0), 0.9f, 0.15f, Cyan);
        Sph(r.transform, new Vector3(0, 12.3f, 0), 0.9f, Cyan).AddComponent<Pulse>().speed = 2.5f;
        return r;
    }
}