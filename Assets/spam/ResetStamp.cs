using UnityEngine;

public class ResetStamp : MonoBehaviour
{
    [SerializeField] private Transform stamp;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;

    private Vector3 originalPosition;
    private Rigidbody stampRigidbody;

    private void Start()
    {
        originalPosition = stamp.position;
        stampRigidbody = stamp.GetComponent<Rigidbody>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (stamp.position.y < 0f)
        {
            stamp.position = originalPosition;

            if (stampRigidbody != null)
            {
                stampRigidbody.linearVelocity = Vector3.zero;
                stampRigidbody.angularVelocity = Vector3.zero;
            }

            if (audioSource != null && popSound != null)
                audioSource.PlayOneShot(popSound);
        }
    }
}
