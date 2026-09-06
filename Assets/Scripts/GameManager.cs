using System.Collections;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Finish Conditions")][Space(10)]
    
    public Rigidbody player;
    public Collider finishZone;

    [Tooltip("Label that shows the finish message. Its object is enabled on win")]
    public TMP_Text info;

    [Tooltip("Label that counts down the seconds left")]
    public TMP_Text timer;

    [Tooltip("Seconds to reach the finish, counted from the GO message")]
    public int timeLimit = 60;

    [Tooltip("Speed (m/s) at or below which the car counts as stopped")]
    public float stopSpeedThreshold = 0.5f;

    [Tooltip("How long the car has to stay stopped on the platform. Zero wins the moment it stops")]
    public float requiredStopTime = 0.2f;

    private float stoppedTime;
    private bool isLevelCompleted;
    
    private const string FinishText = "FINISH!";
    private const string LooserText = "LOOSER!";
    private const string StartText = "GO!";
    private const int CountdownFrom = 3;
    private const float CountdownStep = 1f;
    private const float GoMessageTime = 2f;

    private void Start()
    {
        timer.text = timeLimit.ToString();

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
        StartCoroutine(TimerRoutine());

        yield return new WaitForSeconds(GoMessageTime);

        info.gameObject.SetActive(false);
    }

    private IEnumerator TimerRoutine()
    {
        for (int secondsLeft = timeLimit; secondsLeft > 0; secondsLeft--)
        {
            timer.text = secondsLeft.ToString();
            yield return new WaitForSeconds(1f);
        }

        timer.text = "0";
        EndLevel(LooserText);
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
            EndLevel(FinishText);
        }
    }

    private void EndLevel(string message)
    {
        isLevelCompleted = true;

        // Stops the countdown and the timer from writing over the message
        StopAllCoroutines();

        info.text = message;
        info.gameObject.SetActive(true);

        player.constraints = RigidbodyConstraints.FreezeAll;
    }

    private bool IsPlayerOnFinish()
    {
        Vector3 position = player.position;

        return finishZone.ClosestPoint(position) == position;
    }
}
