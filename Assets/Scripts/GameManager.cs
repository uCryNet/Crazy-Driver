using Ashsvp;
using Unity.Cinemachine;
using System.Collections;
using TMPro;
using UnityEngine;

/*
 *  Invoke - is Unity's setTimeout
*/

public class GameManager : MonoBehaviour
{
    [Header("Game Manager")][Space(10)]
    
    public Rigidbody player;

    [Tooltip("Label that shows the finish message. Its object is enabled on win")]
    public TMP_Text info;

    [Tooltip("Label that counts down the seconds left")]
    public TMP_Text timer;

    [Header("Level Set Up")][Space(10)]
    [Tooltip("Seconds to reach the finish, counted from the GO message")]
    public int timeLimit = 60;

    private SimcadeVehicleController vehicle;
    private PlayerMovement playerMovement;
    private CinemachineCamera playerCamera;
    private bool isLevelCompleted;
    
    private const string FinishText = "FINISH!";
    private const string LooserText = "LOOSER!";
    private const string StartText = "GO!";
    private const int CountdownFrom = 3;
    private const float CountdownStep = 1f;
    private const float GoMessageTime = 2f;
    private const float FallTimeAfterFrozen = 2f;

    private void Awake()
    {
        vehicle = player.GetComponent<SimcadeVehicleController>();
        playerMovement = player.GetComponent<PlayerMovement>();
        playerCamera = player.GetComponentInChildren<CinemachineCamera>();
    }

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

    public void WinLevel()
    {
        if (isLevelCompleted) return;

        EndLevel(FinishText);
    }

    // Called by KillZoneDetector - the car is left to fall through the world, so the camera stays behind
    public void KillPlayer()
    {
        if (isLevelCompleted) return;

        playerCamera.enabled = false;
        
        Invoke(nameof(FreezePlayer), FallTimeAfterFrozen);

        LooseLevel();
    }

    private void FreezePlayer()
    {
        player.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void LooseLevel()
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

        DisablePlayerControl();
    }

    private void DisablePlayerControl()
    {
        vehicle.CanDrive = false; // The controller's own switch - it zeroes steering and throttle and pulls the handbrake, physics keeps running
        
        playerMovement.enabled = false; //  The jump lives outside the controller, so it is switched off on its own
    }
}
