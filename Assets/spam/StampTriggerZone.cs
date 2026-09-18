using UnityEngine;

public class StampTriggerZone : MonoBehaviour
{
    [SerializeField] private Stamp stamp;

    private void Awake()
    {
        if (stamp == null)
            stamp = GetComponentInParent<Stamp>();
    }

    private void OnTriggerEnter(Collider other)
    {
        stamp.HandleStampTrigger(other, transform);
    }
}
