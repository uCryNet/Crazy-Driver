using UnityEngine;

public class KillZoneDetector : MonoBehaviour
{
    public GameManager gameManager;

    private const string KillZoneTag = "KillZone";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(KillZoneTag))
        {
            gameManager.LooseLevel();
        }
    }
}
