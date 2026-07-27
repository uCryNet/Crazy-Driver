using UnityEngine;

namespace EasyLine
{
    [ExecuteAlways]
    public class SplineFollower : MonoBehaviour
    {
        [Header("Setup")]
        [Tooltip("The spline to follow.")]
        public BezierSpline spline;
        
        [Tooltip("Movement speed in units per second.")]
        public float speed = 5f;
        
        public enum FollowMode { Once, Loop }
        [Header("Behavior")]
        [Tooltip("What happens when reaching the end of the spline.")]
        public FollowMode followMode = FollowMode.Loop;
        
        public enum RotationMode { None, LookAhead, SplineRotation }
        [Tooltip("How the object rotates as it moves.")]
        public RotationMode rotationMode = RotationMode.LookAhead;
        
        [Tooltip("If true, the object will move at a perfectly constant speed regardless of curve complexity.")]
        public bool useConstantSpeed = true;

        [Header("State")]
        [Tooltip("Current distance along the spline.")]
        public float currentDistance = 0f;
        
        private BezierSpline.CurveDistanceMapper mapper;

        private void Start()
        {
            RefreshMapper();
        }

        private void Update()
        {
            if (spline == null) return;
            
            // In editor or if spline changed, we might need to refresh mapper
            if (mapper == null) RefreshMapper();

            float totalLength = (mapper != null) ? mapper.GetTotalPhysicalLength() : 10f;
            if (totalLength <= 0f) return;

            // --- 1. MOVE ---
            if (Application.isPlaying)
            {
                currentDistance += speed * Time.deltaTime;
            }

            // --- 2. HANDLE BOUNDS & LOOPING ---
            if (followMode == FollowMode.Loop)
            {
                // Smooth modulo for looping
                currentDistance %= totalLength;
                if (currentDistance < 0f) currentDistance += totalLength;
            }
            else // Once
            {
                currentDistance = Mathf.Clamp(currentDistance, 0f, totalLength);
            }

            // --- 3. CALCULATE T ---
            float t = 0f;
            if (useConstantSpeed && mapper != null)
            {
                t = mapper.GetTAtDistance(currentDistance);
            }
            else
            {
                t = currentDistance / totalLength;
            }
            t = Mathf.Clamp01(t);

            // --- 4. APPLY TRANSFORM ---
            transform.position = spline.transform.TransformPoint(spline.GetPoint(t));

            if (distanceMagnitude(speed) > 0.001f || !Application.isPlaying) 
            {
                ApplyRotation(t);
            }
        }

        private float distanceMagnitude(float val) => Mathf.Abs(val);

        private void ApplyRotation(float t)
        {
            if (rotationMode == RotationMode.LookAhead)
            {
                Vector3 dir = spline.transform.TransformDirection(spline.GetDirection(t));
                // Flip if speed is negative to face movement direction
                if (speed < 0f) dir = -dir;

                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
            else if (rotationMode == RotationMode.SplineRotation)
            {
                Vector3 forward = spline.transform.TransformDirection(spline.GetDirection(t));
                if (speed < 0f) forward = -forward;

                Vector3 up = Vector3.up; 
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f) up = Vector3.forward;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;

                transform.rotation = Quaternion.LookRotation(forward, up);
            }
        }

        [ContextMenu("Refresh Mapper")]
        public void RefreshMapper()
        {
            if (spline != null)
            {
                mapper = new BezierSpline.CurveDistanceMapper(spline, 0f, 200);
            }
        }
    }
}
