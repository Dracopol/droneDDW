using UnityEngine;

public class LeverAudio : MonoBehaviour
{
    [SerializeField] private LeverHandLever lever;
    [SerializeField] private AudioSource droneSource;
    [SerializeField] private AudioSource birdSource;

    [Tooltip("How far past the center (0.5) the lever must go. 0.2 means below 0.3 or above 0.7.")]
    [SerializeField, Range(0.05f, 0.5f)] private float threshold = 0.2f;

    private enum Side { Center, Left, Right }
    private Side current = Side.Center;

    private void OnEnable()
    {
        if (lever != null) lever.onValueChanged.AddListener(OnLeverMoved);
    }

    private void OnDisable()
    {
        if (lever != null) lever.onValueChanged.RemoveListener(OnLeverMoved);
    }

    private void OnLeverMoved(float value)
    {
        Side next = Side.Center;
        if (value < 0.5f - threshold) next = Side.Left;
        else if (value > 0.5f + threshold) next = Side.Right;

        if (next == current) return;
        current = next;

        droneSource.Stop();
        birdSource.Stop();

        if (next == Side.Left) droneSource.Play();
        else if (next == Side.Right) birdSource.Play();
    }
}