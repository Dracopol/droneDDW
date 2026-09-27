using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraEnvironmentToggle : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Color solidColor = new Color(0f, 0f, 0f, 0f);

    private bool showingSkybox = true;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        showingSkybox = targetCamera.clearFlags == CameraClearFlags.Skybox;

        if (!showingSkybox)
            targetCamera.backgroundColor = solidColor;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            showingSkybox = !showingSkybox;
            targetCamera.clearFlags = showingSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = solidColor;
        }
    }
}
