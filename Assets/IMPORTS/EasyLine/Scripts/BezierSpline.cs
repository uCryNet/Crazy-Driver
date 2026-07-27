using UnityEngine;

namespace EasyLine
{
    public class BezierSpline : MonoBehaviour
    {
        [Tooltip("The control points of the spline (Anchors and Tangent handles). Each segment is 4 points.")]
        public Vector3[] points;

        [Tooltip("If true, the spline will connect the last anchor back to the first anchor.")]
        public bool loop;

        [HideInInspector]
        public float[] anchorRolls = new float[0];
        [HideInInspector]
        public Vector3[] anchorScales = new Vector3[0];

        public void Reset()
        {
            points = new Vector3[] {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 2f),
                new Vector3(0f, 0f, 4f),
                new Vector3(0f, 0f, 6f)
            };
            anchorRolls = new float[] { 0f, 0f };
            anchorScales = new Vector3[] { Vector3.one, Vector3.one };
        }

        public int CurveCount
        {
            get
            {
                return (points.Length - 1) / 3;
            }
        }

        public Vector3 GetPoint(float t)
        {
            if (loop) t = Mathf.Repeat(t, 1f);
            else t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = points.Length - 4;
            }
            else
            {
                float curveT = t * CurveCount;
                i = (int)curveT;
                t = curveT - i;
                i *= 3;
            }

