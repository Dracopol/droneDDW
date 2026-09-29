using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;

public class LeverHandLever : MonoBehaviour
{
    public enum Axis { X, Z }

    [Header("Grab")]
    [Tooltip("Lever_Knob. Found automatically if left empty.")]
    [SerializeField] private Transform knob;
    [Tooltip("How close (metres) your pinch must be to the knob to grab it.")]
    [SerializeField] private float grabRadius = 0.2f;
    [SerializeField] private float pinchStart = 0.03f;
    [SerializeField] private float pinchEnd = 0.05f;

    [Header("Rotation")]
    [SerializeField] private Axis rotationAxis = Axis.Z;
    [SerializeField, Range(5f, 80f)] private float maxAngle = 45f;
    [Tooltip("Tick this if moving the lever left gives nature instead of efficiency.")]
    [SerializeField] private bool invertDirection = false;

    [Header("Snapping when released")]
    [SerializeField] private bool snapOnRelease = true;
    [SerializeField, Range(1f, 50f)] private float snapStepPercent = 10f;
    [SerializeField] private float snapSpeed = 10f;

    [Header("Events")]
    public UnityEvent<float> onValueChanged;
    public UnityEvent<float> onReleased;

    public float Value01 { get; private set; } = 0.5f;
    public int NaturePercent => Mathf.RoundToInt(Value01 * 100f);
    public int EfficiencyPercent => 100 - NaturePercent;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private float grabOffset;
    private float currentAngle;
    private float lastSentValue = -1f;

    private XRHandSubsystem subsystem;
    private static readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    private Transform origin;
    private bool leftPinching, rightPinching;
    private int gripHand = -1;   
    private Vector3 gripPoint;
    private Vector3 knobBaseScale;
    private float nextWarn;

    private Vector3 AxisVector => rotationAxis == Axis.X ? Vector3.right : Vector3.forward;

    private void Awake()
    {
        restRotation = transform.localRotation;
        restPosition = transform.localPosition;

        if (knob == null) knob = transform.Find("Lever_Knob");
        if (knob == null) knob = transform;
        knobBaseScale = knob.localScale;

        Debug.Log("[LEVER] LeverHandLever is running. Knob = " + knob.name);
    }

    private void FindSubsystem()
    {
        SubsystemManager.GetSubsystems(subsystems);
        subsystem = subsystems.Count > 0 ? subsystems[0] : null;

        if (origin == null && Camera.main != null) origin = Camera.main.transform.root;

        if (subsystem == null && Time.time > nextWarn)
        {
            nextWarn = Time.time + 5f;
            Debug.LogWarning("[LEVER] No hand tracking subsystem yet. Enable 'Hand Tracking Subsystem' in " +
                             "Project Settings > XR Plug-in Management > OpenXR > Features.");
        }
    }

    private bool TryGetPinch(XRHand hand, ref bool wasPinching, out Vector3 point)
    {
        point = default;
        if (!hand.isTracked) { wasPinching = false; return false; }

        if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
            !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index))
        {
            wasPinching = false;
            return false;
        }

        Vector3 t = origin.TransformPoint(thumb.position);
        Vector3 i = origin.TransformPoint(index.position);
        float d = Vector3.Distance(t, i);

        bool pinching = wasPinching ? d < pinchEnd : d < pinchStart;
        wasPinching = pinching;
        point = (t + i) * 0.5f;
        return pinching;
    }

    private void UpdateHands()
    {
        if (subsystem == null || origin == null) { FindSubsystem(); if (subsystem == null || origin == null) return; }

        bool prevL = leftPinching, prevR = rightPinching;
        bool lp = TryGetPinch(subsystem.leftHand, ref leftPinching, out Vector3 lpt);
        bool rp = TryGetPinch(subsystem.rightHand, ref rightPinching, out Vector3 rpt);

        if (gripHand == -1)
        {
            if (lp && !prevL) LogPinch("Left", lpt);
            if (rp && !prevR) LogPinch("Right", rpt);
        }

        if (gripHand == -1)
        {
            if (lp && Vector3.Distance(lpt, knob.position) < grabRadius) BeginGrab(0, lpt);
            else if (rp && Vector3.Distance(rpt, knob.position) < grabRadius) BeginGrab(1, rpt);
        }
        else
        {
            bool still = gripHand == 0 ? lp : rp;
            if (!still) EndGrab();
            else gripPoint = gripHand == 0 ? lpt : rpt;
        }
    }

    private void LogPinch(string side, Vector3 p)
    {
        float d = Vector3.Distance(p, knob.position);
        Debug.Log($"[LEVER] {side} pinch detected, {d:F2} m from the knob (need < {grabRadius:F2} m)");
    }

    private void BeginGrab(int hand, Vector3 point)
    {
        gripHand = hand;
        gripPoint = point;
        grabOffset = HandAngle() - currentAngle;
        knob.localScale = knobBaseScale * 1.15f;
        Debug.Log("[LEVER] GRABBED with " + (hand == 0 ? "left" : "right") + " hand");
    }

    private void EndGrab()
    {
        gripHand = -1;
        knob.localScale = knobBaseScale;
        onReleased?.Invoke(Value01);
        Debug.Log($"[LEVER] Released: {EfficiencyPercent}% efficiency, {NaturePercent}% nature");
    }
    private float HandAngle()
    {
        Vector3 local = transform.parent != null
            ? transform.parent.InverseTransformPoint(gripPoint)
            : gripPoint;

        Vector3 dir = Quaternion.Inverse(restRotation) * (local - restPosition);
        dir = Vector3.ProjectOnPlane(dir, AxisVector);

        return dir.sqrMagnitude > 0.0001f
            ? Vector3.SignedAngle(Vector3.up, dir, AxisVector)
            : currentAngle;
    }

    private void LateUpdate()
    {
        UpdateHands();

        float angle;
        bool holding = gripHand != -1;

        if (holding)
        {
            angle = HandAngle() - grabOffset;
        }
        else
        {
            Quaternion delta = Quaternion.Inverse(restRotation) * transform.localRotation;
            Vector3 poleDir = Vector3.ProjectOnPlane(delta * Vector3.up, AxisVector);
            angle = poleDir.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(Vector3.up, poleDir, AxisVector)
                : currentAngle;
        }

        angle = Mathf.Clamp(angle, -maxAngle, maxAngle);

        if (!holding && snapOnRelease)
        {
            float stepAngle = (snapStepPercent / 100f) * (2f * maxAngle);
            float target = Mathf.Round((angle + maxAngle) / stepAngle) * stepAngle - maxAngle;
            angle = Mathf.Lerp(angle, Mathf.Clamp(target, -maxAngle, maxAngle), Time.deltaTime * snapSpeed);
        }

        currentAngle = angle;

        transform.localRotation = restRotation * Quaternion.AngleAxis(angle, AxisVector);
        transform.localPosition = restPosition;

        float value = Mathf.InverseLerp(-maxAngle, maxAngle, angle);
        if (invertDirection) value = 1f - value;
        Value01 = value;

        if (Mathf.Abs(value - lastSentValue) > 0.001f)
        {
            lastSentValue = value;
            onValueChanged?.Invoke(value);
        }
    }

    public void ResetLever()
    {
        gripHand = -1;
        knob.localScale = knobBaseScale;
        currentAngle = 0f;
        transform.localRotation = restRotation;
    }
}