using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Finish Conditions")][Space(10)]
    
    public Rigidbody player;
    public Collider finishZone;

    [Tooltip("Speed (m/s) at or below which the car counts as stopped")]
    public float stopSpeedThreshold = 0.5f;

    [Tooltip("How long the car has to stay stopped on the platform. Zero wins the moment it stops")]
    public float requiredStopTime = 0.2f;

    private float stoppedTime;
    private bool levelCompleted;

    private void FixedUpdate()
    {
        if (levelCompleted)
        {
            return;
        }

        if (!IsPlayerOnFinish() || player.linearVelocity.magnitude > stopSpeedThreshold)
        {
            stoppedTime = 0f;
            return;
        }

        stoppedTime += Time.fixedDeltaTime;

        if (stoppedTime >= requiredStopTime)
        {
            levelCompleted = true;
            Debug.Log("===");
            Debug.Log("LEVEL COMPLETE!");
            Debug.Log("===");
        }
    }

    private bool IsPlayerOnFinish()
    {
        Vector3 position = player.position;

        return finishZone.ClosestPoint(position) == position;
    }
}
