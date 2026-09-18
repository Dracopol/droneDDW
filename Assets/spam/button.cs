using System.Collections;
using UnityEngine;

public class button : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip popSound;
    [SerializeField] private AudioClip laserSound;
    [SerializeField] private GameObject laserObject;
    [SerializeField] private float laserBlinkSpeed = 0.06f;
    [SerializeField] private float laserDuration = 2f;
    [SerializeField] private GameObject stampMark;
    [SerializeField] private bool isB;

    private bool wasPressed;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("fingertip"))
            return;

        if (audioSource != null && popSound != null)
            audioSource.PlayOneShot(popSound);

        if (!wasPressed)
            StartCoroutine(LaserSequence());

        wasPressed = true;
    }

    private IEnumerator LaserSequence()
    {
        if (audioSource != null && laserSound != null)
            audioSource.PlayOneShot(laserSound);

        float elapsed = 0f;
        while (elapsed < laserDuration)
        {
            laserObject.SetActive(!laserObject.activeSelf);
            yield return new WaitForSeconds(laserBlinkSpeed);
            elapsed += laserBlinkSpeed;
        }

        laserObject.SetActive(false);

        if (stampMark != null)
        {
            float x = isB ? 0.2f : -0.2f;
            stampMark.transform.position = new Vector3(x, 0.975f, 0.3f);
            stampMark.SetActive(true);
        }
    }

    public void ResetButton()
    {
        wasPressed = false;
    }
}
