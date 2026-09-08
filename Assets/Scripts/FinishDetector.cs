using UnityEngine;

public class FinishDetector : MonoBehaviour
{
    public GameManager gameManager;

    private const string FinishTag = "Finish";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(FinishTag))
        {
            gameManager.SetOnFinish(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(FinishTag))
        {
            gameManager.SetOnFinish(false);
        }
    }
}