            float oneMinusT = 1f - t;
            return
                oneMinusT * oneMinusT * oneMinusT * points[i] +
                3f * oneMinusT * oneMinusT * t * points[i + 1] +
                3f * oneMinusT * t * t * points[i + 2] +
                t * t * t * points[i + 3];
        }

        public float GetRoll(float t, float smoothWindow = 0f)
        {
            if (smoothWindow > 0.001f)
            {
                int samples = 4;
                float sum = 0f;
                for (int s = -samples; s <= samples; s++)
                {
                    float sampleT = t + (s / (float)samples) * (smoothWindow * 0.5f);
                    if (loop) sampleT = Mathf.Repeat(sampleT, 1f);
                    else sampleT = Mathf.Clamp01(sampleT);
                    sum += GetRollInternal(sampleT);
                }
                return sum / (samples * 2 + 1);
            }
            return GetRollInternal(t);
        }

        private float GetRollInternal(float t)
        {
            if (anchorRolls == null || anchorRolls.Length == 0) return 0f;
            if (anchorRolls.Length == 1) return anchorRolls[0];

            if (loop) t = Mathf.Repeat(t, 1f);
            else t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = CurveCount - 1;
            }
            else
            {
                float curveT = t * CurveCount;
                i = (int)curveT;
                t = curveT - i; // local T
            }

            // Smoothstep the interpolation for a more organic banking effect
            float smoothT = t * t * (3f - 2f * t);

            int anchorStart = i;
            int anchorEnd = i + 1;

            float startRoll = anchorRolls[Mathf.Clamp(anchorStart, 0, anchorRolls.Length - 1)];
            float endRoll = 0f;

            if (anchorEnd < anchorRolls.Length)
            {
                endRoll = anchorRolls[anchorEnd];
            }
            else if (loop)
            {
                endRoll = anchorRolls[0];
            }
            else
            {
                endRoll = startRoll;
            }

            return Mathf.Lerp(startRoll, endRoll, smoothT);
        }
        
        public Vector3 GetScale(float t, float smoothWindow = 0f)
        {
            if (smoothWindow > 0.001f)
            {
                int samples = 4;
                Vector3 sum = Vector3.zero;
                for (int s = -samples; s <= samples; s++)
                {
                    float sampleT = t + (s / (float)samples) * (smoothWindow * 0.5f);
                    if (loop) sampleT = Mathf.Repeat(sampleT, 1f);
                    else sampleT = Mathf.Clamp01(sampleT);
                    sum += GetScaleInternal(sampleT);
                }
                return sum / (samples * 2 + 1);
            }
            return GetScaleInternal(t);
        }

        private Vector3 GetScaleInternal(float t)
        {
            if (anchorScales == null || anchorScales.Length == 0) return Vector3.one;
            if (anchorScales.Length == 1) return anchorScales[0];

            if (loop) t = Mathf.Repeat(t, 1f);
            else t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = CurveCount - 1;
            }
            else
            {
                float curveT = t * CurveCount;
                i = (int)curveT;
                t = curveT - i;
            }

            float smoothT = t * t * (3f - 2f * t);

            int anchorStart = i;
            int anchorEnd = i + 1;

            Vector3 startScale = anchorScales[Mathf.Clamp(anchorStart, 0, anchorScales.Length - 1)];
            Vector3 endScale = Vector3.one;

            if (anchorEnd < anchorScales.Length)
            {
                endScale = anchorScales[anchorEnd];
            }
            else if (loop)
            {
                endScale = anchorScales[0];
            }
            else
            {
                endScale = startScale;
            }

            Vector3 res = Vector3.Lerp(startScale, endScale, smoothT);
            if (res.sqrMagnitude < 0.0001f) return Vector3.one; 
            return res;
        }

        public Vector3 GetVelocity(float t)
        {
            if (points == null || points.Length < 4) return Vector3.forward;

            if (loop) t = Mathf.Repeat(t, 1f);
            else t = Mathf.Clamp01(t);

            int i;
            if (t >= 1f)
            {
                t = 1f;
                i = points.Length - 4;
            }
            else
            {
                float curveT = t * CurveCount;
                i = (int)curveT;
                t = curveT - i;
                i *= 3;
            }
            
            Vector3 p0 = points[i], p1 = points[i+1], p2 = points[i+2], p3 = points[i+3];
            
            float oneMinusT = 1f - t;
            Vector3 v = 3f * oneMinusT * oneMinusT * (p1 - p0) +
                6f * oneMinusT * t * (p2 - p1) +
                3f * t * t * (p3 - p2);

            // EXTRA ROBUSTNESS: If velocity is zero (collapsed tangents), try a small offset to resolve direction
            if (v.sqrMagnitude < 0.0001f)
            {
                float offsetT = (t < 0.5f) ? t + 0.01f : t - 0.01f;
                float oneMinusOffsetT = 1f - offsetT;
                v = 3f * oneMinusOffsetT * oneMinusOffsetT * (p1 - p0) +
                    6f * oneMinusOffsetT * offsetT * (p2 - p1) +
                    3f * offsetT * offsetT * (p3 - p2);
                
                // Still zero? Last resort: pointing towards the segment end
                if (v.sqrMagnitude < 0.0001f) v = (p3 - p0).normalized;
                // Absolute last resort
                if (v.sqrMagnitude < 0.0001f) v = Vector3.forward;
            }

            return v;
        }

        public Vector3 GetDirection(float t)
        {
            return GetVelocity(t).normalized;
        }

        public class CurveDistanceMapper
        {
            private float[] cumulativeWarped;
            private float totalPhysicalLength;
            private float totalWarpedLength;
            private int resolution;
            private BezierSpline spline;

            public CurveDistanceMapper(BezierSpline spline, float density = 1f, int resolution = 200)
            {
                this.spline = spline;
                this.resolution = resolution;
                cumulativeWarped = new float[resolution + 1];
                
                // 1. Sample curve points and calculate raw distances
                Vector3[] sampledPoints = new Vector3[resolution + 1];
                float[] stepDistances = new float[resolution + 1];
                
                for (int i = 0; i <= resolution; i++) {
                    sampledPoints[i] = spline.GetPoint((float)i / resolution);
                    if (i > 0) stepDistances[i] = Vector3.Distance(sampledPoints[i - 1], sampledPoints[i]);
                }
                
                // 2. Calculate raw discrete angles between segments (0 to 180 degrees)
                float[] rawAngles = new float[resolution + 1];
                for (int i = 1; i < resolution; i++) {
                    Vector3 dir1 = (sampledPoints[i] - sampledPoints[i - 1]).normalized;
                    Vector3 dir2 = (sampledPoints[i + 1] - sampledPoints[i]).normalized;
                    rawAngles[i] = Vector3.Angle(dir1, dir2); 
                }
                rawAngles[0] = rawAngles[1];
                rawAngles[resolution] = rawAngles[resolution - 1];

                // 3. Apply a strong Gaussian-style multi-pass smoothing window
                float[] smoothedAngles = new float[resolution + 1];
                // Wide smooth window to prevent any abrupt single-vert stretches
                int smoothWindow = Mathf.Max(5, resolution / 10); 
                
                for (int pass = 0; pass < 2; pass++) // 2 passes for heavy blur
                {
                    float[] source = pass == 0 ? rawAngles : smoothedAngles;
                    float[] target = new float[resolution + 1];

                    for (int i = 0; i <= resolution; i++) {
                        float sum = 0f;
                        float weightSum = 0f;
                        
                        for (int j = -smoothWindow; j <= smoothWindow; j++) {
                            int idx = Mathf.Clamp(i + j, 0, resolution);
                            // Center weighs more than edges
                            float weight = 1f - (Mathf.Abs(j) / (float)(smoothWindow + 1));
                            sum += source[idx] * weight;
                            weightSum += weight;
                        }
                        target[i] = sum / weightSum;
                    }
                    
                    if (pass == 0) smoothedAngles = target;
                    else smoothedAngles = target; // apply second pass
                }

                // 4. Calculate warped length mapping
                totalPhysicalLength = 0f;
                totalWarpedLength = 0f;
                float[] bendFactors = new float[resolution + 1];
                
                for (int i = 1; i <= resolution; i++) {
                    float physicalStep = stepDistances[i];
                    totalPhysicalLength += physicalStep;
                    
                    float angle = smoothedAngles[i];
                    // Normalize angle: 0 on straight, ~1 on moderate bend, higher on sharp turns
                    float bendIntensity = Mathf.Clamp(angle / 10f, 0f, 5f); 
                    
                    float stepBendFactor = 1f;
                    if (!Mathf.Approximately(density, 0f)) {
                        if (density > 0f) {
                            // Positive: bends cost MORE warped distance -> mesh shrinks on curves
                            stepBendFactor = 1f + (bendIntensity * density * 0.3f);
                        } else {
                            // Negative: bends cost LESS warped distance -> mesh stretches on curves
                            stepBendFactor = 1f / (1f + (bendIntensity * Mathf.Abs(density) * 0.3f));
                            stepBendFactor = Mathf.Max(stepBendFactor, 0.1f);
                        }
                    }
                    
                    bendFactors[i] = stepBendFactor;
                }

                // 5. Final pass to smooth the generated Bend Factors themselves
                for (int i = 1; i <= resolution; i++) {
                    float sum = 0f;
                    int count = 0;
                    for (int j = -3; j <= 3; j++) {
                        int idx = Mathf.Clamp(i + j, 1, resolution);
                        sum += bendFactors[idx];
                        count++;
                    }
                    
                    float finalSmoothedFactor = sum / count;
                    float warpedStep = stepDistances[i] * finalSmoothedFactor;
                    
                    totalWarpedLength += warpedStep;
                    cumulativeWarped[i] = totalWarpedLength;
                }
            }
            
            public float GetTotalPhysicalLength() 
            { 
                return totalPhysicalLength; 
            }

            public float GetTAtDistance(float distance)
            {
                if (spline == null || totalPhysicalLength < 0.001f) return 0f;
                
                // Un-normalize the distance into a raw ratio
                float rawRatio = distance / totalPhysicalLength;
                float wrappedRatio = spline.loop ? Mathf.Repeat(rawRatio, 1f) : Mathf.Clamp01(rawRatio);
                
                // Map the wrapped ratio to the warped cumulative curve
                float targetWarpedDistance = totalWarpedLength * wrappedRatio;
                
                float wrappedT = 0f;
                if (targetWarpedDistance <= 0f) wrappedT = 0f;
                else if (targetWarpedDistance >= totalWarpedLength) wrappedT = 1f;
                else
                {
                    for (int i = 1; i <= resolution; i++) {
                        if (cumulativeWarped[i] >= targetWarpedDistance) {
                            float prevWarped = cumulativeWarped[i - 1];
                            float currentWarped = cumulativeWarped[i];
                            
                            if (currentWarped - prevWarped < 0.00001f) 
                            {
                                wrappedT = (float)i / resolution;
                                break;
                            }
                                
                            float pct = (targetWarpedDistance - prevWarped) / (currentWarped - prevWarped);
                            wrappedT = Mathf.Lerp((float)(i - 1) / resolution, (float)i / resolution, pct);
                            break;
                        }
                    }
                }

                // Return the "unrolled" T that matches the input distance offset
                // t = floor(rawRatio) + wrappedT
                return Mathf.Floor(rawRatio) + wrappedT;
            }
        }

        private CurveDistanceMapper cachedMapper;
        private float lastMapperDensity = -999f;
        private int lastMapperRes = -1;

        public CurveDistanceMapper GetMapper(float density = 0f, int resolution = 200)
        {
            if (cachedMapper != null && Mathf.Approximately(density, lastMapperDensity) && resolution == lastMapperRes)
            {
                return cachedMapper;
            }
            cachedMapper = new CurveDistanceMapper(this, density, resolution);
            lastMapperDensity = density;
            lastMapperRes = resolution;
            return cachedMapper;
        }

        public void MarkDirty()
        {
            cachedMapper = null;
        }

        // --- SHARED HELPER METHODS FOR DEFORMATION ---

        public enum ForwardAxis { Z, X, Y, NegativeZ, NegativeX, NegativeY }

        public static Vector3 MapToStandardLocal(Vector3 pt, ForwardAxis forwardAxis)
        {
            switch (forwardAxis)
            {
                case ForwardAxis.X: return new Vector3(-pt.z, pt.y, pt.x);
                case ForwardAxis.Y: return new Vector3(pt.x, -pt.z, pt.y);
                case ForwardAxis.NegativeZ: return new Vector3(-pt.x, pt.y, -pt.z);
                case ForwardAxis.NegativeX: return new Vector3(pt.z, pt.y, -pt.x);
                case ForwardAxis.NegativeY: return new Vector3(pt.x, pt.z, -pt.y);
                case ForwardAxis.Z:
                default: return pt;
            }
        }

        public static Vector3 GetDirectionForT(BezierSpline spline, float t, CurveDistanceMapper mapper, float smoothBendStrength)
        {
            if (smoothBendStrength > 0.001f)
            {
                float halfWindow = smoothBendStrength * 0.1f;
                int samples = 8;
                Vector3 avgDir = Vector3.zero;
                for (int s = -samples; s <= samples; s++)
                {
                    float sampleT = t + (s / (float)samples) * halfWindow;
                    if (spline != null && spline.loop) sampleT = Mathf.Repeat(sampleT, 1f);
                    else sampleT = Mathf.Clamp01(sampleT);
                    
                    avgDir += spline.GetDirection(sampleT);
                }
                return avgDir.normalized;
            }
            
            float singleT = t;
            if (spline != null && spline.loop) singleT = Mathf.Repeat(singleT, 1f);
            else singleT = Mathf.Clamp01(singleT);
            
            return spline.GetDirection(singleT);
        }

        public static Quaternion GetRotationFromForward(BezierSpline spline, Vector3 forward, float t, bool lockX, bool lockY, bool lockZ, float smoothWindow = 0f, bool forceUpright = false, Vector3? upReference = null)
        {
            Vector3 upRef = upReference ?? Vector3.up;

            // Apply Pitch (X) and Yaw (Y) locks to the forward vector
            // Lock X (Pitch): Project onto horizontal plane relative to upRef
            if (lockX || (forceUpright && !lockY))
            {
                forward = Vector3.ProjectOnPlane(forward, upRef).normalized;
            }
            
            // Lock Y (Yaw): Force the forward direction to stay aligned with a fixed world axis (Z/Forward)
            // if we are upright, or a relative side-direction.
            if (lockY)
            {
                // We lock it to the Local Forward of the reference (usually world Z)
                Vector3 worldBankRight = Vector3.Cross(upRef, Vector3.forward);
                if (worldBankRight.sqrMagnitude < 0.001f) worldBankRight = Vector3.right;
                forward = Vector3.ProjectOnPlane(forward, worldBankRight).normalized;
            }

            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 up = upRef;
            if (forceUpright) 
            {
                // If forced upright AND locked in Y, we already have our orientation.
                // Otherwise, LookRotation(forward, up) ensures it stands straight.
                return Quaternion.LookRotation(forward, up);
            }

            // --- IMPROVED PARALLEL FALLBACK ---
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.99f)
            {
                up = (Mathf.Abs(Vector3.Dot(forward, Vector3.forward)) < 0.99f) ? Vector3.forward : Vector3.right;
            }

            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right; 
            
            up = Vector3.Cross(forward, right).normalized;
            
            // Apply Roll (Banking) if not locked
            if (spline != null && !lockZ)
            {
                float roll = spline.GetRoll(t, smoothWindow);
                if (Mathf.Abs(roll) > 0.001f)
                {
                    up = Quaternion.AngleAxis(roll, forward) * up;
                }
            }
            
            return Quaternion.LookRotation(forward, up);
        }
    }
}

