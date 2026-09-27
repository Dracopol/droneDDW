using UnityEngine;

public class Stamp : MonoBehaviour
{
    [SerializeField] private Transform stampBottom;
    [SerializeField] private Collider paperA;
    [SerializeField] private Collider paperB;
    [SerializeField] private GameObject stampMark;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stampSound;

    private bool hasStamped;

    private void Awake()
    {
        if (stampBottom == null)
            stampBottom = transform;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (stampMark != null)
            stampMark.SetActive(false);
    }

    public void HandleStampTrigger(Collider other, Transform triggerZone)
    {
        if (hasStamped)
            return;

        Collider paper = other == paperA ? paperA
            : other == paperB ? paperB
            : null;

        if (paper == null)
            return;

        if (stampMark == null)
            return;

        Vector3 markPosition = new Vector3(triggerZone.position.x, paper.bounds.max.y, triggerZone.position.z);

        Vector3 markEuler = stampBottom.eulerAngles;
        markEuler.x = 0f;
        markEuler.z = 0f;

        stampMark.transform.SetParent(paper.transform, true);
        stampMark.transform.SetPositionAndRotation(markPosition, Quaternion.Euler(markEuler));
        stampMark.SetActive(true);

        if (audioSource != null && stampSound != null)
            audioSource.PlayOneShot(stampSound);

        hasStamped = true;
    }

    public void ResetStamp()
    {
        hasStamped = false;

        if (stampMark != null)
            stampMark.SetActive(false);
    }
}
