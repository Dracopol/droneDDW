using UnityEngine;

public class Stamp : MonoBehaviour
{
    [SerializeField] private Transform stampBottom;
    [SerializeField] private Collider paperA;
    [SerializeField] private Collider paperB;
    [SerializeField] private GameObject stampMark;

    private bool hasStamped;

    private void Awake()
    {
        if (stampBottom == null)
            stampBottom = transform;

        Debug.Log($"[Stamp] {name}: Awake ran. stampBottom={stampBottom.name}, paperA={(paperA != null ? paperA.name : "NULL")}, paperB={(paperB != null ? paperB.name : "NULL")}, stampMark={(stampMark != null ? stampMark.name : "NULL")}", this);

        if (paperA == null)
            Debug.LogWarning($"[Stamp] {name}: Paper A is not assigned.", this);
        if (paperB == null)
            Debug.LogWarning($"[Stamp] {name}: Paper B is not assigned.", this);
        if (stampMark == null)
            Debug.LogWarning($"[Stamp] {name}: Stamp Mark is not assigned.", this);
        else
            stampMark.SetActive(false);
    }

    public void HandleStampTrigger(Collider other, Transform triggerZone)
    {
        Debug.Log($"[Stamp] {name}: trigger entered by '{other.name}' (hasStamped={hasStamped})", this);

        if (hasStamped)
        {
            Debug.Log($"[Stamp] {name}: ignoring trigger, already stamped this cycle.", this);
            return;
        }

        Collider paper = other == paperA ? paperA
            : other == paperB ? paperB
            : null;

        if (paper == null)
        {
            Debug.Log($"[Stamp] {name}: '{other.name}' is not paperA or paperB, ignoring.", this);
            return;
        }

        if (stampMark == null)
        {
            Debug.LogError($"[Stamp] {name}: cannot place mark, Stamp Mark is not assigned.", this);
            return;
        }

        Vector3 markPosition = new Vector3(triggerZone.position.x, paper.bounds.max.y, triggerZone.position.z);

        Vector3 markEuler = stampBottom.eulerAngles;
        markEuler.x = 0f;
        markEuler.z = 0f;

        Debug.Log($"[Stamp] {name}: stamping '{paper.name}' at {markPosition}, rotation euler {markEuler}.", this);

        stampMark.transform.SetParent(paper.transform, true);
        stampMark.transform.SetPositionAndRotation(markPosition, Quaternion.Euler(markEuler));
        stampMark.SetActive(true);

        hasStamped = true;
    }

    public void ResetStamp()
    {
        Debug.Log($"[Stamp] {name}: stamp reset, ready to stamp again.", this);
        hasStamped = false;

        if (stampMark != null)
            stampMark.SetActive(false);
    }
}
