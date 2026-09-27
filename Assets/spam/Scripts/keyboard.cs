using UnityEngine;
using UnityEngine.InputSystem;

public class keyboard : MonoBehaviour
{
    [SerializeField] private GameObject stampMark;
    [SerializeField] private GameObject stamp;
    [SerializeField] private Stamp stampScript;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;
    [SerializeField] private GameObject buttonA;
    [SerializeField] private button buttonAScript;
    [SerializeField] private GameObject buttonB;
    [SerializeField] private button buttonBScript;

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

            buttonA.SetActive(false);
            buttonB.SetActive(false);
        }

        if (Keyboard.current != null && Keyboard.current.dKey.wasPressedThisFrame)
        {
            if (buttonAScript != null)
                buttonAScript.ResetButton();
            if (buttonBScript != null)
                buttonBScript.ResetButton();

            buttonA.SetActive(true);
            buttonB.SetActive(true);

            stamp.SetActive(false);
            stampMark.SetActive(false);

            if (audioSource != null && popSound != null)
                audioSource.PlayOneShot(popSound);
        }
    }
}
