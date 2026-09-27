using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Stamp : MonoBehaviour
{
    [SerializeField] private Transform stampBottom;
    [SerializeField] private Collider paperA;
    [SerializeField] private Collider paperB;
    [SerializeField] private DecalProjector stampMark;
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
            stampMark.gameObject.SetActive(false);
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

        // Decals project along their local +Z, so tilt 90 degrees to point down at the paper, keeping the stamp's yaw
        Quaternion markRotation = Quaternion.Euler(90f, stampBottom.eulerAngles.y, 0f);

        // Centre the projection box on the paper surface, whatever the projector's pivot is set to
        markPosition -= markRotation * stampMark.pivot;

        stampMark.transform.SetParent(paper.transform, true);
        stampMark.transform.SetPositionAndRotation(markPosition, markRotation);
        stampMark.gameObject.SetActive(true);

        if (audioSource != null && stampSound != null)
            audioSource.PlayOneShot(stampSound);

        hasStamped = true;
    }

    public void ResetStamp()
    {
        hasStamped = false;

        if (stampMark != null)
            stampMark.gameObject.SetActive(false);
    }
}
