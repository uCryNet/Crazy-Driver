using Ashsvp;
using System.Collections;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Game Manager")][Space(10)]
    
    public Rigidbody player;
    public SimcadeVehicleController vehicle;

    [Tooltip("Label that shows the finish message. Its object is enabled on win")]
    public TMP_Text info;

    [Tooltip("Label that counts down the seconds left")]
    public TMP_Text timer;

    [Header("Level Set Up")][Space(10)]
    [Tooltip("Seconds to reach the finish, counted from the GO message")]
    public int timeLimit = 60;

    private bool isLevelCompleted;
    
    private const string FinishText = "FINISH!";
    private const string LooserText = "LOOSER!";
    private const string StartText = "GO!";
    private const int CountdownFrom = 3;
    private const float CountdownStep = 1f;
    private const float GoMessageTime = 2f;
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
        LooseLevel();
    }

    // Called by FinishDetector once the car has stood still inside the finish zone
    public void WinLevel()
    {
        if (isLevelCompleted) return;

        EndLevel(FinishText);
    }

    // Called by KillZoneDetector, which rides on the player and reports what it drove into
    public void LooseLevel()
    {
        if (isLevelCompleted) return;

        EndLevel(LooserText);
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
}
