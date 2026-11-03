using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TutorialZone : MonoBehaviour
{
    public TutorialZoneId zone;
    private bool used;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player")) return;
        used = true;
        TutorialEvents.NotifyZoneEntered(zone);
    }
}