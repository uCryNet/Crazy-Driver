using UnityEngine;
using System.Collections.Generic;

namespace EasyLine
{
    [ExecuteAlways]
    public class SplinePrefabInstantiator : MonoBehaviour
    {
        public enum SpacingMode { FixedDistance, TotalCount }

        [Header("Curve Setup")]
        [Tooltip("The Bezier Spline component that defines the placement path.")]
        public BezierSpline spline;

        [Header("Prefab Settings")]
        [Tooltip("The prefab to be instantiated along the curve.")]
        public GameObject prefab;
        
        [Header("Spawn Settings")]
        public SpacingMode spacingMode = SpacingMode.TotalCount;
        
        [Tooltip("Total number of instances to spread along the curve.")]
        [Min(1)] public int instanceCount = 10;

        [Tooltip("Distance between instances (for FixedDistance mode).")]
        [Min(0.1f)] public float spacing = 2f;
        
        [Tooltip("Global shift along the curve for all instances.")]
        public float globalOffset = 0f;

        [Tooltip("Offset applied to each instance relative to the spline point.")]
        public Vector3 positionOffset;
        [Tooltip("Rotation offset applied to each instance.")]
        public Vector3 rotationOffset;
        [Tooltip("Base scale for all instances.")]
        public Vector3 baseScale = Vector3.one;

        [Header("Alignment")]
        [Tooltip("Which axis of the prefab represents the 'forward' direction along the curve.")]
        public BezierSpline.ForwardAxis forwardAxis = BezierSpline.ForwardAxis.Z;

        [Tooltip("If true, instances will rotate to match the spline's direction.")]
        public bool alignToSpline = true;
        [Tooltip("Forces the instances to always stay upright (ignore spline's roll).")]
        public bool stayUpright = false;

        [Tooltip("If true, individual meshes inside the prefab will bend along the curve. Great for long objects like train cars.")]
        public bool deformMeshes = false;

        [Tooltip("If true, the meshes will stretch/shrink proportionally to perfectly fit the distance between instances. Based on the longest mesh in the prefab.")]
        public bool stretchToFit = false;

        [Tooltip("If true, the spline's per-anchor Scale (widening/narrowing) will be applied to the prefabs. If false, they keep their original shape.")]
        public bool applySplineScale = true;

        [Header("Randomization")]
        [Tooltip("Random offset added to each instance position.")]
        public Vector3 positionJitter;
        [Tooltip("Random rotation range added to each instance.")]
        public Vector3 randomRotation;
        [Tooltip("Random scale variation multiplier. 0 = no change, 0.5 = ±50% size.")]
        [Range(0f, 1f)] public float scaleRandomness = 0.2f;

        [Header("Editor Options")]
        [Tooltip("When enabled, updates whenever parameters change.")]
        public bool autoUpdate = true;

        [HideInInspector]
        [SerializeField]
        private List<GameObject> instances = new List<GameObject>();

        private void OnValidate()
        {
            if (autoUpdate)
            {
    #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) UpdateInstances();
                };
    #endif
            }
        }

        private void Update()
        {
            if (!Application.isPlaying && autoUpdate && spline != null)
            {
                // In editor we rely on OnValidate and EditorUpdate, 
                // but Update helps if things get out of sync.
            }
        }

