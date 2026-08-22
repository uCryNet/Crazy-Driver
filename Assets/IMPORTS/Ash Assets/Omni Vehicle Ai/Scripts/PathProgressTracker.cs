using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

namespace OmniVehicleAi
{
    public class PathProgressTracker : MonoBehaviour
    {
        public AIVehicleController aiVehicleController;
        public SplineContainer splineContainer; // The spline container holding the spline

        [Header("Start Point")]
        [Tooltip("Find the closest point on the spline in world space when progress is reset.\n\n" +
                 "Off = original behaviour: the search compares the world space vehicle position against " +
                 "the spline's local space knots, so a Spline Container that is not at the origin makes the " +
                 "vehicle start from a wrong point (often near the beginning of the spline) and steer " +
                 "towards it for the first frames.")]
        public bool accurateStartPoint = true;

        [Header("Offsets")]
        public float offset_A = 15f;
        public float offset_AB = 10f;
        public float offset_BC = 10f;

        public Vector3 progressPoint { get; private set; }
        public Vector3 progressTangent { get; private set; }
        public Vector3 progressRight { get; private set; }

        public float progressDistance { get; private set; } // The progress along the spline.

        [HideInInspector] public Vector3 A { get; private set; }
        [HideInInspector] public Vector3 B { get; private set; }
        [HideInInspector] public Vector3 C { get; private set; }
        [HideInInspector] public Vector3 D { get; private set; }
        private float speed;
        

        [Header("Lap Settings")]
        public int totalLaps = 3; // Total number of laps for the race
        public int currentLap = 1; // Current lap count
        public bool loopCircuit = true; // If true, the circuit loops for multiple laps

        private Spline spline;
        private float splineLength;

        public UnityEvent OnLapCompleted; // Event for lap completion
        public UnityEvent OnAllLapsCompleted; // Event for all laps completion

        private void Start()
        {
            if (aiVehicleController == null)
            {
                Debug.LogError("AIVehicleController component is missing on this GameObject.");
            }

            if (splineContainer == null)
            {
                Debug.LogError("SplineContainer not assigned and none found in the scene.");
                enabled = false;
                return;
            }

            spline = splineContainer.Splines[0];
            if (spline == null)
            {
                Debug.LogError("SplineContainer does not contain any splines.");
                enabled = false;
                return;
            }

            ResetProgress();
        }

        public void ResetProgress()
        {
            if (accurateStartPoint)
            {
                // Keep the cached spline in sync with the container, SetPath() can swap it at runtime.
                spline = splineContainer.Splines[0];

                // World space length, to match the world space points GetSplinePoint() returns.
                splineLength = splineContainer.CalculateLength();

                progressDistance = GetSplineDistanceToWorldPoint(aiVehicleController.vehicleTransform.position) + offset_A;
            }
            else
            {
                splineLength = spline.GetLength();

                Vector3 vehiclePosOnspline = GetClosestPointOnCircuit(aiVehicleController.vehicleTransform.position);
                progressDistance = GetSplineDistanceToPoint(vehiclePosOnspline) + offset_A;
            }

            //progressDistance = offset_A; //0f;
            currentLap = 1; // Reset the lap count
        }

        private void Update()
        {
            if (aiVehicleController == null || spline == null)
            {
                return;
            }

            speed = aiVehicleController.LocalVehiclevelocity.magnitude;

            // If loopCircuit is true, the positions can wrap around the spline.
            if (loopCircuit)
            {
                A = GetSplinePoint((progressDistance + offset_A) % splineLength);
                B = GetSplinePoint((progressDistance + offset_A + offset_AB) % splineLength);
                C = GetSplinePoint((progressDistance + offset_A + offset_AB + offset_BC) % splineLength);
                D = GetSplinePoint((progressDistance + offset_A + offset_AB + offset_BC + speed) % splineLength);
            }
            else
            {
                // Clamp the positions at the end of the spline when it's not looping.
                A = GetSplinePoint(Mathf.Min(progressDistance + offset_A, splineLength));
                B = GetSplinePoint(Mathf.Min(progressDistance + offset_A + offset_AB, splineLength));
                C = GetSplinePoint(Mathf.Min(progressDistance + offset_A + offset_AB + offset_BC, splineLength));
                D = GetSplinePoint(Mathf.Min(progressDistance + offset_A + offset_AB + offset_BC + speed, splineLength));
            }

            // Get the current progress point and tangent
            progressPoint = GetSplinePoint(progressDistance % splineLength);
            progressTangent = GetSplineTangent(progressDistance % splineLength);
            progressRight = Vector3.Cross(progressTangent, Vector3.up).normalized;

            Vector3 progressDelta = progressPoint - transform.position;

            // Adjust progress distance based on direction
            if (Vector3.Dot(progressDelta, progressTangent) < 0)
            {
                progressDistance += progressDelta.magnitude * 0.5f;
            }

            // Check if we've completed a lap
            if (progressDistance >= splineLength)
            {
                CompleteLap();
            }
        }

