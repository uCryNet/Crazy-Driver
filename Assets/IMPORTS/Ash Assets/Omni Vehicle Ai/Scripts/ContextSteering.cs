using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OmniVehicleAi
{
    public class ContextSteering : MonoBehaviour
    {
        [Tooltip("Transform representing the vehicle.")]
        public Transform vehicleTransform;

        [Header("Steering Settings")]
        [Tooltip("Number of directions to check for context steering.")]
        [Range(8, 32)]
        public int directions = 16;

        [Tooltip("The maximum length of the ray for obstacle detection.")]
        public float rayLength = 10f;

        [Tooltip("Curve to adjust the ray length based on velocity alignment.")]
        public AnimationCurve raylengthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        // ==========================================
        [Space]
        [Tooltip("Factor to extend the ray length based on current speed. used for applying brakes if vehicle is faster and there is a obstacle in path")]
        public float lookAheadFactor = 1f;

        [Tooltip("Weight applied to the target direction.")]
        public float targetWeight = 1f;

        [Tooltip("Weight applied to obstacles.")]
        public float obstacleWeight = 2f;

        [Tooltip("Weight applied to other vehicles.")]
        public float otherVehiclesWeight = 0.5f;

        [Tooltip("Weight applied to inertia from current velocity.")]
        public float inertiaWeight = 0.5f;

        [Tooltip("The radius of the vehicle for obstacle detection.")]
        public float vehicleRadius = 2f;

        // ==========================================
        [Header("Obstacle Settings")]
        [Tooltip("Distance threshold for considering an obstacle as very close.")]
        public float obstacleCloseDistance = 4f;

        [HideInInspector]
        [Tooltip("Indicates if an obstacle is in the critical path (very close).")]
        public bool obstacleInCriticalPath_veryClose;

        [Tooltip("Layer mask for obstacles.")]
        public LayerMask obstacleLayers;

        [Tooltip("Layer mask for other vehicles.")]
        public LayerMask otherVehicleLayers;

        [HideInInspector]
        [Tooltip("Indicates if an obstacle is in the move direction.")]
        public bool obstacleInMoveDirection;

        [HideInInspector]
        [Tooltip("Indicates if any obstacle is in the path.")]
        public bool obstacleInPath;

        [HideInInspector]
        [Tooltip("Indicates if any other vehicle is in the path.")]
        public bool vehicleInPath;


        // ==========================================
        [Header("Gizmo Settings")]
        [Tooltip("Show debug gizmos in the editor.")]
        public bool showGizmos = true;

        [Tooltip("Show danger level visualizations.")]
        public bool showDangerLevels = true;

        [Tooltip("Show interest level visualizations.")]
        public bool showInterestLevels = true;

        [Tooltip("Show best direction visualization.")]
        public bool showBestDirection = true;

        // ==========================================

        [HideInInspector]
        public Vector3[] directionVectors, obstaclePositions, otherVehiclePositions;

        [HideInInspector]
        public float[] interest, danger_obstacle, danger_otherVehicle, scores;

        [HideInInspector]
        public Vector3 closestObstaclePosition, closestOtherVehiclePosition, bestDirection_obstacle, bestDirection_otherVehicle;

        // Internal ray length calculations for each direction.
        private float[] new_raylengths;



        private void OnValidate()
        {
            InitializeDirections();
        }

        private void OnEnable()
        {
            InitializeDirections();
        }

        [ContextMenu("Initialize Directions")]
        public void InitializeDirections()
        {
            if(vehicleTransform == null) return;

            float angleIncrement = 360f / directions;
            interest = new float[directions];
            danger_obstacle = new float[directions];
            danger_otherVehicle = new float[directions];
            scores = new float[directions];
            directionVectors = new Vector3[directions];
            obstaclePositions = new Vector3[directions];
            otherVehiclePositions = new Vector3[directions];
            new_raylengths = new float[directions];

            for (int i = 0; i < directions; i++)
            {
                float angle = i * angleIncrement;
                directionVectors[i] = Quaternion.Euler(0, angle, 0) * vehicleTransform.forward;
            }
        }

        public void GetBestDirection(Vector3 targetDirection, Vector3 currentVelocity, Vector3 moveDir)
        {
            Array.Clear(interest, 0, directions);
            Array.Clear(danger_obstacle, 0, directions);

            int obstacles = 0;
            int otherVehicles = 0;

            float speed = currentVelocity.magnitude;

            // Calculate interest & danger for each direction
            for (int i = 0; i < directions; i++)
            {
                Vector3 dir = directionVectors[i];

                // Interest based on target direction and inertia
                interest[i] = Vector3.Dot(dir, targetDirection.normalized) * targetWeight;
                if (speed > 0.1f)
                {
                    interest[i] += Vector3.Dot(dir, currentVelocity.normalized) * inertiaWeight;
                }

                // Use a multiplier based on how aligned this direction is with the vehicle's forward direction.
                float forwardFactor = Mathf.Clamp(Vector3.Dot(dir, vehicleTransform.forward), 0.5f, 1f);
                float sphereRadius = forwardFactor * vehicleRadius / 2f;
                float velocityFactor = Vector3.Dot(dir.normalized, currentVelocity / speed);
                float dangerObstacle = 0f;
                float dangerOther = 0f;


                float new_rayLength = rayLength * raylengthCurve.Evaluate(velocityFactor);
                new_raylengths[i] = new_rayLength;

                // Danger from obstacles
                if (Physics.SphereCast(vehicleTransform.position, sphereRadius, dir, out RaycastHit obstacleHit, new_rayLength, obstacleLayers))
                {
                    dangerObstacle = (1f - obstacleHit.distance / new_rayLength) * obstacleWeight;
                    obstaclePositions[i] = obstacleHit.point;
                }

                // Danger from other vehicles
                if (Physics.SphereCast(vehicleTransform.position, sphereRadius, dir, out RaycastHit vehicleHit, new_rayLength, otherVehicleLayers))
                {
                    dangerOther = (1f - vehicleHit.distance / new_rayLength) * otherVehiclesWeight;
                    otherVehiclePositions[i] = vehicleHit.point;
                }

                // Combine the two danger values. You could also choose to use Mathf.Max if you want the worst-case danger.
                danger_obstacle[i] = dangerObstacle;
                danger_otherVehicle[i] = dangerOther;

                obstacles += dangerObstacle > 0 ? 1 : 0;
                otherVehicles += dangerOther > 0 ? 1 : 0;

            }

            obstacleInPath = obstacles > 0;
            vehicleInPath = otherVehicles > 0;


            // Find the best direction
            float bestScoreObstacle = float.MinValue;
            int bestIndex_obstacle = 0;
            float bestScoreOther = float.MinValue;
            int bestIndex_other = 0;

            for (int i = 0; i < directions; i++)
            {
                float scoreObstacle = interest[i] - danger_obstacle[i];
                scores[i] = scoreObstacle;
                if (scoreObstacle > bestScoreObstacle)
                {
                    bestScoreObstacle = scoreObstacle;
                    bestIndex_obstacle = i;
                }

                float scoreOther = interest[i] - danger_otherVehicle[i];
                if (scoreOther > bestScoreOther)
                {
                    bestScoreOther = scoreOther;
                    bestIndex_other = i;
                }
            }
            bestDirection_obstacle = directionVectors[bestIndex_obstacle];
            bestDirection_otherVehicle = directionVectors[bestIndex_other];



            // Check for a direct obstacle in the current move direction (using the full vehicle width).
            bool hasDirectObstacle = Physics.SphereCast(
                vehicleTransform.position,
                vehicleRadius / 4,
                moveDir,
                out RaycastHit ob_hit,
                rayLength,
                obstacleLayers
            );
            obstacleInCriticalPath_veryClose = hasDirectObstacle && (ob_hit.distance < obstacleCloseDistance);


            //slowdown spherecast
            bool hasDirectObstacle_far = Physics.SphereCast(
                vehicleTransform.position,
                vehicleRadius/2,
                moveDir,
                out RaycastHit ob_hitfar,
                rayLength + currentVelocity.magnitude * lookAheadFactor,
                obstacleLayers
            );
            obstacleInMoveDirection = hasDirectObstacle_far;


        }

        public float closeObstacleDirection() // instead of this, just do overlap sphere check closest point on other object.
        {
            float closestDistance = float.MaxValue;
            Vector3 closestPoint = Vector3.zero;
            bool found = false;

            // Loop over each direction.
            for (int i = 0; i < directions; i++)
            {
                // Only consider directions where a danger was recorded.
                if (danger_obstacle[i] > 0f)
                {
                    // Check the obstaclePositions array.
                    if (obstaclePositions[i] != Vector3.zero)
                    {
                        float distance = Vector3.Distance(vehicleTransform.position, obstaclePositions[i]);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestPoint = obstaclePositions[i];
                            closestObstaclePosition = closestPoint;
                            found = true;
                        }
                    }

                    // Check the otherVehiclePositions array.
                    if (otherVehiclePositions[i] != Vector3.zero)
                    {
                        float distance2 = Vector3.Distance(vehicleTransform.position, otherVehiclePositions[i]);
                        if (distance2 < closestDistance)
                        {
                            closestDistance = distance2;
                            closestPoint = otherVehiclePositions[i];
                            closestObstaclePosition = closestPoint;
                            found = true;
                        }
                    }
                }
            }

            // If no obstacles or other vehicles were detected, default to "forward" (+1).
            if (!found)
            {
                return 1f;
            }

            // Determine the relative direction of the closest hit.
            Vector3 directionToObstacle = (closestPoint - vehicleTransform.position).normalized;
            float dot = Vector3.Dot(vehicleTransform.forward, directionToObstacle);

            // Return +1 if the closest point is in front, -1 if it's behind.
            return dot >= 0 ? 1f : -1f;
        }



        void OnDrawGizmosSelected()
        {
            if (!showGizmos || directionVectors == null) return;

#if UNITY_EDITOR
            // Uncomment if you want to visualize arcs for a forward avoid angle.
            //Handles.color = new Color(1, 0.5f, 0, 0.1f);
            //Handles.DrawSolidArc(vehicleTransform.position, Vector3.up, vehicleTransform.forward, forwardAvoidAngle, rayLength);
            //Handles.DrawSolidArc(vehicleTransform.position, Vector3.up, vehicleTransform.forward, -forwardAvoidAngle, rayLength);
#endif

            // Draw a sphere around the vehicle to visualize its width.
            Gizmos.color = new Color(1, 1, 0, 0.25f);
            Gizmos.DrawWireSphere(vehicleTransform.position, vehicleRadius / 2);
            // Draw the safety distance circle.
            Gizmos.color = new Color(1, 0, 0, 0.25f);
            Gizmos.DrawWireSphere(vehicleTransform.position, obstacleCloseDistance);

            // Draw danger and interest levels for each direction.
            if (!Application.isPlaying)
            {
                for (int i = 0; i < directionVectors.Length; i++)
                {

                    float forwardFactor = Vector3.Dot(directionVectors[i].normalized, vehicleTransform.forward);
                    float new_rayLength = rayLength * raylengthCurve.Evaluate(forwardFactor);
                    Vector3 endPos = vehicleTransform.position + directionVectors[i] * new_rayLength;

                    Gizmos.color = Color.green/1.5f;
                    Gizmos.DrawLine(vehicleTransform.position, endPos);
                    Gizmos.DrawSphere(endPos, vehicleRadius/2);
                }

                return;
            }

            for (int i = 0; i < directionVectors.Length; i++)
            {
                Vector3 endPos = vehicleTransform.position + directionVectors[i] * new_raylengths[i];

                float colorLerp = Mathf.Max(danger_obstacle[i] / obstacleWeight, danger_otherVehicle[i] / otherVehiclesWeight);

                Gizmos.color = Color.Lerp(Color.green/1.5f, Color.red/1.5f, colorLerp);

                // Draw danger (red) – the denominator includes both weights for visualization purposes.
                if (showDangerLevels && danger_obstacle != null && danger_otherVehicle != null)
                {
                    Gizmos.DrawLine(vehicleTransform.position, endPos);
                    Gizmos.DrawSphere(endPos, (vehicleRadius / 2));
                }

                // Draw interest (green).
                if (showInterestLevels && interest != null && interest.Length > i)
                {
#if UNITY_EDITOR
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    style.fontStyle = FontStyle.Bold; // Make text bold
                    style.normal.textColor = Color.white; // Change text color

                    Handles.Label(endPos, $"{scores[i]:F1}", style);
#endif
                }

            }

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(closestObstaclePosition, 0.5f);

        }
    }
}
