using Ashsvp;
using System.Collections;
using TMPro;
using UnityEngine;

/*
 * player.linearVelocity.magnitude - speed of player
 */

public class GameManager : MonoBehaviour
{
    [Header("Game Manager")][Space(10)]
    
    public Rigidbody player;
    public SimcadeVehicleController vehicle;
    public Collider finishZone;

    [Tooltip("Label that shows the finish message. Its object is enabled on win")]
    public TMP_Text info;

    [Tooltip("Label that counts down the seconds left")]
    public TMP_Text timer;

    [Header("Level Set Up")][Space(10)]
    [Tooltip("Seconds to reach the finish, counted from the GO message")]
    public int timeLimit = 60;

    private float stoppedTime;
    private bool isLevelCompleted;
    
    private const string FinishText = "FINISH!";
    private const string LooserText = "LOOSER!";
    private const string StartText = "GO!";
    private const int CountdownFrom = 3;
    private const float CountdownStep = 1f;
    private const float GoMessageTime = 2f;
    private const float StopSpeedThreshold = 0.5f; // Speed (m/s) at or below which the car counts as stopped
    private const float RequiredStopTime = 0.2f; // How long the car has to stay stopped on the platform. Zero wins the moment it stops
    private bool IsGrounded => vehicle.vehicleIsGrounded;

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
        if (isLevelCompleted) return;

        // Loose condition. Time is up
        if (!IsPlayerOnFinish() || player.linearVelocity.magnitude > StopSpeedThreshold)
        {
            stoppedTime = 0f;
            return;
        }

        stoppedTime += Time.fixedDeltaTime;

        // Win condition
        if (stoppedTime >= RequiredStopTime)
        {
            EndLevel(FinishText);
        }
    }

    private void EndLevel(string message)
    {
        isLevelCompleted = true;
        
        StopAllCoroutines(); // Stops the countdown and the timer

        info.text = message;
        info.gameObject.SetActive(true);

        StartCoroutine(FreezePlayerRoutine());
    }

    private IEnumerator FreezePlayerRoutine()
    {
        player.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;

        while (!IsGrounded)
        {
            yield return new WaitForFixedUpdate();
        }

        player.constraints = RigidbodyConstraints.FreezeAll;
    }

    private bool IsPlayerOnFinish()
    {
        Vector3 position = player.position;

        return finishZone.ClosestPoint(position) == position;
    }
}