        private void CompleteLap()
        {
            if (loopCircuit)
            {
                progressDistance -= splineLength;// Continue with remaining distance past the spline length
            }

            if (currentLap <= totalLaps)
            {
                currentLap++;
            }

            OnLapCompleted?.Invoke();

            if(currentLap > totalLaps)
            {
                OnAllLapsCompleted?.Invoke();
            }

            // Handle looping logic if enabled
            if (loopCircuit && currentLap > totalLaps)
            {
                currentLap = 1; // Restart from the first lap
            }
        }

        public Vector3 GetClosestPointOnCircuit(Vector3 point)
        {
            if (spline == null)
            {
                Debug.LogError("Spline is not initialized.");
                return Vector3.zero;
            }

            float closestDistance = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;

            for (int i = 0; i < spline.Count - 1; i++)
            {
                float t0 = (float)i / (spline.Count - 1);
                float t1 = (float)(i + 1) / (spline.Count - 1);
                int steps = 10;

                for (int j = 0; j <= steps; j++)
                {
                    float t = Mathf.Lerp(t0, t1, j / (float)steps);
                    Vector3 splinePoint = spline.EvaluatePosition(t);
                    float distance = Vector3.Distance(point, splinePoint);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPoint = splinePoint;
                    }
                }
            }

            return closestPoint;
        }

        public Vector3 GetSplinePoint(float distance)
        {
            if (splineContainer == null)
            {
                Debug.LogError("SplineContainer is not assigned.");
                return Vector3.zero;
            }
            float normalizedDistance = distance / splineLength;
            return splineContainer.EvaluatePosition(normalizedDistance);
        }

        public Vector3 GetSplineTangent(float distance)
        {
            if (splineContainer == null)
            {
                Debug.LogError("SplineContainer is not assigned.");
                return Vector3.forward;
            }
            float normalizedDistance = distance / splineLength;

            Vector3 splineTangent = splineContainer.EvaluateTangent(normalizedDistance);

            return splineTangent.normalized;
        }

        public float GetSplineDistanceToPoint(Vector3 point)
        {
            if (spline == null)
            {
                Debug.LogError("Spline is not initialized.");
                return 0f;
            }

            float closestT = 0f; // Parameter on the spline that corresponds to the closest point
            float closestDistance = float.MaxValue;

            // Find the closest t-value on the spline to the given point
            for (int i = 0; i < spline.Count - 1; i++)
            {
                float t0 = (float)i / (spline.Count - 1);
                float t1 = (float)(i + 1) / (spline.Count - 1);
                int steps = 10; // Subdivision steps for finding closest point

                for (int j = 0; j <= steps; j++)
                {
                    float t = Mathf.Lerp(t0, t1, j / (float)steps);
                    Vector3 splinePoint = spline.EvaluatePosition(t);
                    float distance = Vector3.Distance(point, splinePoint);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestT = t;
                    }
                }
            }

            // Calculate arc length from start to closestT using numerical integration
            float splineDistance = 0f;
            Vector3 previousPoint = spline.EvaluatePosition(0f);
            int integrationSteps = 100; // Higher value increases accuracy

            for (int i = 1; i <= integrationSteps; i++)
            {
                float t = closestT * (i / (float)integrationSteps);
                Vector3 currentPoint = spline.EvaluatePosition(t);
                splineDistance += Vector3.Distance(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }

            return splineDistance;
        }

        // Same as GetSplineDistanceToPoint, but the point is converted into the container's local space
        // first. The spline stores its knots locally, so comparing a world space point against them
        // offsets every distance by the container's transform and picks an arbitrary part of the spline.
        private float GetSplineDistanceToWorldPoint(Vector3 worldPoint)
        {
            float3 localPoint = splineContainer.transform.InverseTransformPoint(worldPoint);
            SplineUtility.GetNearestPoint(spline, localPoint, out _, out float t);

            // Spline t is normalized by arc length, so scaling it by the length gives the distance along it.
            return t * splineLength;
        }


#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            if(aiVehicleController.AiMode != AIVehicleController.Ai_Mode.PathFollow) { return; }

            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, A);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(progressPoint, 0.2f);
                Gizmos.DrawLine(transform.position, progressPoint);
                Gizmos.color = new Color(0.8f, 0.1f, 0, 0.8f);
                Gizmos.DrawSphere(A, 1);
                Gizmos.DrawSphere(B, 1);
                Gizmos.DrawSphere(C, 1);
                Gizmos.DrawSphere(D, 1);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(A, B);
                Gizmos.DrawLine(B, C);
                Gizmos.DrawLine(C, D);
            }
            else
            {
                Vector3 tempA = transform.position + transform.forward * offset_A;
                Vector3 tempB = transform.position + transform.forward * (offset_A + offset_AB);
                Vector3 tempC = transform.position + transform.forward * (offset_A + offset_AB + offset_BC);

                Gizmos.color = new Color(0.8f, 0.1f, 0, 0.8f);
                Gizmos.DrawSphere(tempA, 1);
                Gizmos.DrawSphere(tempB, 1);
                Gizmos.DrawSphere(tempC, 1);
            }
        }

#endif
    }
}
