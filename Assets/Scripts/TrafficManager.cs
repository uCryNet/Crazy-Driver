using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrafficManager : MonoBehaviour
{
    [Header("Traffic Manager")][Space(10)]

    public Transform player;
    public float activationDistance = 120f;
    public float deactivationDistance = 160f;
    
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

        // Everything starts off, so the cars don't have to be disabled by hand in the
        // Inspector. The first check below switches on whatever is already near the player.
        foreach (GameObject car in aiCars)
        {
            car.SetActive(false);
        }

        InvokeRepeating(nameof(UpdateAiCars), 0f, CheckInterval);
    }

    /// <summary>
    /// GameObject.FindGameObjectsWithTag skips inactive objects, and cars may already be
    /// disabled in the scene, so the scene roots are walked once instead.
    /// </summary>
    private void CollectAiCars()
    {
        aiCars.Clear();

        Scene scene = gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                // Plain string compare, not CompareTag: an unknown tag makes CompareTag
                // throw, which would abort Start and leave every car switched on.
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

    private void UpdateAiCars()
    {
        if (player == null)
        {
            return;
        }

        // Comparing squared distances keeps the square root out of the loop.
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
            else if (sqrDistance <= activationSqr)
            {
                car.SetActive(true);
            }
        }
    }
}
