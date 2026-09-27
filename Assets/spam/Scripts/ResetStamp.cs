using UnityEngine;

public class ResetStamp : MonoBehaviour
{
    [SerializeField] private Transform stamp;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;
    [SerializeField] private float settleDuration = 0.5f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody stampRigidbody;
    private float settleTimer;

    private void Start()
    {
        originalPosition = stamp.position;
        originalRotation = stamp.rotation;
        stampRigidbody = stamp.GetComponent<Rigidbody>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void FixedUpdate()
    {
        if (stamp.position.y < 0f)
            Respawn();

        if (settleTimer > 0f)
            Settle();
    }

    private void Respawn()
    {
        if (stampRigidbody != null)
        {
            stampRigidbody.position = originalPosition;
            stampRigidbody.rotation = originalRotation;
            settleTimer = settleDuration;
        }

        stamp.SetPositionAndRotation(originalPosition, originalRotation);

        if (audioSource != null && popSound != null)
            audioSource.PlayOneShot(popSound);
    }

    private void Settle()
    {
        // Grabbed again (XRI makes it kinematic while held), stop settling so the next throw isn't killed
        if (stampRigidbody.isKinematic)
        {
            settleTimer = 0f;
            return;
        }

        settleTimer -= Time.fixedDeltaTime;
        stampRigidbody.linearVelocity = Vector3.zero;
        stampRigidbody.angularVelocity = Vector3.zero;
    }
}
