using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class quicktest : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float responsiveness = 4f;
    [SerializeField] private float verticalSpeed = 4f;
    [SerializeField] private float yawSpeed = 90f;

    [Header("Tilt")]
    [SerializeField] private float maxTiltAngle = 30f;
    [SerializeField] private float degreesPerAcceleration = 2.5f;
    [SerializeField] private float tiltSmoothTime = 0.12f;

    [Header("Idle wobble")]
    [SerializeField] private float wobbleAngle = 0.8f;
    [SerializeField] private float wobbleSpeed = 0.5f;

    private Rigidbody body;
    private float yaw;
    private float pitch;
    private float pitchVelocity;
    private float roll;
    private float rollVelocity;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.freezeRotation = true;

        yaw = transform.eulerAngles.y;
    }

    private void FixedUpdate()
    {
        Vector2 stick = ReadStick();
        float lift = ReadLift();
        float turn = ReadYaw();

        yaw += turn * yawSpeed * Time.fixedDeltaTime;
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);

        Vector3 wish = heading * new Vector3(stick.x, 0f, stick.y);
        if (wish.sqrMagnitude > 1f)
            wish.Normalize();

        // Target velocity, then accelerate toward it. The gap between where we are
        // and where we want to be is what the rotors have to fight, so it is also
        // what the body leans into.
        Vector3 targetVelocity = wish * maxSpeed + Vector3.up * (lift * verticalSpeed);
        Vector3 acceleration = (targetVelocity - body.linearVelocity) * responsiveness;
        body.AddForce(acceleration, ForceMode.Acceleration);

        // Tilt follows acceleration, expressed relative to where the nose points.
        Vector3 localAcceleration = Quaternion.Inverse(heading) * new Vector3(acceleration.x, 0f, acceleration.z);
        float targetPitch = Mathf.Clamp(localAcceleration.z * degreesPerAcceleration, -maxTiltAngle, maxTiltAngle);
        float targetRoll = Mathf.Clamp(-localAcceleration.x * degreesPerAcceleration, -maxTiltAngle, maxTiltAngle);

        targetPitch += Wobble(0f);
        targetRoll += Wobble(31.7f);

        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, tiltSmoothTime);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, tiltSmoothTime);

        body.MoveRotation(Quaternion.Euler(pitch, yaw, roll));
    }

    private float Wobble(float seed)
    {
        return (Mathf.PerlinNoise(Time.time * wobbleSpeed + seed, seed) - 0.5f) * 2f * wobbleAngle;
    }

    private Vector2 ReadStick()
    {
        if (Gamepad.current != null)
        {
            Vector2 gamepadStick = Gamepad.current.leftStick.ReadValue();
            if (gamepadStick.sqrMagnitude > 0.01f)
                return gamepadStick;
        }

        if (Keyboard.current == null)
            return Vector2.zero;

        float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
        float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    private float ReadLift()
    {
        if (Gamepad.current != null)
        {
            float gamepadLift = Gamepad.current.rightStick.ReadValue().y;
            if (Mathf.Abs(gamepadLift) > 0.1f)
                return gamepadLift;
        }

        if (Keyboard.current == null)
            return 0f;

        return (Keyboard.current.spaceKey.isPressed ? 1f : 0f) - (Keyboard.current.leftCtrlKey.isPressed ? 1f : 0f);
    }

    private float ReadYaw()
    {
        if (Gamepad.current != null)
        {
            float gamepadYaw = Gamepad.current.rightStick.ReadValue().x;
            if (Mathf.Abs(gamepadYaw) > 0.1f)
                return gamepadYaw;
        }

        if (Keyboard.current == null)
            return 0f;

        return (Keyboard.current.eKey.isPressed ? 1f : 0f) - (Keyboard.current.qKey.isPressed ? 1f : 0f);
    }
}
