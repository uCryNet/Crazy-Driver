using System.Collections;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Finish Conditions")][Space(10)]
    
    public Rigidbody player;
    public Collider finishZone;
    public TrafficManager trafficManager;

    [Tooltip("Label that shows the finish message. Its object is enabled on win")]
    public TMP_Text info;

    [Tooltip("Speed (m/s) at or below which the car counts as stopped")]
    public float stopSpeedThreshold = 0.5f;

    [Tooltip("How long the car has to stay stopped on the platform. Zero wins the moment it stops")]
    public float requiredStopTime = 0.2f;

    private float stoppedTime;
    private bool isLevelCompleted;
    
    private const string FinishText = "FINISH!";
    private const string StartText = "GO!";
    private const int CountdownFrom = 3;
    private const float CountdownStep = 1f;
    private const float GoMessageTime = 2f;

    private void Start()
    {
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        info.gameObject.SetActive(true);

        player.constraints = RigidbodyConstraints.FreezeAll;

        for (int count = CountdownFrom; count > 0; count--)
        {
            info.text = count.ToString();
            yield return new WaitForSeconds(CountdownStep);
        }

        player.constraints = RigidbodyConstraints.None;

        info.text = StartText;
        yield return new WaitForSeconds(GoMessageTime);

        info.gameObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (isLevelCompleted)
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
            isLevelCompleted = true;

            StopAllCoroutines();

            info.text = FinishText;
            info.gameObject.SetActive(true);

            player.constraints = RigidbodyConstraints.FreezeAll;
            trafficManager.StopTraffic();
        }
    }

    private bool IsPlayerOnFinish()
    {
        Vector3 position = player.position;

        return finishZone.ClosestPoint(position) == position;
    }
}