        [ContextMenu("Update Instances")]
        public void UpdateInstances()
        {
            if (spline == null || prefab == null) 
            {
                ClearInstances();
                return;
            }

            ClearInstances();

            BezierSpline.CurveDistanceMapper mapper = new BezierSpline.CurveDistanceMapper(spline, 0f, 100);
            float totalLength = mapper.GetTotalPhysicalLength();

            int count = 0;
            if (spacingMode == SpacingMode.TotalCount)
            {
                count = instanceCount;
            }
            else
            {
                if (spacing > 0.001f)
                {
                    count = Mathf.FloorToInt(totalLength / spacing);
                    if (!spline.loop && count > 0) count += 1; // Include end point if not looping and there's at least one instance
                }
            }

            if (count <= 0) return;

            Random.InitState(0); // Stable randomization

            float maxMeshLen = 1f;
            if (deformMeshes && stretchToFit)
            {
                maxMeshLen = GetMaxMeshLength(prefab, forwardAxis);
            }

            float segmentLength = (spacingMode == SpacingMode.FixedDistance) ? spacing : (totalLength / Mathf.Max(1, (spline.loop ? count : count - 1)));

            for (int i = 0; i < count; i++)
            {
                float distance = 0f;
                if (spacingMode == SpacingMode.TotalCount)
                {
                    float t = count > 1 ? (float)i / (count - (spline.loop ? 0 : 1)) : 0f;
                    distance = t * totalLength + globalOffset;
                }
                else
                {
                    distance = i * spacing + globalOffset;
                }

                if (spline.loop)
                {
                    distance = Mathf.Repeat(distance, totalLength);
                }
                else if (distance > totalLength + 0.001f || distance < -0.001f)
                {
                    continue;
                }

                float curveT = mapper.GetTAtDistance(distance);
                Vector3 pos = spline.transform.TransformPoint(spline.GetPoint(curveT));
                Vector3 dir = spline.transform.TransformDirection(spline.GetDirection(curveT));
                
                Quaternion rot = Quaternion.identity;
                if (alignToSpline)
                {
                    if (!stayUpright)
                    {
                        Vector3 forward = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, curveT, mapper, 0f));
                        rot = BezierSpline.GetRotationFromForward(spline, forward, curveT, false, false, false);
                    }
                    else
                    {
                        rot = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z).normalized, Vector3.up);
                    }
                }
                
                // Apply forward axis correction so the chosen axis faces the spline
                rot *= GetForwardCorrection();
                
                rot *= Quaternion.Euler(rotationOffset);

                // Randomization
                Vector3 jitter = new Vector3(
                    Random.Range(-positionJitter.x, positionJitter.x),
                    Random.Range(-positionJitter.y, positionJitter.y),
                    Random.Range(-positionJitter.z, positionJitter.z)
                );

                Vector3 randRot = new Vector3(
                    Random.Range(-randomRotation.x, randomRotation.x),
                    Random.Range(-randomRotation.y, randomRotation.y),
                    Random.Range(-randomRotation.z, randomRotation.z)
                );
                rot *= Quaternion.Euler(randRot);

                float randScale = 1f + Random.Range(-scaleRandomness, scaleRandomness);
                
                // Start with the prefab's own local scale as defined in the asset/project
                Vector3 assetScale = prefab.transform.localScale;

                // Calculate total target scale
                Vector3 finalScale = Vector3.Scale(assetScale, baseScale) * randScale;
                if (applySplineScale)
                {
                    finalScale = Vector3.Scale(finalScale, spline.GetScale(curveT));
                }

                GameObject inst = InstantiatePrefab(pos + rot * (positionOffset + jitter), rot, finalScale);
                if (inst != null)
                {
    #if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(inst, "Spawn Instance");
    #endif
                    if (deformMeshes)
                    {
                        float stretchRatio = stretchToFit ? (segmentLength / maxMeshLen) : 1f;
                        ApplyDeformationToInstance(inst, distance, mapper, stretchRatio, applySplineScale);
                    }
                    instances.Add(inst);
                }
            }
        }

        private void ApplyDeformationToInstance(GameObject inst, float baseDistance, BezierSpline.CurveDistanceMapper mapper, float stretchRatio, bool usePointScale)
        {
            MeshFilter[] mfs = inst.GetComponentsInChildren<MeshFilter>();
            float curveLength = mapper.GetTotalPhysicalLength();

            // The instance's world transform (already at spline position with rotation).
            // We need the INVERSE to convert world-space deformed points back into the instance's local frame.
            Matrix4x4 instWorldToLocal = inst.transform.worldToLocalMatrix;

            foreach (var mf in mfs)
            {
                // Resolve through the ProBuilder bridge: plain sharedMesh for normal objects, or a
                // compiled readable mesh for ProBuilder objects whose runtime mesh isn't ready yet.
                Mesh originalMesh = ProBuilderSupport.ResolveRenderMesh(mf);
                if (originalMesh == null) continue;
                bool isPb = ProBuilderSupport.IsProBuilderBacked(mf);

                // The import-readability fix only applies to imported model assets. ProBuilder meshes
                // are procedural (and the resolver already returned a readable copy), so skip it.
                if (!originalMesh.isReadable && !isPb)
                {
    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        string path = UnityEditor.AssetDatabase.GetAssetPath(originalMesh);
                        if (!string.IsNullOrEmpty(path))
                        {
                            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
                            if (importer != null && !importer.isReadable)
                            {
                                importer.isReadable = true;
                                importer.SaveAndReimport();
                            }
                        }
                    }
    #endif
                    if (!originalMesh.isReadable) continue;
                }

                Mesh deformedMesh = new Mesh();
                deformedMesh.name = "Bent " + originalMesh.name;

                Vector3[] verts = originalMesh.vertices;
                Vector3[] normals = originalMesh.normals;
                Vector3[] deformedVerts = new Vector3[verts.Length];
                Vector3[] deformedNormals = new Vector3[verts.Length];

                // Transform from mesh-filter local space → world space
                Matrix4x4 mfLocalToWorld = mf.transform.localToWorldMatrix;
                // Transform from world → mesh-filter local (for output)
                Matrix4x4 mfWorldToLocal = mf.transform.worldToLocalMatrix;

                for (int v = 0; v < verts.Length; v++)
                {
                    // 1. Get vertex in WORLD space (via the mesh filter's transform)
                    Vector3 worldPt = mfLocalToWorld.MultiplyPoint3x4(verts[v]);
                    Vector3 worldNorm = mfLocalToWorld.MultiplyVector(normals.Length > v ? normals[v] : Vector3.up);

                    // 2. Convert to instance's local space to get the "offset from anchor"
                    Vector3 instancePt = instWorldToLocal.MultiplyPoint3x4(worldPt);

                    // 3. Map to standard local (Z = forward along spline)
                    Vector3 standardPt = BezierSpline.MapToStandardLocal(instancePt, forwardAxis);

                    // 4. Find position on spline for this vertex
                    float finalDist = baseDistance + (standardPt.z * stretchRatio);
                    if (spline.loop) finalDist = Mathf.Repeat(finalDist, curveLength);
                    else finalDist = Mathf.Clamp(finalDist, 0f, curveLength);

                    float t = mapper.GetTAtDistance(finalDist);
                    Vector3 curvePos = spline.transform.TransformPoint(spline.GetPoint(t));
                    Vector3 forward = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, t, mapper, 0f));
                    Quaternion rot = BezierSpline.GetRotationFromForward(spline, forward, t, false, false, false);
                    
                    Vector3 localSideOffset = new Vector3(standardPt.x, standardPt.y, 0f);
                    Vector3 pointScale = usePointScale ? spline.GetScale(t) : Vector3.one;

                    // 5. Compute world-space deformed position
                    Vector3 deformedWorld = curvePos + rot * Vector3.Scale(localSideOffset, pointScale);
                    Vector3 deformedWorldNorm = rot * BezierSpline.MapToStandardLocal(
                        instWorldToLocal.MultiplyVector(worldNorm), forwardAxis);

                    // 6. Transform back to mesh filter's local space
                    deformedVerts[v] = mfWorldToLocal.MultiplyPoint3x4(deformedWorld);
                    deformedNormals[v] = mfWorldToLocal.MultiplyVector(deformedWorldNorm).normalized;
                }

                deformedMesh.vertices = deformedVerts;
                deformedMesh.normals = deformedNormals;
                deformedMesh.uv = originalMesh.uv;
                deformedMesh.triangles = originalMesh.triangles;
                deformedMesh.subMeshCount = originalMesh.subMeshCount;
                for(int s=0; s<originalMesh.subMeshCount; s++) deformedMesh.SetTriangles(originalMesh.GetTriangles(s), s);
                
                deformedMesh.RecalculateBounds();
                mf.sharedMesh = deformedMesh;

                // For ProBuilder instances, remove the ProBuilderMesh component so it cannot rebuild
                // and overwrite the bent mesh we just baked. These are throwaway generated instances,
                // so dropping ProBuilder authoring data on them is intended.
                if (isPb) ProBuilderSupport.StripProBuilder(mf);
            }
        }

        private float GetMaxMeshLength(GameObject prefab, BezierSpline.ForwardAxis axis)
        {
            MeshFilter[] mfs = prefab.GetComponentsInChildren<MeshFilter>();
            float maxLen = 0.001f;
            foreach (var mf in mfs)
            {
                // Resolve through the ProBuilder bridge so prefab assets (sharedMesh == null) work too.
                Mesh srcMesh = ProBuilderSupport.ResolveRenderMesh(mf);
                if (srcMesh == null) continue;
                Bounds b = srcMesh.bounds;
                float len = 0;
                switch (axis)
                {
                    case BezierSpline.ForwardAxis.X:
                    case BezierSpline.ForwardAxis.NegativeX: len = b.size.x; break;
                    case BezierSpline.ForwardAxis.Y:
                    case BezierSpline.ForwardAxis.NegativeY: len = b.size.y; break;
                    default: len = b.size.z; break;
                }
                if (len > maxLen) maxLen = len;
            }
            return maxLen;
        }

        private Quaternion GetForwardCorrection()
        {
            switch (forwardAxis)
            {
                case BezierSpline.ForwardAxis.X: return Quaternion.Euler(0, 90, 0);
                case BezierSpline.ForwardAxis.NegativeX: return Quaternion.Euler(0, -90, 0);
                case BezierSpline.ForwardAxis.Y: return Quaternion.Euler(-90, 0, 0);
                case BezierSpline.ForwardAxis.NegativeY: return Quaternion.Euler(90, 0, 0);
                case BezierSpline.ForwardAxis.NegativeZ: return Quaternion.Euler(0, 180, 0);
                case BezierSpline.ForwardAxis.Z:
                default: return Quaternion.identity;
            }
        }

        private Transform GetContainer()
        {
            Transform container = transform.Find("Generated Instances");
            if (container == null)
            {
                GameObject obj = new GameObject("Generated Instances");
                obj.transform.SetParent(transform);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                container = obj.transform;
            }
            return container;
        }

        private GameObject InstantiatePrefab(Vector3 worldPos, Quaternion worldRot, Vector3 scale)
        {
            GameObject inst;
            Transform parent = GetContainer();
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.SetPositionAndRotation(worldPos, worldRot);
                inst.transform.localScale = scale;
                return inst;
            }
    #endif
            inst = Instantiate(prefab, worldPos, worldRot, parent);
            inst.transform.localScale = scale;
            return inst;
        }

        public void ClearInstances()
        {
            Transform container = transform.Find("Generated Instances");
            if (container == null)
            {
                instances.Clear();
                return;
            }

            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                toDestroy.Add(container.GetChild(i).gameObject);
            }

            foreach (var obj in toDestroy)
            {
                if (obj == null) continue;
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(obj);
                }
                else
                {
                    Destroy(obj);
                }
    #else
                Destroy(obj);
    #endif
            }
            instances.Clear();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                // We don't necessarily want to clear on disable, 
                // but we might want to if the component is being removed.
            }
        }

        private void OnDestroy()
        {
            if (!Application.isPlaying)
            {
                ClearInstances();
            }
        }
    }
}
