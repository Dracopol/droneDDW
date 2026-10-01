using System.Collections.Generic;
using UnityEngine;

public class Sway : MonoBehaviour
{
    public float angle = 3f;
    public float speed = 1f;
    float phase;
    Quaternion baseRot;

    void Awake() { phase = Random.value * 6.28f; baseRot = transform.localRotation; }

    void Update()
    {
        float t = Time.time * speed + phase;
        transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * angle, 0f, Mathf.Cos(t * 0.8f) * angle);
    }
}

public class Spinner : MonoBehaviour
{
    public float speed = 1000f;
    void Update() { transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self); }
}

public class Pulse : MonoBehaviour
{
    public float speed = 3f;
    public float amount = 0.4f;
    Vector3 baseScale;
    float phase;

    void Awake() { baseScale = transform.localScale; phase = Random.value * 6.28f; }
    void Update() { transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * speed + phase) * amount); }
}

public class Flap : MonoBehaviour
{
    public Transform left, right;
    public float speed = 14f;
    public float angle = 35f;
    float phase;

    void Awake() { phase = Random.value * 6.28f; }

    void Update()
    {
        float a = Mathf.Sin(Time.time * speed + phase) * angle;
        if (left) left.localRotation = Quaternion.Euler(0f, 0f, a);
        if (right) right.localRotation = Quaternion.Euler(0f, 0f, -a);
    }
}

public class OrbitAgent : MonoBehaviour
{
    public Vector3 center;
    public float radiusX = 3f, radiusZ = 3f;
    public float height = 0f;
    public float speed = 0.5f;
    public float bob = 0.3f;
    public float phase;

    void Update()
    {
        float t = Time.time * speed + phase;
        Vector3 p = center + new Vector3(Mathf.Cos(t) * radiusX,
                                         height + Mathf.Sin(t * 2.3f) * bob,
                                         Mathf.Sin(t * 1.3f) * radiusZ);
        Vector3 dir = p - transform.localPosition;
        transform.localPosition = p;
        if (dir.sqrMagnitude > 1e-8f)
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                                      Quaternion.LookRotation(dir), 5f * Time.deltaTime);
    }
}

public class CloudDrift : MonoBehaviour
{
    public float speed = 2f;
    public float wrap = 140f;

    void Update()
    {
        Vector3 p = transform.localPosition;
        p.x += speed * Time.deltaTime;
        if (p.x > wrap) p.x = -wrap;
        transform.localPosition = p;
    }
}

public class PackageDrop : MonoBehaviour
{
    public float groundY;
    float vy, timer;
    bool landed;
    Vector3 baseScale;

    void Start() { baseScale = transform.localScale; }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!landed)
        {
            vy -= 9.8f * dt;
            Vector3 p = transform.localPosition;
            p.y += vy * dt;
            if (p.y <= groundY) { p.y = groundY; landed = true; }
            transform.localPosition = p;
        }
        else
        {
            timer += dt;
            if (timer > 2f)
            {
                float k = 1f - (timer - 2f) / 0.5f;
                if (k <= 0f) { Destroy(gameObject); return; }
                transform.localScale = baseScale * k;
            }
        }
    }
}

public class DroneAgent : MonoBehaviour
{
    Vector3 hubTop;
    List<Vector3> targets;
    float cruise, speed;

    Vector3[] route;
    int idx;
    float wait, yaw, bobPhase;
    Vector3 vel;
    Transform package, body;
    bool needsReset;

    const int DropIndex = 2;

    public void Init(Vector3 hubTop, List<Vector3> targets, float cruiseHeight, float speed)
    {
        this.hubTop = hubTop;
        this.targets = targets;
        this.cruise = cruiseHeight;
        this.speed = speed;
        body = transform.Find("Body");
        bobPhase = Random.value * 6.28f;
        ResetToHub();
        wait = Random.Range(0f, 2.5f);
    }

    void OnEnable()
    {
        if (needsReset && route != null) { needsReset = false; ResetToHub(); }
    }

    void OnDisable() { needsReset = true; }

    void ResetToHub()
    {
        transform.localPosition = hubTop;
        vel = Vector3.zero;
        StartTrip();
        wait = Random.Range(0.3f, 2f);
    }

    void StartTrip()
    {
        Vector3 t = (targets != null && targets.Count > 0)
            ? targets[Random.Range(0, targets.Count)]
            : hubTop + new Vector3(6f, -8f, 6f);

        float h = cruise + Random.Range(-1.5f, 2f);
        var hubAbove = new Vector3(hubTop.x, h, hubTop.z);
        var tAbove = new Vector3(t.x, h, t.z);

        route = new[] { hubAbove, tAbove, t + Vector3.up * 2.5f, tAbove, hubAbove, hubTop };
        idx = 0;

        if (package == null)
        {
            package = ProceduralModels.Package(transform).transform;
            package.localPosition = new Vector3(0f, -0.3f, 0f);
        }
    }

    void Update()
    {
        if (route == null) return;
        float dt = Time.deltaTime;

        if (body) body.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f + bobPhase) * 0.06f, 0f);

        if (wait > 0f)
        {
            wait -= dt;
            vel = Vector3.Lerp(vel, Vector3.zero, 5f * dt);
            return;
        }

        Vector3 pos = transform.localPosition;
        Vector3 to = route[idx] - pos;
        float dist = to.magnitude;

        float sp = speed * Mathf.Clamp01(dist / 3f + 0.2f);         
        Vector3 desired = dist > 0.001f ? to / dist * sp : Vector3.zero;
        vel = Vector3.Lerp(vel, desired, 1f - Mathf.Exp(-3f * dt));
        transform.localPosition = pos + vel * dt;

        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        float fs = flat.magnitude;
        if (fs > 0.6f) yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 3f * dt);
        Quaternion goal = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(Mathf.Clamp(fs * 2.5f, 0f, 18f), 0f, 0f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, goal, 5f * dt);

        if (dist < 0.25f) Arrive();
    }

    void Arrive()
    {
        if (idx == DropIndex) Drop();

        if (idx == route.Length - 1)
        {
            wait = Random.Range(1.5f, 3.5f);   
            StartTrip();
            return;
        }
        idx++;
    }

    void Drop()
    {
        if (package == null) return;
        Transform p = package;
        package = null;
        p.SetParent(transform.parent, true);
        var d = p.gameObject.AddComponent<PackageDrop>();
        d.groundY = route[DropIndex].y - 2.5f + 0.2f;
    }
}