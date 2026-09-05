using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrafficManager : MonoBehaviour
{
    [Header("Traffic Manager")][Space(10)]

    public Transform player;
    public float activationDistance = 150f;
    public float deactivationDistance = 180f;
    
    [Tooltip("Check this distance from a spot to see if it's occupied by another car")]
    public float minSpawnClearance = 5f;
    
    private const string TrafficTag = "Traffic";
    private const float CheckInterval = 1f;
    private readonly List<GameObject> aiCars = new List<GameObject>();

    private void Start()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindAnyObjectByType<PlayerMovement>();
            if (playerMovement != null)
            {
                player = playerMovement.transform;
            }
        }

        CollectAiCars();
        
        foreach (GameObject car in aiCars)
        {
            car.SetActive(false);
        }

        InvokeRepeating(nameof(UpdateAiCars), 0f, CheckInterval);
    }
    
    private void CollectAiCars()
    {
        aiCars.Clear();

        Scene scene = gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.CompareTag(TrafficTag))
                {
                    aiCars.Add(child.gameObject);
                }
            }
        }

        if (aiCars.Count == 0)
        {
            Debug.LogWarning($"TrafficManager found no cars tagged '{TrafficTag}' in scene '{scene.name}', so none will be managed.", this);
        }
    }

    // Called on level completion - the cars keep standing where they are instead of vanishing
    public void StopTraffic()
    {
        CancelInvoke(nameof(UpdateAiCars));

        foreach (GameObject car in aiCars)
        {
            car.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void UpdateAiCars()
    {
        if (player == null)
        {
            return;
        }

        // Comparing squared distances keeps the square root out of the loop
        float activationSqr = activationDistance * activationDistance;
        float deactivationSqr = deactivationDistance * deactivationDistance;
        Vector3 playerPosition = player.position;

        foreach (GameObject car in aiCars)
        {
            if (car == null)
            {
                continue;
            }

            float sqrDistance = (car.transform.position - playerPosition).sqrMagnitude;

            if (car.activeSelf)
            {
                if (sqrDistance > deactivationSqr)
                {
                    car.SetActive(false);
                }
            }
            else if (sqrDistance <= activationSqr && !IsSpotOccupied(car, playerPosition))
            {
                car.SetActive(true);
            }
        }
    }

    // Check a spot under disabled cars to see if it's occupied by another car. If so, don't activate
    private bool IsSpotOccupied(GameObject car, Vector3 playerPosition)
    {
        float clearanceSqr = minSpawnClearance * minSpawnClearance;
        Vector3 position = car.transform.position;

        if ((playerPosition - position).sqrMagnitude <= clearanceSqr)
        {
            return true;
        }
        
        foreach (GameObject other in aiCars)
        {
            if (other == null || other == car || !other.activeSelf)
            {
                continue;
            }

            if ((other.transform.position - position).sqrMagnitude <= clearanceSqr)
            {
                return true;
            }
        }

        return false;
    }
}
