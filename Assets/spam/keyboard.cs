using UnityEngine;
using UnityEngine.InputSystem;

public class keyboard : MonoBehaviour
{
    [SerializeField] private GameObject stampMark;
    [SerializeField] private GameObject stamp;
    [SerializeField] private Stamp stampScript;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;

    private Vector3 stampOriginalPosition;
    private Rigidbody stampRigidbody;

    private void Start()
    {
        stampOriginalPosition = stamp.transform.position;
        stampRigidbody = stamp.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame)
        {
            if (stampScript != null)
                stampScript.ResetStamp();

            stampMark.SetActive(false);
            stamp.SetActive(false);

            stamp.transform.position = stampOriginalPosition;

            if (stampRigidbody != null)
            {
                stampRigidbody.linearVelocity = Vector3.zero;
                stampRigidbody.angularVelocity = Vector3.zero;
            }

            stamp.SetActive(true);

            if (audioSource != null && popSound != null)
                audioSource.PlayOneShot(popSound);
        }
    }
}
