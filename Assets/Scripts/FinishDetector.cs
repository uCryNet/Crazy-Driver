using UnityEngine;

/*
 * player.linearVelocity.magnitude - speed of player
 * enabled = <bool> - off/on the script
*/

[RequireComponent(typeof(Rigidbody))]
public class FinishDetector : MonoBehaviour
{
    public GameManager gameManager;

    private const string FinishTag = "Finish";
    private const float StopSpeedThreshold = 0.5f; // Speed (m/s) at or below which the car counts as stopped
    private const float RequiredStopTime = 0.2f; // How long the car has to stand still inside. Zero wins the moment it stops

    private Rigidbody body;
    private bool isInside;
    private float stoppedTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!isInside || body.linearVelocity.magnitude > StopSpeedThreshold)
        {
            stoppedTime = 0f;
            return;
        }

        stoppedTime += Time.fixedDeltaTime;

        if (stoppedTime >= RequiredStopTime)
        {
            enabled = false; // disabled FinishDetector if we win
            gameManager.WinLevel();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(FinishTag))
        {
            isInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(FinishTag))
        {
            isInside = false;
        }
    }
}
