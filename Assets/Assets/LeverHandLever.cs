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

    [Header("Grid snapping")]
    [Tooltip("Size of one grid step as % of the full range. 10 = 11 positions (0,10,...,100).")]
    [SerializeField, Range(1f, 50f)] private float snapStepPercent = 10f;
    [Tooltip("Hand must pass this far beyond a step boundary (0-0.5 of a step) before it switches. Prevents flickering.")]
    [SerializeField, Range(0f, 0.4f)] private float hysteresis = 0.15f;
    [Tooltip("If on, the lever jumps instantly between grid positions. If off, it glides to them.")]
    [SerializeField] private bool instantSnap = false;
    [SerializeField] private float glideSpeed = 25f;

    [Header("Events")]
    public UnityEvent<float> onValueChanged;    
    public UnityEvent<float> onReleased;
    public UnityEvent<int> onStepChanged;       

    public float Value01 { get; private set; } = 0.5f;
    public int NaturePercent => Mathf.RoundToInt(Value01 * 100f);
    public int EfficiencyPercent => 100 - NaturePercent;
    public int StepIndex => stepIndex;
    public int StepCount => stepCount;

    private Quaternion restRotation;
    private Vector3 restPosition;
    private float grabOffset;
    private float displayAngle;
    private int stepIndex;
    private int stepCount;
    private float stepAngle;
    private int lastSentStep = -1;

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

        RebuildGrid();
        stepIndex = stepCount / 2;       
        displayAngle = AngleOfStep(stepIndex);

        Debug.Log($"[LEVER] LeverHandLever is running. Knob = {knob.name}, {stepCount + 1} grid positions");
    }

    private void OnValidate() => RebuildGrid();

    private void RebuildGrid()
    {
        stepCount = Mathf.Max(1, Mathf.RoundToInt(100f / snapStepPercent));
        stepAngle = (2f * maxAngle) / stepCount;
    }

    private float AngleOfStep(int i) => -maxAngle + i * stepAngle;

    private int AngleToStep(float rawAngle)
    {
        rawAngle = Mathf.Clamp(rawAngle, -maxAngle, maxAngle);

        float distFromCurrent = Mathf.Abs(rawAngle - AngleOfStep(stepIndex));
        if (distFromCurrent < stepAngle * (0.5f + hysteresis))
            return stepIndex;

        return Mathf.Clamp(Mathf.RoundToInt((rawAngle + maxAngle) / stepAngle), 0, stepCount);
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
        grabOffset = HandAngle() - AngleOfStep(stepIndex);
        knob.localScale = knobBaseScale * 1.15f;
        Debug.Log("[LEVER] GRABBED with " + (hand == 0 ? "left" : "right") + " hand");
    }

    private void EndGrab()
    {
        gripHand = -1;
        knob.localScale = knobBaseScale;
        onReleased?.Invoke(Value01);
        Debug.Log($"[LEVER] Released at step {stepIndex}/{stepCount}: {EfficiencyPercent}% efficiency, {NaturePercent}% nature");
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
            : AngleOfStep(stepIndex);
    }

    private void LateUpdate()
    {
        UpdateHands();

        if (gripHand != -1)
            stepIndex = AngleToStep(HandAngle() - grabOffset);

        float targetAngle = AngleOfStep(stepIndex);
        if (instantSnap)
        {
            displayAngle = targetAngle;
        }
        else
        {
            displayAngle = Mathf.Lerp(displayAngle, targetAngle, 1f - Mathf.Exp(-glideSpeed * Time.deltaTime));
            if (Mathf.Abs(displayAngle - targetAngle) < 0.05f) displayAngle = targetAngle;
        }

        transform.localRotation = restRotation * Quaternion.AngleAxis(displayAngle, AxisVector);
        transform.localPosition = restPosition;

        float value = (float)stepIndex / stepCount;
        if (invertDirection) value = 1f - value;
        Value01 = value;

        if (stepIndex != lastSentStep)
        {
            lastSentStep = stepIndex;
            onValueChanged?.Invoke(value);
            onStepChanged?.Invoke(stepIndex);
        }
    }

    public void ResetLever()
    {
        gripHand = -1;
        knob.localScale = knobBaseScale;
        stepIndex = stepCount / 2;
        displayAngle = AngleOfStep(stepIndex);
        transform.localRotation = restRotation * Quaternion.AngleAxis(displayAngle, AxisVector);
    }
}