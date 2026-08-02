using UnityEngine;
using System.Collections.Generic;

namespace EasyLine
{
[System.Serializable]
public class SplineMeshElement
{
    public string elementName = "";
    public SplineMeshDeformer.SourceMode mode = SplineMeshDeformer.SourceMode.Mesh;
    [Tooltip("The mesh asset to use for this specific range (if mode is Mesh).")]
    public Mesh mesh;
    [Tooltip("The prefab asset to use for this specific range (if mode is Prefab).")]
    public GameObject prefab;
    [Tooltip("The materials assigned to this mesh (overrides prefab materials if set).")]
    public Material[] materials;
    [Tooltip("The segment index where this element starts appearing (inclusive).")]
    public int startIndex = 0;
    [Tooltip("The segment index where this element stops appearing (inclusive).")]
    public int endIndex = 0;
    [Tooltip("Type of placement and physics. Road = stretches/bends, Prop = rigid/non-deforming, Auto = heuristic (long = road, short = prop).")]
    public SplineMeshDeformer.ColliderDeformation colliderMode = SplineMeshDeformer.ColliderDeformation.Auto;

    [Header("Element Specific Constraints")]
    public bool forceUpright = false;
    public bool deformProps = false;
    public bool lockPropX = false, lockPropY = false, lockPropZ = false;
    [Tooltip("Global offset applied to the position of this element relative to the spline.")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("Manual rotation offset (Euler angles) for this element.")]
    public Vector3 rotationOffset = Vector3.zero;
    [Tooltip("Custom scale for this element. Forward axis (Z) is overridden by 'Stretch to Index' if enabled.")]
    public Vector3 elementScale = Vector3.one;
    [Tooltip("Subdivides THIS layer's mesh this many times before deformation, so it bends smoothly on curves instead of looking faceted. Each level multiplies triangle count ~4x. 0 = no subdivision.")]
    [Range(0, 3)]
    public int subdivisions = 0;
    [Tooltip("Flips the mesh on selected axes.")]
    public bool flipX = false, flipY = false, flipZ = false;
    [Tooltip("If true, the asset will be stretched along the forward axis to perfectly fill the range from startIndex to endIndex.")]
    public bool stretchToIndexEnds = false;
    [Tooltip("Collider only. Lets the global 'Simplify Props as Boxes' replace this element's prop parts with a plain box in the generated mesh collider. The visible mesh is never affected.")]
    public bool allowBoxSimplification = true;

    [HideInInspector] public bool isExpanded = false; // For Inspector UI persistence
}

[System.Serializable]
public struct SerializedSubmesh
{
    public int[] triangles;
    public Material material;
}

[System.Serializable]
public struct SerializedNormalGroup
{
    public int[] indices;
}

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineMeshDeformer : MonoBehaviour
{
    public enum ForwardAxis { Z, X, Y, NegativeZ, NegativeX, NegativeY }
    public enum SourceMode { Mesh, Prefab }
    public enum ColliderDeformation 
    { 
        [InspectorName("Auto (for mixed)")] Auto, 
        Road, 
        Prop 
    }

    [Header("Curve Setup")]
    [Tooltip("The Bezier Spline component that defines the deformation path.")]
    public BezierSpline spline;
    
    [Header("Single Source Mode")]
    [Tooltip("Choose whether to use a single Mesh or a Prefab as the source for the deformation.")]
    public SourceMode sourceMode = SourceMode.Mesh;

    [Tooltip("The primary mesh object to be arrayed along the curve.")]
    public Mesh sourceMesh;
    [Tooltip("Alternatively, assign a Prefab here. The script will use the Mesh found in its children.")]
    public GameObject sourcePrefab;
    [Tooltip("Materials applied to the primary source mesh.")]
    public Material[] materials = new Material[1];
    
    [Header("Mixed Meshes Mode")]
    [Tooltip("Toggle this to use multiple different meshes at different segment indices (e.g., for variations or specialized end-caps).")]
    public bool useMixedMeshes = false;
    [Tooltip("List of mesh elements to place along specific parts of the curve.")]
    public SplineMeshElement[] mixedMeshes;

    [Header("Array Settings")]
    [Tooltip("How many copies of the mesh should be placed along the curve.")]
    public int segmentCount = 1;

    [Tooltip("Which axis of the source mesh represents the 'forward' direction along the curve.")]
    public ForwardAxis forwardAxis = ForwardAxis.Z;
    
    [Tooltip("Offset added to the spacing between instances. Negative values make them overlap.")]
    public float overlapOffset = 0f;

    [Tooltip("Scale applied to each mesh instance.")]
    public Vector3 meshScale = Vector3.one;

    [Header("Advanced Options")]
    [Tooltip("If true, vertices that share the same position on the boundaries will be merged.")]
    public bool mergeOverlappingVertices = true;

    [Tooltip("Automatically blends normals across segment seams and the loop seam for a perfectly smooth mesh.")]
    public bool smoothNormals = true;

    [Tooltip("Subdivides the source mesh this many times BEFORE deformation, adding geometry so it bends smoothly on curves instead of looking faceted. Each level multiplies triangle count ~4x. Applies to the visible mesh only (not the collider). Ignored in 'Use Mixed Meshes - LAYERS' mode, where each layer has its own subdivision setting.")]
    [Range(0, 3)]
    public int subdivisions = 0;

    [Tooltip("Distance threshold for merging vertices when overlapping is enabled. Larger values merge more aggressively.")]
    public float mergeDistance = 0.001f;

    [Tooltip("If true, the mesh is bent to follow the curve. If false, mesh instances are placed rigidly along the path.")]
    public bool deformMesh = true;

    [Tooltip("Controls mesh length at curve bends. 0 = no effect (even spacing). Positive values shrink mesh on bends. Negative values stretch mesh on bends.")]
    public float curveBendDensity = 0f;

    [Tooltip("Scales the chain so it ends exactly on the end of the curve, leaving no gap and no overshoot. Only road surfaces are stretched - props keep their authored size and are just repositioned. With this on, Segment Count acts as a density control: more segments means each one is shorter.")]
    public bool stretchToFitCurve = true;

    [Tooltip("If true, recreates UVs based on World Space position (useful for seamless textures).")]
    public bool generateWorldSpaceUVs = false;
    
    [Tooltip("Scale multiplier for World Space UVs.")]
    public float worldUvScale = 1f;

    [Tooltip("Smooths sharp direction changes at curve bends. 0 = sharp corners. Higher values create more gradual turns.")]
    [Range(0f, 1f)]
    public float smoothBendStrength = 0f;

    [Tooltip("If true, a MeshCollider will be generated/updated for the deformed mesh.")]
    public bool generateMeshCollider = false;

    [Tooltip("Simplifies the collider by skipping segments. 1 = full detail, 10 = very simplified.")]
    [Range(1, 10)]
    public int colliderSimplification = 1;

    [Tooltip("If true, props (lamps, poles) will be replaced with simplified boxes in the collider to further optimize physics.")]
    public bool simplifyPropsAsBoxes = true;

    [Tooltip("Global override for all elements. Auto = use per-element settings, Road = force all to bend, Prop = force all to be rigid.")]
    public ColliderDeformation globalColliderMode = ColliderDeformation.Auto;

    [Header("Rotation Constraints")]
    [Tooltip("Forces the X-axis (Pitch) rotation to remain 0.")]
    public bool lockRotationX = false;
    [Tooltip("Forces the Y-axis (Yaw) rotation to remain 0.")]
    public bool lockRotationY = false;
    [Tooltip("Forces the Z-axis (Roll) rotation to remain 0. Highly recommended for train cars or flat conveyor belts so they don't bank into corners.")]
    public bool lockRotationZ = false;

    [Header("Prop Constraints")]
    [Tooltip("Keep props vertical regardless of spline tilt/banking")]
    public bool forcePropsUpright = false;
    public bool lockPropsRotationX = false;
    public bool lockPropsRotationY = false;
    public bool lockPropsRotationZ = false;
    [Tooltip("Allow props to bend and stretch along the spline (useful for flexible pipes, cables, etc)")]
    public bool deformProps = false;
    [Tooltip("Global offset applied to the position of all props (unless overridden by element settings).")]
    public Vector3 globalPropsPositionOffset = Vector3.zero;

    [Tooltip("When enabled, the mesh automatically updates when parameters change.")]
    public bool autoDeform = true;
    
    [Header("Conveyor Animation")]
    [Tooltip("If true, the mesh will continuously scroll along the spline at runtime. Great for conveyor belts or tank treads.")]
    public bool animateConveyor = false;
    
    [Tooltip("Speed and direction of the conveyor animation.")]
    public float conveyorSpeed = 2f;

    [Header("Conveyor Performance")]
    [Tooltip("If true, disables heavy smoothing calculations (like RecalculateNormals) during Play Mode animation to maintain high FPS.")]
    public bool fastAnimationMode = true;

    [Tooltip("Prevents the MeshCollider from regenerating every frame during Play Mode animation. This stops massive CPU spikes from PhysX cooking and treats the track as a static collision surface.")]
    public bool staticColliderWhileAnimating = true;

    private int[] cachedVertexElementIndex; // -1 for global source
    private int[] cachedCollVertElementIndex;

    [SerializeField, HideInInspector] private int[] serVertexElementIndices;
    [SerializeField, HideInInspector] private int[] serCollVertElementIndices;
    
    // --- OPTIMIZATION CACHES ---
    private Mesh generatedMesh;
    private float currentConveyorOffset = 0f;
    private bool lastAnimateConveyor = false;
    private BezierSpline.CurveDistanceMapper cachedMapper;
    private BezierSpline lastMapperSpline;
    private float lastMapperBendDensity;
    private double lastRebuildTime = 0;
    private bool isInternalRebuild = false;

    [System.Serializable]
    public class MeshPart
    {
        public Mesh mesh;
        public Matrix4x4 localMatrix;
        public Material[] materials;
    }

    // Static Mesh Data Cache (Flattened but not yet bent)
    // We use a physical Mesh object because it can survive Assembly Reloads 
    // when entering Play Mode via HideAndDontSave, unlike private List<T> caches.
    private Mesh cachedStaticMesh;
    private Material[] cachedStaticMaterials = new Material[0];
    private List<int> cachedVertexSegmentIndices = new List<int>();
    private float[] cachedVertexFlatCenters;
    private bool[] cachedVertexIsRoad;
    private bool staticMeshDirty = true;
    private float lastStepDistance, lastMeshLengthZ, lastMinZ, lastTotalFlatLength;
    private Vector2Int[] cachedSnapPairs; // Pairs of vertex indices (current, neighbor) for post-deform snapping

    // Animation Array Caches (Zero-Allocation)
    private Vector3[] cachedDeformedVerts;
    private Vector3[] cachedDeformedNormals;
    private Vector2[] cachedFinalUvs;
    private Vector3[] cachedCollDeformedVerts;
    
    // Cache for Mixed Meshes (when using Prefabs)
    private Dictionary<GameObject, Mesh> elementMeshCache = new Dictionary<GameObject, Mesh>();
    private Dictionary<GameObject, Material[]> elementMatCache = new Dictionary<GameObject, Material[]>();

    // Fast Animation LUT (Zero-Math in Play Mode)
    private int lutResolution = 1000;
    private Vector3[] lutPos;
    private Quaternion[] lutRot;
    private Vector3[] lutScale;
    private bool lutValid = false;
    private int[] segmentToElementMap; 

    // Prefab combination cache
    private Mesh combinedPrefabMesh;
    private Material[] combinedPrefabMaterials;
    private GameObject lastCombinedPrefab;

    // Pre-deformation subdivision cache (rebuilt fresh on every static rebuild).
    // Key is (source mesh, level, forward axis); value is an owned, densified mesh copy.
    [System.NonSerialized] private Dictionary<(Mesh mesh, int level, Vector3 forward), Mesh> subdivCache;

    // Serialization Fields (Hidden data backup for Builds/PlayMode to bypass Read/Write restriction)
    private Vector3[] serVerts, serCollVerts;
    private Vector3[] serNormals;
    private Vector2[] serUvs;
    private int[] serSegmentIndices, serCollVertSegmentIndices;
    private float[] serCollZMults;
    private SerializedSubmesh[] serSubmeshes, serCollSubmeshes;
    [SerializeField, HideInInspector] private float serLastStepDist, serLastMeshLen, serLastMinZ, serLastTotalLen;
    [SerializeField, HideInInspector] private bool serHasColliderBake;
    private SerializedNormalGroup[] serNormalGroups;
    private List<int[]> sharedNormalGroups;

    [SerializeField, HideInInspector] private float[] serVertexFlatCenters, serCollFlatCenters;
    [SerializeField, HideInInspector] private float[] serVertexIsRoad, serCollIsRoad, serCollIsBoxProxy;

    private Mesh generatedColliderMesh;
    private Vector3[] cachedCollFlatVerts;
    private int[] cachedCollVertSegmentIndices;
    private float[] cachedCollZMults;
    private float[] cachedCollFlatCenters;
    private bool[] cachedCollIsRoad, cachedCollIsBoxProxy;
    private Material[] cachedCollMaterials;

    private void OnValidate()
    {
        // RESET: If user unchecks animateConveyor, reset the position to 0
        if (lastAnimateConveyor && !animateConveyor)
        {
            currentConveyorOffset = 0f;
            staticMeshDirty = true;
        }
        lastAnimateConveyor = animateConveyor;

        // Don't rebuild if we are already in the middle of a frame or an internal update
        if (!autoDeform || spline == null || isInternalRebuild) return;
        
        double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (currentTime - lastRebuildTime < 0.1) return; // Throttle to 10 FPS
        
        staticMeshDirty = true; 
        
        // Remove any previous pending calls to ensure only the latest one runs
        UnityEditor.EditorApplication.delayCall -= BufferedDeform;
        UnityEditor.EditorApplication.delayCall += BufferedDeform;
    }

    private void BufferedDeform()
    {
        if (this != null && spline != null) Deform();
    }

    public void RefreshMixedMeshes(bool assetsChanged = false, bool geometryChanged = true)
    {
        if (geometryChanged) staticMeshDirty = true;
        materialsDirty = true;
        
        // If assets changed (new prefab assigned or removed), we MUST clear caches.
        // However, if we just ADDED a new empty element slot, we don't necessarily 
        // need to wipe the existing prefab-mesh cache.
        if (assetsChanged) MarkCachesDirty();
        
        // Throttle only if this is a frequent update (like typing or scrubbing)
        // BUT if it's a checkbox click (geometryChanged=true), we want immediate feedback.
        double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!geometryChanged && currentTime - lastRebuildTime < 0.1) return;
        lastRebuildTime = currentTime;

        UnityEditor.EditorApplication.delayCall -= BufferedDeform;
        UnityEditor.EditorApplication.delayCall += BufferedDeform;
    }

    /// <summary>
    /// True if any layer carries its own upright/lock-rotation constraint.
    /// <para>
    /// The fast animation LUT bakes one rotation per curve position from the *global* locks only
    /// (see BuildLUT), so it physically cannot represent per-element constraints. Rather than let
    /// those settings silently stop working once Fast Animation Mode kicks in, we fall back to the
    /// exact per-vertex path whenever any layer actually needs it. Costs frame time only in the
    /// scenes that would otherwise render incorrectly.
    /// </para>
    /// </summary>
    private bool HasPerElementRotationOverrides()
    {
        if (!useMixedMeshes || mixedMeshes == null) return false;

        for (int i = 0; i < mixedMeshes.Length; i++)
        {
            var e = mixedMeshes[i];
            if (e == null) continue;
            if (e.forceUpright || e.lockPropX || e.lockPropY || e.lockPropZ) return true;
        }
        return false;
    }

    public void RecalculateSegmentCountFromMesh(Mesh mesh, Vector3 elementScale)
    {
        if (spline == null || mesh == null) return;

        float splineLength = GetMapper().GetTotalPhysicalLength();
        if (splineLength < 0.001f) return;

        // Get bounds size in standard local space
        Bounds b = mesh.bounds;
        Vector3 stdSize = BezierSpline.MapToStandardLocal(b.size, (BezierSpline.ForwardAxis)forwardAxis);
        float meshLengthZ = Mathf.Abs(stdSize.z);

        if (meshLengthZ < 0.001f) return;

        // Calculate step (overlap offset is global)
        float step = (meshLengthZ * meshScale.z * elementScale.z) + overlapOffset;
        if (step < 0.001f) return;

        segmentCount = Mathf.Max(1, Mathf.RoundToInt(splineLength / step));
        
        // If it's a loop, we might want to adjust to ensure it closes perfectly if stretch is off?
        // Actually, let the user decide, but RoundToInt is a good starting point.
        
        staticMeshDirty = true;
        RefreshMixedMeshes(false, true);
    }

    public void RecalculateSegmentCountFromPrefab(GameObject prefab, Vector3 elementScale)
    {
        if (prefab == null) return;

        // Extract the "primary" mesh from prefab for length calculation.
        // Resolve through the ProBuilder bridge so prefab assets (where sharedMesh is null) work too.
        Mesh m = null;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            m = ProBuilderSupport.ResolveRenderMesh(mf);
            if (m != null) break;
        }

        if (m != null)
        {
            RecalculateSegmentCountFromMesh(m, elementScale);
        }
    }

    private void Awake()
    {
        // Force a rebuild when the object wakes up (e.g. entering Play Mode)
        // This ensures that any cached data lost during assembly reload is recreated.
        staticMeshDirty = true;
    }

    private void Start()
    {
        // Double check on Start to ensure everything is initialized for Play Mode
        if (Application.isPlaying)
        {
            staticMeshDirty = true;
            Deform();
        }
    }

#if UNITY_EDITOR
    private double lastEditorTime = 0;

    private void OnEnable()
    {
        cachedVertexElementIndex = serVertexElementIndices;
        cachedCollVertElementIndex = serCollVertElementIndices;
        
        if (Application.isPlaying) RefreshMixedMeshes(true);
        {
            UnityEditor.EditorApplication.update += EditorUpdate;
            lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.update -= EditorUpdate;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!generateMeshCollider || generatedColliderMesh == null) return;
        
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f); // Cyan for normal collider
        Vector3[] v = generatedColliderMesh.vertices;
        if (v == null || v.Length == 0) return;

        // Draw vertices as small spheres to show density/simplification
        for (int i = 0; i < v.Length; i += Mathf.Max(1, v.Length / 500))
        {
            // If it's part of a box proxy, draw it green
            bool isBox = (cachedCollIsBoxProxy != null && i < cachedCollIsBoxProxy.Length) ? cachedCollIsBoxProxy[i] : false;
            Gizmos.color = isBox ? Color.green : new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawSphere(transform.TransformPoint(v[i]), isBox ? 0.05f : 0.02f);
        }
    }
#endif

    private void EditorUpdate()
    {
        if (this == null) return;
        if (!Application.isPlaying && animateConveyor && spline != null && segmentCount > 0)
        {
            double currentEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = (float)(currentEditorTime - lastEditorTime);
            lastEditorTime = currentEditorTime;

            AdvanceConveyor(dt);
            UnityEditor.SceneView.RepaintAll();
        }

        if (lastColliderRequestTime > 0 && UnityEditor.EditorApplication.timeSinceStartup - lastColliderRequestTime > 0.8)
        {
            UpdateCollider();
            lastColliderRequestTime = -1;
        }
    }
#endif

    private void Update()
    {
        if (Application.isPlaying && animateConveyor && spline != null && segmentCount > 0)
        {
            AdvanceConveyor(Time.deltaTime);
        }
    }

    private void AdvanceConveyor(float dt)
    {
        currentConveyorOffset += dt * conveyorSpeed;
        if (spline != null)
        {
            float curveLen = GetMapper().GetTotalPhysicalLength();
            if (curveLen > 0.0001f) currentConveyorOffset = Mathf.Repeat(currentConveyorOffset, curveLen);
        }
        Deform(); 
    }

    private BezierSpline.CurveDistanceMapper GetMapper()
    {
        if (cachedMapper == null || lastMapperSpline != spline || lastMapperBendDensity != curveBendDensity)
        {
            cachedMapper = new BezierSpline.CurveDistanceMapper(spline, curveBendDensity, 1000);
            lastMapperSpline = spline;
            lastMapperBendDensity = curveBendDensity;
        }
        return cachedMapper;
    }

    [ContextMenu("Deform")]
    public void Deform(bool isDragging = false)
    {
        if (spline == null || segmentCount <= 0 || isInternalRebuild) return;

        try 
        {
            isInternalRebuild = true;
            lastRebuildTime = UnityEditor.EditorApplication.timeSinceStartup;

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh();
                generatedMesh.name = "Deformed Mesh";
                generatedMesh.hideFlags = HideFlags.HideAndDontSave; // Prevent GC in Play Mode
                generatedMesh.MarkDynamic();
            }

            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != generatedMesh)
            {
                mf.sharedMesh = generatedMesh;
            }

        if (staticMeshDirty || cachedStaticMesh == null || cachedStaticMesh.vertexCount == 0 || (Application.isPlaying && animateConveyor && (cachedStaticMesh == null || cachedStaticMesh.vertexCount == 0)))
        {
            staticMeshDirty = false; // Clear immediately to prevent infinite retry loops on error
            
            // NEW: If we are animating (or in Play Mode) and have serialized data, use it!
            if (serVerts != null && serVerts.Length > 0 && (Application.isPlaying || animateConveyor))
            {
                HydrateStaticFromSerialized();
            }
            else
            {
                RebuildStaticMesh();
                lutValid = false;
            }
        }
        else if (materialsDirty && !Application.isPlaying)
        {
            RebuildMaterialsOnly();
        }
        
        // If it's STILL empty after a forced rebuild, then we truly have no geometry to deform.
        if (cachedStaticMesh == null || cachedStaticMesh.vertexCount == 0) 
        {
            if (generatedMesh != null) generatedMesh.Clear();
            return;
        }

        var mapper = GetMapper();
        float curveLength = mapper.GetTotalPhysicalLength();
        float lengthScale = stretchToFitCurve && lastTotalFlatLength > 0.001f ? curveLength / lastTotalFlatLength : 1f;

        Vector3[] flatVerts = cachedStaticMesh.vertices;
        Vector3[] flatNormals = cachedStaticMesh.normals;
        Vector2[] flatUvs = cachedStaticMesh.uv;

        if (cachedDeformedVerts == null || cachedDeformedVerts.Length != flatVerts.Length)
        {
            cachedDeformedVerts = new Vector3[flatVerts.Length];
            cachedDeformedNormals = new Vector3[flatVerts.Length];
            cachedFinalUvs = new Vector2[flatVerts.Length];
        }

        Vector3[] deformedVerts = cachedDeformedVerts;
        Vector3[] deformedNormals = cachedDeformedNormals;
        Vector2[] finalUvs = cachedFinalUvs;

        bool isRigid = !deformMesh;
        bool useFastLUT = Application.isPlaying && animateConveyor && fastAnimationMode && !isRigid
                          && !HasPerElementRotationOverrides();

        if (useFastLUT && !lutValid) BuildLUT(curveLength, mapper);

        int maxSegs = segmentCount;
        Vector3[] rigidCenters = null;
        Quaternion[] rigidRots = null;
        Quaternion[] rigidRotsProps = null; // FIXED
        Vector3[] rigidScales = null;

        // --- RIGID ALIGNMENT CACHE (Used by Props always, and by Roads if deformMesh is false) ---
        rigidCenters = new Vector3[maxSegs];
        rigidRots = new Quaternion[maxSegs];
        rigidRotsProps = new Quaternion[maxSegs];
        rigidScales = new Vector3[maxSegs];
        
        float sWindow = Mathf.Max(0.001f, smoothBendStrength * 0.2f);
        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);

        for (int s = 0; s < maxSegs; s++)
        {
            float flatS = s * lastStepDistance;
            float flatE = (s + 1) * lastStepDistance;
            float splineS = flatS * lengthScale + currentConveyorOffset;
            float splineE = flatE * lengthScale + currentConveyorOffset;
            float sMidT = mapper.GetTAtDistance((splineS + splineE) * 0.5f);
            
            Vector3 wS = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(splineS)));
            Vector3 wE = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(splineE)));
            Vector3 wCenter = (wS + wE) * 0.5f;
            rigidCenters[s] = transform.InverseTransformPoint(wCenter);
            
            Vector3 wChord = (wE - wS);
            if (wChord.sqrMagnitude < 0.0001f) wChord = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, sMidT, mapper, smoothBendStrength));
            Vector3 lChord = transform.InverseTransformDirection(wChord.normalized);
            if (lChord.sqrMagnitude < 0.0001f) lChord = Vector3.forward;

            rigidRots[s] = BezierSpline.GetRotationFromForward(spline, lChord, sMidT, lockRotationX, lockRotationY, lockRotationZ, sWindow);
            rigidRotsProps[s] = BezierSpline.GetRotationFromForward(spline, lChord, sMidT, lockRotationX || lockPropsRotationX, lockRotationY || lockPropsRotationY, lockRotationZ || lockPropsRotationZ, sWindow, forcePropsUpright, localUp);
            rigidScales[s] = spline.GetScale(sMidT, sWindow);
        }

        for (int i = 0; i < flatVerts.Length; i++)
        {
            Vector3 pt = flatVerts[i];
            float rawZ = pt.z;
            
            Vector3 curvePoint = Vector3.zero;
            Vector3 localOffset = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector3 pScale = Vector3.one;

            int eIdx = (cachedVertexElementIndex != null && i < cachedVertexElementIndex.Length) ? cachedVertexElementIndex[i] : -1;
            Vector3 pOffset = Vector3.zero;
            Vector3 rOffset = Vector3.zero; 
            bool forceUprightV = forcePropsUpright;
            bool lockPX = lockPropsRotationX, lockPY = lockPropsRotationY, lockPZ = lockPropsRotationZ;
            bool dPropsV = deformProps;
            bool isRoadV = (cachedVertexIsRoad != null && i < cachedVertexIsRoad.Length) ? cachedVertexIsRoad[i] : true;
            
            if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length)
            {
                var elem = mixedMeshes[eIdx];
                pOffset = elem.positionOffset;
                rOffset = elem.rotationOffset;
                // Constraints add up, they never loosen: an element can force upright on top of the
                // global switch, but a default (false) element must not silently cancel it. This
                // matches how the rotation locks below are combined.
                forceUprightV = forcePropsUpright || elem.forceUpright;
                lockPX = elem.lockPropX; lockPY = elem.lockPropY; lockPZ = elem.lockPropZ;
                dPropsV = elem.deformProps;
            }
            else if (isRoadV == false)
            {
                pOffset = globalPropsPositionOffset;
            }
            
            bool shouldDeformV = !isRigid && (isRoadV || dPropsV);

            // --- PROPS SCALE / STRETCH FIX ---
            // If stretching is OFF for this element, we calculate finalZ such that its 1:1 length is preserved 
            // relative to its instance center, preventing global 'Stretch to Fit' from elongating it.
            float pCC = (cachedVertexFlatCenters != null && i < cachedVertexFlatCenters.Length) ? cachedVertexFlatCenters[i] : (rawZ - (lastMinZ * meshScale.z));
            bool isStretchingV = isRoadV || (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length && mixedMeshes[eIdx].stretchToIndexEnds);

            float finalZ;
            if (isStretchingV)
            {
                finalZ = (rawZ - (lastMinZ * meshScale.z)) * lengthScale + currentConveyorOffset;
            }
            else
            {
                float stretchedCC = (pCC - (lastMinZ * meshScale.z)) * lengthScale + currentConveyorOffset;
                finalZ = stretchedCC + (rawZ - pCC);
            }

            if (shouldDeformV)
            {
                // FULL DEFORMATION (Bending Road)
                if (useFastLUT)
                {
                    float ratio = (finalZ + pOffset.z * lengthScale) / curveLength;
                    ratio -= Mathf.Floor(ratio); // Repeat 0-1
                    float fIdx = ratio * (lutResolution - 1);
                    int idx = (int)fIdx;
                    if (idx >= lutResolution - 1) idx = lutResolution - 2;
                    float tLerp = fIdx - idx;

                    curvePoint = Vector3.LerpUnclamped(lutPos[idx], lutPos[idx + 1], tLerp);
                    rotation = Quaternion.SlerpUnclamped(lutRot[idx], lutRot[idx + 1], tLerp);
                    pScale = Vector3.LerpUnclamped(lutScale[idx], lutScale[idx + 1], tLerp);
                }
                else
                {
                    float curvedT = mapper.GetTAtDistance(finalZ + pOffset.z * lengthScale);
                    Vector3 worldPos = spline.transform.TransformPoint(spline.GetPoint(curvedT));
                    curvePoint = transform.InverseTransformPoint(worldPos);

                    Vector3 worldDir = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, curvedT, mapper, smoothBendStrength));
                    if (worldDir.sqrMagnitude < 0.0001f) worldDir = Vector3.forward;
                    Vector3 localDir = transform.InverseTransformDirection(worldDir);
                    if (localDir.sqrMagnitude < 0.0001f) localDir = Vector3.forward;

                    bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                    bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                    bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                    rotation = BezierSpline.GetRotationFromForward(spline, localDir, curvedT, finalLX, finalLY, finalLZ, sWindow, forceUprightV, localUp);
                    pScale = spline.GetScale(curvedT, sWindow);
                }
                
                // --- APPLY MANUAL ROTATION ---
                if (rOffset.sqrMagnitude > 0.0001f) rotation *= Quaternion.Euler(rOffset);
                
                localOffset = new Vector3(pt.x + pOffset.x, pt.y + pOffset.y, 0f);
            }
            else
            {
                // RIGID DEFORMATION (Props or Rigid Road)
                int segIdx = cachedVertexSegmentIndices[i];
                if (segIdx >= maxSegs) segIdx = maxSegs - 1;
                
                curvePoint = rigidCenters[segIdx];
                pScale = rigidScales[segIdx];
                
                // --- FIXED: RESPECT PER-ELEMENT CONSTRAINTS IN RIGID MODE ---
                if (!isRoadV)
                {
                    bool hasOverrides = (forceUprightV != forcePropsUpright) || lockPX || lockPY || lockPZ;
                    if (hasOverrides)
                    {
                        float sMidT = mapper.GetTAtDistance((segIdx + 0.5f) * lastStepDistance * lengthScale + currentConveyorOffset);
                        
                        bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                        bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                        bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                        rotation = BezierSpline.GetRotationFromForward(spline, rigidRotsProps[segIdx] * Vector3.forward, sMidT, finalLX, finalLY, finalLZ, sWindow, forceUprightV, localUp);
                    }
                    else
                    {
                        rotation = rigidRotsProps[segIdx];
                    }
                }
                else
                {
                    rotation = rigidRots[segIdx];
                }

                // If we have a Z-offset, we must RE-SAMPLE the curve to avoid drifting on bends
                if (Mathf.Abs(pOffset.z) > 0.0001f)
                {
                    float flatCenter = (segIdx + 0.5f) * lastStepDistance;
                    float shiftedSplineDist = (flatCenter + pOffset.z) * lengthScale + currentConveyorOffset;
                    float shiftedT = mapper.GetTAtDistance(shiftedSplineDist);
                    
                    Vector3 wShiftedPos = spline.transform.TransformPoint(spline.GetPoint(shiftedT));
                    curvePoint = transform.InverseTransformPoint(wShiftedPos);
                    pScale = spline.GetScale(shiftedT, Mathf.Max(0.001f, smoothBendStrength * 0.2f));

                    if (!isRoadV)
                    {
                        // Dynamic prop rotation for shifted position
                        float halfLen = 0.5f * lastStepDistance * lengthScale;
                        float sS = shiftedSplineDist - halfLen;
                        float sE = shiftedSplineDist + halfLen;
                        
                        Vector3 wS = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(sS)));
                        Vector3 wE = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(sE)));
                        Vector3 wChord = (wE - wS);
                        if (wChord.sqrMagnitude < 0.0001f) wChord = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, shiftedT, mapper, smoothBendStrength));
                        Vector3 lChord = transform.InverseTransformDirection(wChord.normalized);
                        
                        sWindow = Mathf.Max(0.001f, smoothBendStrength * 0.2f);
                        localUp = transform.InverseTransformDirection(Vector3.up);
                        bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                        bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                        bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                        rotation = BezierSpline.GetRotationFromForward(spline, lChord, shiftedT, finalLX, finalLY, finalLZ, sWindow, forceUprightV, localUp);
                    }
                }
                
                // --- APPLY MANUAL ROTATION ---
                if (rOffset.sqrMagnitude > 0.0001f) rotation *= Quaternion.Euler(rOffset);
                
                float flatCenterDist = (cachedVertexFlatCenters != null && cachedVertexFlatCenters.Length > i) ? cachedVertexFlatCenters[i] : (segIdx + 0.5f) * lastStepDistance;
                float localZ = pt.z - flatCenterDist;
                // Longitudinal shift is now handled by curve sampling, not localOffset!
                localOffset = new Vector3(pt.x + pOffset.x, pt.y + pOffset.y, localZ);
            }

            Vector3 finalDeformedPt = curvePoint + rotation * Vector3.Scale(localOffset, pScale);
            // Fallback against extreme floating point corruption
            if (float.IsNaN(finalDeformedPt.x) || float.IsInfinity(finalDeformedPt.x)) finalDeformedPt = curvePoint;
            
            deformedVerts[i] = finalDeformedPt;
            Vector3 finalNorm = (rotation * flatNormals[i]).normalized;
            if (float.IsNaN(finalNorm.x)) finalNorm = Vector3.up;
            deformedNormals[i] = finalNorm;
            
            finalUvs[i] = generateWorldSpaceUVs ? new Vector2(pt.x * worldUvScale, finalZ * worldUvScale) : (flatUvs.Length > i ? flatUvs[i] : Vector2.zero);
        }

        // --- POST-DEFORMATION MERGE (Spatial Hash on FINAL positions) ---
        // This closes gaps on curves in rigid mode by finding vertices that are 
        // close in DEFORMED space (not flat space). Works for all modes.
        if (mergeOverlappingVertices && mergeDistance > 0.0001f)
        {
            float postCS = Mathf.Max(mergeDistance, 0.01f);
            float postInvCS = 1f / postCS;
            float postMergeSq = mergeDistance * mergeDistance;
            
            // Build spatial hash of deformed positions
            var postCells = new Dictionary<Vector3Int, List<int>>(deformedVerts.Length / 4);
            for (int vi = 0; vi < deformedVerts.Length; vi++)
            {
                Vector3 dv = deformedVerts[vi];
                Vector3Int k = new Vector3Int(
                    Mathf.FloorToInt(dv.x * postInvCS),
                    Mathf.FloorToInt(dv.y * postInvCS),
                    Mathf.FloorToInt(dv.z * postInvCS));
                if (!postCells.TryGetValue(k, out var list))
                    postCells[k] = list = new List<int>(4);
                list.Add(vi);
            }
            
            // Snap: for each vertex, find nearest from adjacent segment
            bool[] snapped = new bool[deformedVerts.Length];
            for (int vi = 0; vi < deformedVerts.Length; vi++)
            {
                if (snapped[vi]) continue;
                int segA = cachedVertexSegmentIndices[vi];
                Vector3 vA = deformedVerts[vi];
                Vector3Int k = new Vector3Int(
                    Mathf.FloorToInt(vA.x * postInvCS),
                    Mathf.FloorToInt(vA.y * postInvCS),
                    Mathf.FloorToInt(vA.z * postInvCS));
                    
                float bestDist = postMergeSq;
                int bestIdx = -1;
                
                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                for (int oz = -1; oz <= 1; oz++)
                {
                    var nk = new Vector3Int(k.x + ox, k.y + oy, k.z + oz);
                    if (!postCells.TryGetValue(nk, out var list)) continue;
                    for (int m = 0; m < list.Count; m++)
                    {
                        int vj = list[m];
                        if (vj <= vi || snapped[vj]) continue;
                        int segB = cachedVertexSegmentIndices[vj];
                        
                        // Circular adjacency check for loops (0 connects to N-1)
                        int diff = Mathf.Abs(segA - segB);
                        bool isAdjacent = (diff == 1) || (spline != null && spline.loop && diff == segmentCount - 1);
                        if (!isAdjacent) continue;
                        
                        float dSq = (deformedVerts[vj] - vA).sqrMagnitude;
                        if (dSq < bestDist) { bestDist = dSq; bestIdx = vj; }
                    }
                }
                
                if (bestIdx >= 0)
                {
                    Vector3 avg = (deformedVerts[vi] + deformedVerts[bestIdx]) * 0.5f;
                    deformedVerts[vi] = avg;
                    deformedVerts[bestIdx] = avg;
                    snapped[vi] = true;
                    snapped[bestIdx] = true;
                }
            }
        }

        generatedMesh.Clear();
        if (deformedVerts.Length > 65535) generatedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        generatedMesh.vertices = deformedVerts;
        generatedMesh.normals = deformedNormals;
        generatedMesh.uv = finalUvs;
        
        generatedMesh.subMeshCount = cachedStaticMesh.subMeshCount;
        
        // Copy triangles straight from the indestructible static mesh cache
        for (int s = 0; s < cachedStaticMesh.subMeshCount; s++)
        {
            generatedMesh.SetTriangles(cachedStaticMesh.GetTriangles(s), s);
        }
        
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterials = cachedStaticMaterials;
        
        // --- AGGRESSIVE FRUSTUM CULLING FIX ---
        if (spline != null && spline.points != null && spline.points.Length > 0)
        {
            Vector3 localCenter = transform.InverseTransformPoint(spline.transform.TransformPoint(spline.points[0]));
            Bounds safeBounds = new Bounds(localCenter, Vector3.one);
            for (int p = 1; p < spline.points.Length; p++)
            {
                Vector3 ptStr = transform.InverseTransformPoint(spline.transform.TransformPoint(spline.points[p]));
                safeBounds.Encapsulate(ptStr);
            }
            // Add padding for the mesh thickness itself
            float maxThickness = Mathf.Max(lastMeshLengthZ * meshScale.z, 10f); // Minimum 10 units padding
            safeBounds.Expand(maxThickness);
            generatedMesh.bounds = safeBounds;
        }
        else
        {
            generatedMesh.RecalculateBounds();
            Bounds b = generatedMesh.bounds;
            b.Expand(10f); 
            generatedMesh.bounds = b;
        }

        bool isAnimating = Application.isPlaying && animateConveyor;
        bool skipNormals = isAnimating && fastAnimationMode;
        
        if (!skipNormals)
        {
            generatedMesh.RecalculateNormals();
            
            if (smoothNormals && sharedNormalGroups != null && sharedNormalGroups.Count > 0)
            {
                Vector3[] nrms = generatedMesh.normals;
                foreach (var group in sharedNormalGroups)
                {
                    Vector3 avg = Vector3.zero;
                    for (int idx = 0; idx < group.Length; idx++) avg += nrms[group[idx]];
                    if (avg.sqrMagnitude > 0.0001f)
                    {
                        avg.Normalize();
                        for (int idx = 0; idx < group.Length; idx++) nrms[group[idx]] = avg;
                    }
                }
                generatedMesh.normals = nrms;
            }
        }
        
        // NEW: We only skip UPDATING the collider if it's already built and we are in static mode.
        isAnimating = Application.isPlaying && animateConveyor;
        bool shouldSkipUpdates = isDragging || (isAnimating && staticColliderWhileAnimating);
        bool needsInitialBuild = (generatedColliderMesh == null || generatedColliderMesh.vertexCount == 0);
        
        if (generateMeshCollider && (!shouldSkipUpdates || needsInitialBuild))
        {
            DeformCollider();
            UpdateCollider(); // Immediate update for collider
        }
        else if (generateMeshCollider && !isAnimating)
        {
            // In editor, ensure it's at least assigned if it exists
            UpdateCollider();
        }
        }
        finally
        {
            isInternalRebuild = false;
        }
    }

    private void DeformCollider()
    {
        if (cachedCollFlatVerts == null || cachedCollFlatVerts.Length == 0) return;
        if (generatedColliderMesh == null)
        {
            generatedColliderMesh = new Mesh();
            generatedColliderMesh.name = "Deformed Collider Mesh";
            generatedColliderMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        var mapper = GetMapper();
        float curveLength = mapper.GetTotalPhysicalLength();
        float lengthScale = stretchToFitCurve && lastTotalFlatLength > 0.001f ? curveLength / lastTotalFlatLength : 1f;

        float activeOffset = staticColliderWhileAnimating ? 0f : currentConveyorOffset;

        if (cachedCollDeformedVerts == null || cachedCollDeformedVerts.Length != cachedCollFlatVerts.Length)
        {
            cachedCollDeformedVerts = new Vector3[cachedCollFlatVerts.Length];
        }

        int maxSegs = segmentCount;
        Vector3[] rigidCenters = new Vector3[maxSegs];
        Quaternion[] rigidRots = new Quaternion[maxSegs];
        Vector3[] rigidScales = new Vector3[maxSegs];
        
        float sWindow = Mathf.Max(0.001f, smoothBendStrength * 0.2f);
        Vector3 worldUp = Vector3.up;
        Vector3 localUp = transform.InverseTransformDirection(worldUp);

        for (int s = 0; s < maxSegs; s++)
        {
            float flatS = s * lastStepDistance;
            float flatE = (s + 1) * lastStepDistance;
            float splineS = flatS * lengthScale + activeOffset;
            float splineE = flatE * lengthScale + activeOffset;
            float sMidT = mapper.GetTAtDistance((splineS + splineE) * 0.5f);
            
            Vector3 wS = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(splineS)));
            Vector3 wE = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(splineE)));
            
            rigidCenters[s] = transform.InverseTransformPoint((wS + wE) * 0.5f);
            
            Vector3 wChord = (wE - wS);
            if (wChord.sqrMagnitude < 0.0001f) wChord = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, sMidT, mapper, smoothBendStrength));
            Vector3 lChord = transform.InverseTransformDirection(wChord.normalized);
            if (lChord.sqrMagnitude < 0.0001f) lChord = Vector3.forward;

            rigidRots[s] = BezierSpline.GetRotationFromForward(spline, lChord, sMidT, lockRotationX, lockRotationY, lockRotationZ, sWindow);
            rigidScales[s] = spline.GetScale(sMidT, sWindow);
        }

        Vector3[] deformedVerts = cachedCollDeformedVerts;

        bool useFastLUT = Application.isPlaying && animateConveyor && fastAnimationMode && deformMesh
                          && !HasPerElementRotationOverrides();
        if (useFastLUT && !lutValid) BuildLUT(curveLength, mapper);

        for (int i = 0; i < cachedCollFlatVerts.Length; i++)
        {
            Vector3 pt = cachedCollFlatVerts[i];
            float rawZ = pt.z;
            
            float curvedT = 0, fMid = 0;
            Vector3 curvePoint = Vector3.zero;
            Vector3 pScale = Vector3.one;
            Quaternion rotation = Quaternion.identity;
            Vector3 localOffset = Vector3.zero;
            Vector3 worldPos;

            float mult = cachedCollZMults != null && cachedCollZMults.Length > i ? cachedCollZMults[i] : 1f; 

            int eIdx = (cachedCollVertElementIndex != null && i < cachedCollVertElementIndex.Length) ? cachedCollVertElementIndex[i] : -1;
            Vector3 pOffset = Vector3.zero;
            Vector3 rOffset = Vector3.zero; 
            bool forceUprightC = forcePropsUpright;
            bool lockPX = lockPropsRotationX, lockPY = lockPropsRotationY, lockPZ = lockPropsRotationZ;
            bool dPropsC = deformProps;

            bool isRoadPart = mult > 999f;
            
            if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length)
            {
                var elem = mixedMeshes[eIdx];
                pOffset = elem.positionOffset;
                rOffset = elem.rotationOffset;
                // Same additive rule as the visual mesh, so collider and geometry stay in sync.
                forceUprightC = forcePropsUpright || elem.forceUpright;
                lockPX = elem.lockPropX; lockPY = elem.lockPropY; lockPZ = elem.lockPropZ;
                dPropsC = elem.deformProps;
            }
            else if (isRoadPart == false)
            {
                pOffset = globalPropsPositionOffset;
            }

            bool shouldDeformC = deformMesh && (isRoadPart || dPropsC);
            
            // --- PROPS SCALE / STRETCH FIX (Collider) ---
            // If stretching is OFF for this element, we calculate finalZ relative to its center (pCC), 
            // ensuring the global 'lengthScale' doesn't cause unwanted elongation of props.
            float pCC = (cachedCollFlatCenters != null && i < cachedCollFlatCenters.Length) ? cachedCollFlatCenters[i] : (rawZ - (lastMinZ * meshScale.z));
            bool isStretchingC = isRoadPart || (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length && mixedMeshes[eIdx].stretchToIndexEnds);

            float finalZ;
            if (isStretchingC)
            {
                finalZ = (rawZ - (lastMinZ * meshScale.z)) * lengthScale + activeOffset;
            }
            else
            {
                float stretchedCC = (pCC - (lastMinZ * meshScale.z)) * lengthScale + activeOffset;
                finalZ = stretchedCC + (rawZ - pCC);
            }

            if (shouldDeformC)
            {
                // FULL DEFORMATION (For continuous road surfaces/bent props)
                if (useFastLUT)
                {
                    float ratio = (finalZ + pOffset.z * lengthScale) / curveLength;
                    ratio -= Mathf.Floor(ratio); // Repeat 0-1
                    float fIdx = ratio * (lutResolution - 1);
                    int idx = (int)fIdx;
                    if (idx >= lutResolution - 1) idx = lutResolution - 2;
                    float tLerp = fIdx - idx;

                    curvePoint = Vector3.LerpUnclamped(lutPos[idx], lutPos[idx + 1], tLerp);
                    rotation = Quaternion.SlerpUnclamped(lutRot[idx], lutRot[idx + 1], tLerp);
                    pScale = Vector3.LerpUnclamped(lutScale[idx], lutScale[idx + 1], tLerp);
                }
                else
                {
                    curvedT = mapper.GetTAtDistance(finalZ + pOffset.z * lengthScale);
                    worldPos = spline.transform.TransformPoint(spline.GetPoint(curvedT));
                    curvePoint = transform.InverseTransformPoint(worldPos);

                    Vector3 worldDir = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, curvedT, mapper, smoothBendStrength));
                    if (worldDir.sqrMagnitude < 0.0001f) worldDir = Vector3.forward;
                    Vector3 localDir = transform.InverseTransformDirection(worldDir);
                    if (localDir.sqrMagnitude < 0.0001f) localDir = Vector3.forward;

                    bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                    bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                    bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                    rotation = BezierSpline.GetRotationFromForward(spline, localDir, curvedT, finalLX, finalLY, finalLZ, sWindow, forceUprightC, localUp);
                    pScale = spline.GetScale(curvedT, sWindow);
                }
                
                // --- APPLY MANUAL ROTATION ---
                if (rOffset.sqrMagnitude > 0.0001f) rotation *= Quaternion.Euler(rOffset);
                
                localOffset = new Vector3(pt.x + pOffset.x, pt.y + pOffset.y, 0f);
            }
            else
            {
                // RIGID DEFORMATION (For Props like railings, lamps, poles - prevents distortion)
                // Unified Chord Alignment: Must match the visual mesh logic exactly to avoid offsets.
                int segIdx = cachedCollVertSegmentIndices != null && cachedCollVertSegmentIndices.Length > i ? cachedCollVertSegmentIndices[i] : 0;
                if (segIdx >= segmentCount) segIdx = segmentCount - 1;
                float multProp = cachedCollZMults != null && cachedCollZMults.Length > i ? cachedCollZMults[i] : 1f; 
                float curCollMult = multProp > 999f ? (multProp - 1000f) : multProp;
                
                // A slot or chunk always covers its assigned segment(s). 
                float fStart = segIdx * lastStepDistance;
                float fEnd = fStart + curCollMult * lastStepDistance;
                fMid = (fStart + fEnd) * 0.5f;

                // Dynamic rigid sampling for non-road elements or elements with Z-offsets
                curvePoint = rigidCenters[segIdx];
                pScale = rigidScales[segIdx];
                
                // --- FIXED: RESPECT PER-ELEMENT CONSTRAINTS IN COLLIDER RIGID MODE ---
                if (!isRoadPart)
                {
                    bool hasOverrides = (forceUprightC != forcePropsUpright) || lockPX || lockPY || lockPZ;
                    if (hasOverrides)
                    {
                        float sMidT = mapper.GetTAtDistance((segIdx + 0.5f) * lastStepDistance * lengthScale + activeOffset);
                        
                        bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                        bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                        bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                        rotation = BezierSpline.GetRotationFromForward(spline, rigidRots[segIdx] * Vector3.forward, sMidT, finalLX, finalLY, finalLZ, sWindow, forceUprightC, localUp);
                    }
                    else
                    {
                        rotation = rigidRots[segIdx];
                    }
                }
                else
                {
                    rotation = rigidRots[segIdx];
                }

                if (Mathf.Abs(pOffset.z) > 0.0001f || !isRoadPart)
                {
                    float flatCenter = (segIdx + 0.5f) * lastStepDistance;
                    float shiftedSplineDist = (flatCenter + pOffset.z) * lengthScale + activeOffset;
                    float shiftedT = mapper.GetTAtDistance(shiftedSplineDist);
                    
                    Vector3 wShiftedPos = spline.transform.TransformPoint(spline.GetPoint(shiftedT));
                    curvePoint = transform.InverseTransformPoint(wShiftedPos);
                    pScale = spline.GetScale(shiftedT, sWindow);

                    if (!isRoadPart)
                    {
                        // Dynamic prop rotation for shifted position
                        float halfLen = 0.5f * lastStepDistance * lengthScale;
                        float sS = shiftedSplineDist - halfLen;
                        float sE = shiftedSplineDist + halfLen;
                        
                        Vector3 wS = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(sS)));
                        Vector3 wE = spline.transform.TransformPoint(spline.GetPoint(mapper.GetTAtDistance(sE)));
                        Vector3 wChord = (wE - wS);
                        if (wChord.sqrMagnitude < 0.0001f) wChord = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, shiftedT, mapper, smoothBendStrength));
                        Vector3 lChord = transform.InverseTransformDirection(wChord.normalized);
                        
                        bool finalLX = lockRotationX || lockPropsRotationX || lockPX;
                        bool finalLY = lockRotationY || lockPropsRotationY || lockPY;
                        bool finalLZ = lockRotationZ || lockPropsRotationZ || lockPZ;

                        rotation = BezierSpline.GetRotationFromForward(spline, lChord, shiftedT, finalLX, finalLY, finalLZ, sWindow, forceUprightC, localUp);
                    }
                }
                
                // --- APPLY MANUAL ROTATION ---
                if (rOffset.sqrMagnitude > 0.0001f) rotation *= Quaternion.Euler(rOffset);
                
                float flatCenterDist = (cachedCollFlatCenters != null && cachedCollFlatCenters.Length > i) ? cachedCollFlatCenters[i] : fMid;
                float localZ = pt.z - flatCenterDist;
                // Longitudinal shift is now handled by curve sampling, not localOffset!
                localOffset = new Vector3(pt.x + pOffset.x, pt.y + pOffset.y, localZ);
            }

            Vector3 finalDeformedPt = curvePoint + rotation * Vector3.Scale(localOffset, pScale);
            if (float.IsNaN(finalDeformedPt.x) || float.IsInfinity(finalDeformedPt.x)) finalDeformedPt = curvePoint;
            
            deformedVerts[i] = finalDeformedPt;
        }

        generatedColliderMesh.Clear();
        if (deformedVerts.Length > 65535) generatedColliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        generatedColliderMesh.vertices = deformedVerts;
        
        if (serCollSubmeshes != null)
        {
            generatedColliderMesh.subMeshCount = serCollSubmeshes.Length;
            for (int s = 0; s < serCollSubmeshes.Length; s++)
                generatedColliderMesh.SetTriangles(serCollSubmeshes[s].triangles, s);
        }
    }


    private void BuildLUT(float curveLength, BezierSpline.CurveDistanceMapper mapper)
    {
        if (lutPos == null || lutPos.Length != lutResolution)
        {
            lutPos = new Vector3[lutResolution];
            lutRot = new Quaternion[lutResolution];
            lutScale = new Vector3[lutResolution];
        }

        for (int i = 0; i < lutResolution; i++)
        {
            float dist = (i / (float)(lutResolution - 1)) * curveLength;
            float t = mapper.GetTAtDistance(dist);

            Vector3 wPos = spline.transform.TransformPoint(spline.GetPoint(t));
            lutPos[i] = transform.InverseTransformPoint(wPos);

            Vector3 wDir = spline.transform.TransformDirection(BezierSpline.GetDirectionForT(spline, t, mapper, smoothBendStrength));
            if (wDir.sqrMagnitude < 0.0001f) wDir = Vector3.forward;
            Vector3 lDir = transform.InverseTransformDirection(wDir);
            if (lDir.sqrMagnitude < 0.0001f) lDir = Vector3.forward;

            lutRot[i] = BezierSpline.GetRotationFromForward(spline, lDir, t, lockRotationX, lockRotationY, lockRotationZ, Mathf.Max(0.001f, smoothBendStrength * 0.2f));
            lutScale[i] = spline.GetScale(t, Mathf.Max(0.001f, smoothBendStrength * 0.2f));
        }
        lutValid = true;
    }

    public bool materialsDirty = true;
    private List<int[]> segmentRemaps = new List<int[]>();

    private void RebuildStaticMesh()
    {
        staticMeshDirty = false;
        materialsDirty = false;
        segmentRemaps.Clear();

        // Drop last rebuild's densified meshes; they are regenerated lazily below.
        ClearSubdivCache();

        // --- OPTIMIZATION: SEGMENT MAPPING ---
        // Instead of searching the mixedMeshes list for EVERY segment (O(S * E)), 
        // we pre-calculate a map (O(S + E)).
        if (segmentToElementMap == null || segmentToElementMap.Length != segmentCount)
            segmentToElementMap = new int[segmentCount];
            
        for (int s = 0; s < segmentCount; s++) segmentToElementMap[s] = -1;

        if (useMixedMeshes && mixedMeshes != null)
        {
            // Primary mapping (replaces base road)
            for (int eIdx = 0; eIdx < mixedMeshes.Length; eIdx++)
            {
                var e = mixedMeshes[eIdx];
                if (e.colliderMode == ColliderDeformation.Prop) continue; // Props don't replace primary
                
                int start = Mathf.Max(0, e.startIndex);
                int end = Mathf.Min(segmentCount - 1, e.endIndex);
                for (int s = start; s <= end; s++)
                {
                    if (e.mode == SourceMode.Mesh && e.mesh != null) segmentToElementMap[s] = eIdx;
                    else if (e.mode == SourceMode.Prefab && e.prefab != null) segmentToElementMap[s] = eIdx;
                }
            }
        }

        Mesh bm = sourceMesh; Material[] materialsToUse = materials;
        if (sourceMode == SourceMode.Prefab && sourcePrefab != null)
        {
            CombinePrefab(sourcePrefab);
            bm = combinedPrefabMesh; materialsToUse = combinedPrefabMaterials;
        }

        if (bm == null && useMixedMeshes && mixedMeshes != null)
        {
            for (int eIdx = 0; eIdx < mixedMeshes.Length; eIdx++)
            {
                if (mixedMeshes[eIdx].mode == SourceMode.Mesh && mixedMeshes[eIdx].mesh != null) { bm = mixedMeshes[eIdx].mesh; break; }
                else if (mixedMeshes[eIdx].mode == SourceMode.Prefab && mixedMeshes[eIdx].prefab != null) {
                    CombinePrefab(mixedMeshes[eIdx].prefab);
                    bm = combinedPrefabMesh; break;
                }
            }
        }

        if (bm == null) 
        {
            // If we have no source mesh AND no mixed meshes, clear caches so Deform knows to skip
            if (cachedStaticMesh != null) cachedStaticMesh.Clear();
            if (cachedCollFlatVerts != null) cachedCollFlatVerts = null;
            if (serCollSubmeshes != null) serCollSubmeshes = null;
            return;
        }
        
        // --- EMERGENCY SAFETY CLAMPS ---
        // Hard ceiling for the un-subdivided array (a genuine explosion, unrelated to subdivision).
        if (bm != null && (long)segmentCount * bm.vertexCount > 1500000) {
            Debug.LogError("[EasyLine] Mesh explosion! " + ((long)segmentCount * bm.vertexCount) + " vertices. Lower Segment Count. Aborting rebuild.");
            staticMeshDirty = true; // force retry later
            return;
        }

        // Highest subdivision level requested across the active config.
        int maxSubLevel = subdivisions;
        if (useMixedMeshes && mixedMeshes != null)
        {
            maxSubLevel = 0;
            foreach (var e in mixedMeshes) if (e != null && e.subdivisions > maxSubLevel) maxSubLevel = e.subdivisions;
        }

        // Cap subdivision so the densified visible mesh stays within budget, WITHOUT aborting the
        // rebuild (aborting would leave the previous mesh in place => looks like "nothing changed").
        // safeSubLevel is applied per-part in the visible loop below.
        int safeSubLevel = 4;
        if (bm != null && bm.vertexCount > 0)
        {
            const long vertexBudget = 1500000;
            while (safeSubLevel > 0 && (long)segmentCount * bm.vertexCount * (1L << safeSubLevel) > vertexBudget)
                safeSubLevel--;
        }
        if (maxSubLevel > safeSubLevel)
        {
            Debug.LogWarning("[EasyLine] Subdivisions limited to " + safeSubLevel + " (level " + maxSubLevel + " would exceed ~" + 1500000 + " verts). Reduce Segment Count for higher subdivision.");
        }

        Bounds mb = new Bounds(MapToStandardLocal(bm.bounds.center), MapToStandardLocal(bm.bounds.extents) * 2f);
        lastMeshLengthZ = Mathf.Abs(mb.size.z);
        lastStepDistance = Mathf.Max(0.01f, (lastMeshLengthZ * meshScale.z) + overlapOffset);
        lastMinZ = 0f; // Vertices are now normalized to 0-based start
        lastTotalFlatLength = segmentCount * lastStepDistance;

        if (cachedStaticMesh == null)
        {
            cachedStaticMesh = new Mesh();
            cachedStaticMesh.name = "Static Cache Mesh";
            cachedStaticMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        // --- VISIBLE MESH GENERATION ---
        int estimatedVerts = segmentCount * 24; // Conservative default for simple cubes
        if (bm != null) estimatedVerts = segmentCount * bm.vertexCount;
        
        // Add a safety cap for initial allocation, but allow growth
        int initialCapacity = Mathf.Min(estimatedVerts, 500000); 
        
        List<Vector3> flatVerts = new List<Vector3>(initialCapacity);
        List<Vector3> flatNormals = new List<Vector3>(initialCapacity);
        List<Vector2> flatUvs = new List<Vector2>(initialCapacity);
        
        // --- COLLIDER DATA PREP ---
        List<Vector3> collFlatVerts = new List<Vector3>();
        List<int> collVertSegmentIndices = new List<int>();
        List<float> collZMults = new List<float>();
        
        cachedVertexSegmentIndices.Clear();
        if (cachedVertexSegmentIndices.Capacity < flatVerts.Capacity) cachedVertexSegmentIndices.Capacity = flatVerts.Capacity;
        
        Dictionary<Material, List<int>> subIndices = new Dictionary<Material, List<int>>();
        Dictionary<Material, List<int>> collSubIndices = new Dictionary<Material, List<int>>();
        
        List<float> vFlatCenters_temp = new List<float>(flatVerts.Capacity);
        List<bool> vIsRoad_temp = new List<bool>(flatVerts.Capacity);
        List<int> vElementIndices_temp = new List<int>(flatVerts.Capacity); // Separation tracking
        List<float> collFlatCenters_temp = new List<float>();
        List<bool> collIsRoad_temp = new List<bool>();
        List<bool> collIsBoxProxy_temp = new List<bool>();
        List<int> collElementIndices_temp = new List<int>(); // New mapping for collider
        List<Vector2Int> snapPairs_temp = new List<Vector2Int>(); // Track vertex snap pairs for post-deform
        Material fallbackMat = null;
        
        // Optimized Spatial Hash: Only build if merging is active
        // Use a larger initial capacity for the dictionary to avoid resizes on large meshes
        Dictionary<Vector3Int, List<int>> vCells = mergeOverlappingVertices ? new Dictionary<Vector3Int, List<int>>(initialCapacity / 4) : null;
        float cS = Mathf.Max(mergeDistance, 0.001f);
        float invCS = 1f / cS;
        float mergeDistSq = mergeDistance * mergeDistance;

#if UNITY_EDITOR
        bool subdivLogged = false; // one diagnostic log per rebuild
#endif

        // --- VISIBLE MESH GENERATION ---
        for (int i = 0; i < segmentCount; i++)
        {
            // 1. Identify Elements for this segment
            int primaryElementIdx = -1;
            List<int> overlayElementIndices = new List<int>();

            if (useMixedMeshes && mixedMeshes != null)
            {
                for (int eIdx = 0; eIdx < mixedMeshes.Length; eIdx++)
                {
                    var e = mixedMeshes[eIdx];
                    if (i >= e.startIndex && i <= e.endIndex)
                    {
                        if (e.colliderMode == ColliderDeformation.Prop) {
                            overlayElementIndices.Add(eIdx);
                        } else {
                            primaryElementIdx = eIdx; // Last Road/Auto wins as primary
                        }
                    }
                }
            }

            // 2. Build the list of elements to process (Primary first, then Props)
            List<int> elementsToProcess = new List<int>();
            // Only fall back to the single source (eIdx -1) when NO element covers this segment.
            // If only Prop layer(s) cover it, render just the prop(s) - never a phantom base copy.
            if (primaryElementIdx >= 0)
            {
                elementsToProcess.Add(primaryElementIdx);
                elementsToProcess.AddRange(overlayElementIndices);
            }
            else if (overlayElementIndices.Count > 0)
            {
                elementsToProcess.AddRange(overlayElementIndices);
            }
            else
            {
                elementsToProcess.Add(-1);
            }

            foreach (int eIdx in elementsToProcess)
            {
                SplineMeshElement elementData = null;
                if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length) elementData = mixedMeshes[eIdx];

                List<MeshPart> visualParts = new List<MeshPart>();
                Material[] mats = null; ColliderDeformation deformMode;
                Mesh cmOverall = GetResultingMesh(i, eIdx, out mats, out deformMode, visualParts);
                if (cmOverall == null || visualParts.Count == 0) continue;

                Bounds elementBounds = cmOverall.bounds;
                Vector3 overStdMin = MapToStandardLocal(elementBounds.min);
                Vector3 overStdMax = MapToStandardLocal(elementBounds.max);
                float overallLenZ = Mathf.Abs(overStdMax.z - overStdMin.z);
                
                // elementIsRoadV determines if we STRETCH the whole prefab to fit the slot
                bool elementIsRoadV = IsRoadElement(cmOverall, deformMode);
                
                // --- STRETCH LOGIC ---
                // If it's a road OR stretchToIndexEnds is true, we stretch it to fill the segment perfectly
                bool shouldStretchToFitSegment = elementIsRoadV || (elementData != null && elementData.stretchToIndexEnds);
                float elementFitStretch = shouldStretchToFitSegment ? ((overallLenZ > 0.001f) ? (lastMeshLengthZ / overallLenZ) : 1f) : 1f;
                float elemZScale = (elementData != null) ? elementData.elementScale.z : 1f;

                float overallCenteringOffset = shouldStretchToFitSegment ? 0f : (lastStepDistance - (overallLenZ * elementFitStretch * meshScale.z * elemZScale)) * 0.5f;

                foreach (var part in visualParts)
                {
                    if (part.mesh == null) continue;

                    // Densify BEFORE deformation so this part bends smoothly on curves.
                    // Per-layer level in Mixed mode, otherwise the global setting (budget-capped).
                    int partSubLevel = (useMixedMeshes && elementData != null) ? elementData.subdivisions : subdivisions;
                    partSubLevel = Mathf.Min(partSubLevel, safeSubLevel);
                    Vector3 localForward = GetLocalForwardVector();
                    Vector3 forwardDir = part.localMatrix.transpose.MultiplyVector(localForward).normalized;
                    Mesh partMesh = SubdivideMesh(part.mesh, partSubLevel, forwardDir);

#if UNITY_EDITOR
                    int reqSubLevel = (useMixedMeshes && elementData != null) ? elementData.subdivisions : subdivisions;
                    if (!subdivLogged && reqSubLevel > 0)
                    {
                        subdivLogged = true;
                        Debug.Log("[EasyLine] Subdiv DIAG: requested=" + reqSubLevel + " applied=" + partSubLevel + " safeCap=" + safeSubLevel
                            + " | deformMesh=" + deformMesh + " | source '" + part.mesh.name + "' " + part.mesh.vertexCount + "v -> " + partMesh.vertexCount + "v"
                            + " | segs=" + segmentCount + " bm.verts=" + (bm != null ? bm.vertexCount : 0));
                    }
#endif

                    Vector3[] v = partMesh.vertices;
                    Vector3[] n = partMesh.normals;
                    Vector2[] u = partMesh.uv;
                    int[] remap = new int[v.Length];

                    // Granular classification: each child in a prefab can be a prop or road
                    bool isRoadV = IsRoadElement(partMesh, deformMode);
                    float partStretch = (isRoadV || shouldStretchToFitSegment) ? elementFitStretch : 1f;

                    Vector3 eScale = (elementData != null) ? elementData.elementScale : Vector3.one;
                    float fX = (elementData != null && elementData.flipX) ? -1f : 1f;
                    float fY = (elementData != null && elementData.flipY) ? -1f : 1f;
                    float fZ = (elementData != null && elementData.flipZ) ? -1f : 1f;

                    // If we are stretching to index ends, the stretch factor replaces the manual Z scale to ensure perfect fit
                    float finalZScl = shouldStretchToFitSegment ? partStretch : (eScale.z * partStretch);

                    Vector3 partScl = new Vector3(
                        meshScale.x * eScale.x * fX, 
                        meshScale.y * eScale.y * fY, 
                        meshScale.z * finalZScl * fZ
                    );
                    
                    // Position logic to match collider's real_zO
                    float partZOffset = i * lastStepDistance + overallCenteringOffset - (overStdMin.z * partScl.z);

                    // Metadata for Deform()
                    float localMid = MapToStandardLocal(elementBounds.center).z - overStdMin.z;
                    float pFlatCC = isRoadV ? (i * lastStepDistance + lastStepDistance * 0.5f) : ((i * lastStepDistance) + (localMid * partScl.z) + overallCenteringOffset);

                for (int j = 0; j < v.Length; j++)
                {
                    Vector3 localPt = part.localMatrix.MultiplyPoint3x4(v[j]);
                    Vector3 stdPt = MapToStandardLocal(localPt); 

                    // --- NEW: ROTATION OFFSET ---
                    if (elementData != null && elementData.rotationOffset.sqrMagnitude > 0.0001f)
                    {
                        Quaternion rotQ = Quaternion.Euler(elementData.rotationOffset);
                        stdPt = rotQ * stdPt;
                    }

                    Vector3 pt = Vector3.Scale(stdPt, partScl);
                    Vector3 offsetPt = new Vector3(pt.x, pt.y, pt.z + partZOffset);
                    
                    // --- SNAP-BASED MERGE (Blender-style merge by distance) ---
                    // Each segment keeps its OWN vertices. We just MOVE boundary vertices
                    // to match the neighbor segment, closing the gap without topology distortion.
                    float relZ = (stdPt.z - overStdMin.z) * partScl.z;
                    float elementLen = overallLenZ * partScl.z;
                    float seamThreshold = Mathf.Max(mergeDistance, 0.01f);
                    bool nearZSeam = (relZ < seamThreshold) || (relZ > elementLen - seamThreshold);

                    // Always add the vertex (preserve topology)
                    int newIdx = flatVerts.Count;
                    remap[j] = newIdx;
                    
                    Vector3 finalPos = offsetPt; // Default position
                    int snapTarget = -1; // Track snap pair for post-deform pass
                    
                    // If near a seam, look for the CLOSEST vertex in the previous segment and SNAP to it
                    if (mergeOverlappingVertices && vCells != null && nearZSeam && i > 0)
                    {
                        Vector3Int key = new Vector3Int(Mathf.FloorToInt(offsetPt.x * invCS), Mathf.FloorToInt(offsetPt.y * invCS), Mathf.FloorToInt(offsetPt.z * invCS));
                        float bestDistSq = mergeDistSq;
                        
                        for (int ox = -1; ox <= 1; ox++) {
                            for (int oy = -1; oy <= 1; oy++) {
                                for (int oz = -1; oz <= 1; oz++) {
                                    Vector3Int neighborKey = new Vector3Int(key.x + ox, key.y + oy, key.z + oz);
                                    if (vCells.TryGetValue(neighborKey, out List<int> p)) {
                                        for (int m = 0; m < p.Count; m++) {
                                            int e = p[m];
                                            // Only snap to PREVIOUS segment — unconditional, no type checks
                                            if (cachedVertexSegmentIndices[e] != i - 1) continue;

                                            Vector3 diff = flatVerts[e] - offsetPt;
                                            float dSq = diff.sqrMagnitude;
                                            if (dSq < bestDistSq) {
                                                bestDistSq = dSq;
                                                snapTarget = e;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (snapTarget >= 0) {
                            finalPos = flatVerts[snapTarget]; // SNAP to neighbor's position
                        }
                    }
                    
                    flatVerts.Add(finalPos); 
                    
                    // --- FLIP & ROTATE NORMALS ---
                    Vector3 norm = MapNormalToStandardLocal(part.localMatrix.MultiplyVector(n[j]));
                    if (elementData != null && elementData.rotationOffset.sqrMagnitude > 0.0001f)
                        norm = Quaternion.Euler(elementData.rotationOffset) * norm;

                    Vector3 nScl = new Vector3(Mathf.Sign(partScl.x), Mathf.Sign(partScl.y), Mathf.Sign(partScl.z));
                    flatNormals.Add(Vector3.Scale(norm, nScl).normalized);
                    flatUvs.Add(u != null && u.Length > j ? u[j] : Vector2.zero);
                    cachedVertexSegmentIndices.Add(i);
                    vFlatCenters_temp.Add(pFlatCC);
                    vIsRoad_temp.Add(isRoadV);
                    vElementIndices_temp.Add(eIdx);
                    
                    // Store snap pair for post-deform pass (to close rigid-mode curve gaps)
                    if (snapTarget >= 0) snapPairs_temp.Add(new Vector2Int(newIdx, snapTarget));
                    
                    if (vCells != null)
                    {
                        Vector3Int k2 = new Vector3Int(Mathf.FloorToInt(finalPos.x * invCS), Mathf.FloorToInt(finalPos.y * invCS), Mathf.FloorToInt(finalPos.z * invCS));
                        if (!vCells.TryGetValue(k2, out List<int> bucket)) vCells[k2] = bucket = new List<int>(4);
                        bucket.Add(newIdx);
                    }
                }
                
                segmentRemaps.Add(remap);

                // --- FULLY AUTOMATIC XOR INVERSION ---
                // 1 or 3 negative scale factors = Mirror. Mirror = Reverse Winding.
                // This covers: Flip Checkboxes, Mesh Scale, and Local Scale!
                bool invertedV = (partScl.x < 0) ^ (partScl.y < 0) ^ (partScl.z < 0);

                for (int s = 0; s < partMesh.subMeshCount; s++)
                {
                    Material mat = GetMaterialForSubmesh(part.materials, s);
                    if (mat == null)
                    {
                        if (fallbackMat == null) fallbackMat = new Material(Shader.Find("Standard"));
                        mat = fallbackMat;
                    }
                    if (!subIndices.ContainsKey(mat)) subIndices[mat] = new List<int>();
                    int[] tris = partMesh.GetTriangles(s);
                    if (invertedV)
                    {
                        for (int tIdx = 0; tIdx < tris.Length; tIdx += 3)
                        {
                            subIndices[mat].Add(remap[tris[tIdx]]);
                            subIndices[mat].Add(remap[tris[tIdx + 2]]);
                            subIndices[mat].Add(remap[tris[tIdx + 1]]);
                        }
                    }
                    else
                    {
                        foreach (int t in tris) subIndices[mat].Add(remap[t]);
                    }
                }
                }
            }
        }

        // --- COLLIDER GENERATION (Advanced) ---
        if (generateMeshCollider)
        {
            int effectiveSimplification = deformMesh ? colliderSimplification : 1;
            List<bool> vertexIsRoad = new List<bool>();
            List<MeshPart> segmentParts = new List<MeshPart>();
            List<int> vertexElementIndices = new List<int>(); // New mapping
        
            for (int i = 0; i < segmentCount; )
            {
                int chunkLen = 1;
                for (int next = 1; next < effectiveSimplification && (i + next) < segmentCount; next++)
                {
                    if (useMixedMeshes && segmentToElementMap != null && segmentToElementMap[i + next] != segmentToElementMap[i]) break;
                    chunkLen++;
                }
                float numSegs = (float)chunkLen;
                float chunkDist = numSegs * lastStepDistance;

                for (int k = i; k < i + chunkLen; k++)
                {
                    // 1. Identify Elements for this segment (Primary + Overlays)
                    int primaryEIdx = (segmentToElementMap != null && k < segmentToElementMap.Length) ? segmentToElementMap[k] : -1;
                    List<int> overlayEIndices = new List<int>();
                    if (useMixedMeshes && mixedMeshes != null)
                    {
                        for (int oIdx = 0; oIdx < mixedMeshes.Length; oIdx++)
                        {
                            var e = mixedMeshes[oIdx];
                            if (k >= e.startIndex && k <= e.endIndex && e.colliderMode == ColliderDeformation.Prop)
                                overlayEIndices.Add(oIdx);
                        }
                    }

                    List<int> collElements = new List<int>();
                    collElements.Add(primaryEIdx);
                    collElements.AddRange(overlayEIndices);

                    foreach (int eIdx in collElements)
                    {
                        segmentParts.Clear();
                        Material[] mats = null; ColliderDeformation deformMode;
                        Mesh cm = GetResultingMesh(k, eIdx, out mats, out deformMode, segmentParts);
                        if (cm == null || segmentParts.Count == 0) continue;

                        Bounds elementBounds = cm.bounds;
                        Vector3 overStdMin = MapToStandardLocal(elementBounds.min);
                        Vector3 overStdMax = MapToStandardLocal(elementBounds.max);
                        float overallLenZ = Mathf.Abs(overStdMax.z - overStdMin.z);
                        
                        SplineMeshElement elemData = (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length) ? mixedMeshes[eIdx] : null;
                        bool shouldStretchToFitSegment = IsRoadElement(cm, deformMode) || (elemData != null && elemData.stretchToIndexEnds);
                        float elementFitStretch = shouldStretchToFitSegment ? ((overallLenZ > 0.001f) ? (lastMeshLengthZ / overallLenZ) : 1f) : 1f;
                        float eScaleZ = (elemData != null) ? elemData.elementScale.z : 1f;

                        ColliderDeformation effectiveDeform = (globalColliderMode != ColliderDeformation.Auto) ? globalColliderMode : deformMode;
                        float overallCenteringOffset = shouldStretchToFitSegment ? 0f : (lastStepDistance - (overallLenZ * elementFitStretch * meshScale.z * eScaleZ)) * 0.5f;

                    foreach (var part in segmentParts)
                    {
                        if (part.mesh == null) continue;
                        Bounds b = part.mesh.bounds;
                        Vector3 stdMin = MapToStandardLocal(b.min);
                        Vector3 stdMax = MapToStandardLocal(b.max);
                        float partLenZ = Mathf.Abs(stdMax.z - stdMin.z);

                        // shouldStretch determines if the mesh is treated as a long surface that spans segments (and can be simplified/stretched across CHUNKS)
                        // REFINED: We use the smart heuristic here to protect complex props from being stretched cross-chunk, 
                        // even if they were stretched to fit the single-segment slot by elementFitStretch.
                        bool shouldStretch = IsRoadElement(part.mesh, effectiveDeform);
                        
                        // shouldDeform determines if the mesh vertices are bent along the spline (Full Deformation vs Rigid Chord Alignment)
                        // SYNC: We MUST match the visual mesh's 'deformMesh' behavior to avoid offsets on curves.
                        // If the visual mesh is bent, the collider must be bent too (even if it's a prop).
                        bool isRoadPartForColl = IsRoadElement(part.mesh, effectiveDeform);
                        
                        bool dPropsElem = deformProps;
                        if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length)
                            dPropsElem = mixedMeshes[eIdx].deformProps;

                        bool shouldDeform = deformMesh && (isRoadPartForColl || dPropsElem);

                        // DEDUPLICATION: Road meshes are added only ONCE per chunk (stretched).
                        // Props are added for EVERY segment in the chunk (anchored individually).
                        if (shouldStretch && k != i) continue;

                        float partStretch = shouldStretch ? (chunkDist / (lastMeshLengthZ * meshScale.z)) : 1f;
                        
                        Vector3 eScale = (elemData != null) ? elemData.elementScale : Vector3.one;
                        float fX = (elemData != null && elemData.flipX) ? -1f : 1f;
                        float fY = (elemData != null && elemData.flipY) ? -1f : 1f;
                        float fZ = (elemData != null && elemData.flipZ) ? -1f : 1f;

                        // Stretch factor MUST multiply elementFitStretch and manual Z scale (unless overridden by stretchToIndexEnds)
                        float finalZScl = shouldStretchToFitSegment ? (partStretch * elementFitStretch) : (partStretch * elementFitStretch * eScale.z);

                        Vector3 partScl = new Vector3(
                            meshScale.x * eScale.x * fX, 
                            meshScale.y * eScale.y * fY, 
                            meshScale.z * finalZScl * fZ
                        );

                        // Use the overall element's centering offset and min bound to correctly anchor all children relative to each other
                        float real_zO = (k * lastStepDistance) + overallCenteringOffset - (overStdMin.z * partScl.z);

                        // Anchor Metadata for DeformCollider
                        float localMid = MapToStandardLocal(elementBounds.center).z - overStdMin.z;
                        float pFlatCC = shouldStretch ? (i * lastStepDistance + chunkDist * 0.5f) : ((k * lastStepDistance) + (localMid * partScl.z) + overallCenteringOffset);


                        Vector3[] v = part.mesh.vertices;
                        int[] tris = null; bool isBox = false;

                        if (simplifyPropsAsBoxes && !shouldStretch)
                        {
                            bool allowSimplification = (elemData == null || elemData.allowBoxSimplification);
                            if (allowSimplification)
                            {
                                isBox = true;
                                // Box vertices in mesh local space. Transformation will be applied in the loop below.
                            v = new Vector3[] {
                                new Vector3(b.min.x, b.min.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z),
                                new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.min.x, b.max.y, b.min.z),
                                new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.max.z),
                                new Vector3(b.max.x, b.max.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z)
                            };
                            
                            tris = new int[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 1,2,6, 1,6,5, 2,3,7, 2,7,6, 3,0,4, 3,4,7 };
                            }
                        }

                        int[] collRemap = new int[v.Length];
                        for (int j = 0; j < v.Length; j++)
                        {
                            // FIX: Apply localMatrix to correctly place sub-objects inside a prefab
                            Vector3 localPt = part.localMatrix.MultiplyPoint3x4(v[j]);
                            Vector3 stdPt = MapToStandardLocal(localPt);

                            // Match visual rotation
                            if (elemData != null && elemData.rotationOffset.sqrMagnitude > 0.0001f)
                                stdPt = Quaternion.Euler(elemData.rotationOffset) * stdPt;

                            Vector3 collPt = Vector3.Scale(stdPt, partScl);
                            Vector3 offsetPt = new Vector3(collPt.x, collPt.y, collPt.z + real_zO);
                            
                            collRemap[j] = collFlatVerts.Count;
                            collFlatVerts.Add(offsetPt);
                            collElementIndices_temp.Add(eIdx);
                            collVertSegmentIndices.Add(k);
                            
                            // 1000f is the "Enable Full Deformation" flag for DeformCollider
                            float zMultVal = shouldStretch ? numSegs : 1f;
                            if (shouldDeform) zMultVal += 1000f;
                            collZMults.Add(zMultVal);

                            collFlatCenters_temp.Add(pFlatCC);
                            collIsRoad_temp.Add(shouldStretch);
                            collIsBoxProxy_temp.Add(isBox);
                        }

                        // Automatic XOR inversion for the collider
                        bool invertedC = (partScl.x < 0) ^ (partScl.y < 0) ^ (partScl.z < 0);
                        Material mat0 = GetMaterialForSubmesh(part.materials, 0); if (mat0 == null) mat0 = fallbackMat;
                        if (!collSubIndices.ContainsKey(mat0)) collSubIndices[mat0] = new List<int>();

                        if (isBox)
                        {
                            if (invertedC)
                            {
                                for (int tIdx = 0; tIdx < tris.Length; tIdx += 3)
                                {
                                    collSubIndices[mat0].Add(collRemap[tris[tIdx]]);
                                    collSubIndices[mat0].Add(collRemap[tris[tIdx + 2]]);
                                    collSubIndices[mat0].Add(collRemap[tris[tIdx + 1]]);
                                }
                            }
                            else foreach (int t in tris) collSubIndices[mat0].Add(collRemap[t]);
                        }
                        else
                        {
                            for (int s = 0; s < part.mesh.subMeshCount; s++)
                            {
                                Material mat = GetMaterialForSubmesh(part.materials, s); if (mat == null) mat = fallbackMat;
                                if (!collSubIndices.ContainsKey(mat)) collSubIndices[mat] = new List<int>();
                                int[] tArr = part.mesh.GetTriangles(s);
                                if (invertedC)
                                {
                                    for (int tIdx = 0; tIdx < tArr.Length; tIdx += 3)
                                    {
                                        collSubIndices[mat].Add(collRemap[tArr[tIdx]]);
                                        collSubIndices[mat].Add(collRemap[tArr[tIdx + 2]]);
                                        collSubIndices[mat].Add(collRemap[tArr[tIdx + 1]]);
                                    }
                                }
                                else foreach (int t in tArr) collSubIndices[mat].Add(collRemap[t]);
                            }
                        }
                    }
                }
            }
            i += chunkLen;
        }
    }

        
        if (cachedStaticMesh == null)
        {
            cachedStaticMesh = new Mesh();
            cachedStaticMesh.name = "Static Cache";
            cachedStaticMesh.hideFlags = HideFlags.HideAndDontSave;
        }
        cachedStaticMesh.Clear();
        if (flatVerts.Count > 65535) cachedStaticMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        cachedStaticMesh.vertices = flatVerts.ToArray();
        cachedStaticMesh.normals = flatNormals.ToArray();
        cachedStaticMesh.uv = flatUvs.ToArray();
        
        cachedStaticMesh.subMeshCount = subIndices.Count;
        cachedStaticMaterials = new Material[subIndices.Count];
        int sIdx = 0;
        foreach (var kv in subIndices) 
        {
            cachedStaticMaterials[sIdx] = kv.Key;
            cachedStaticMesh.SetTriangles(kv.Value.ToArray(), sIdx++);
        }

        cachedVertexFlatCenters = vFlatCenters_temp.ToArray();
        cachedVertexIsRoad = vIsRoad_temp.ToArray();

        // Collider Cache
        cachedCollFlatVerts = collFlatVerts.ToArray();
        cachedCollVertSegmentIndices = collVertSegmentIndices.ToArray();
        cachedCollZMults = collZMults.ToArray();
        cachedCollMaterials = new Material[collSubIndices.Count];
        int csIdx = 0;
        List<SerializedSubmesh> serCols = new List<SerializedSubmesh>();
        foreach (var kv in collSubIndices) 
        {
            serCols.Add(new SerializedSubmesh { triangles = kv.Value.ToArray(), material = kv.Key });
            cachedCollMaterials[csIdx++] = kv.Key;
        }
        serCollSubmeshes = serCols.ToArray();
        cachedCollFlatCenters = collFlatCenters_temp.ToArray();
        cachedCollIsRoad = collIsRoad_temp.ToArray();
        cachedCollIsBoxProxy = collIsBoxProxy_temp.ToArray();
        // --- NORMAL SMOOTHING GROUPS ---
        sharedNormalGroups = new List<int[]>();
        if (smoothNormals)
        {
            float cellSize = 0.02f;
            Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
            
            for (int i = 0; i < flatVerts.Count; i++)
            {
                Vector3 pt = flatVerts[i];
                if (spline.loop && pt.z > lastTotalFlatLength - 0.01f) pt.z -= lastTotalFlatLength;
                
                Vector3Int k = new Vector3Int(
                    Mathf.RoundToInt(pt.x / cellSize),
                    Mathf.RoundToInt(pt.y / cellSize),
                    Mathf.RoundToInt(pt.z / cellSize)
                );
                if (!cells.TryGetValue(k, out List<int> list)) {
                    list = new List<int>();
                    cells[k] = list;
                }
                list.Add(i);
            }

            foreach (var list in cells.Values)
            {
                if (list.Count < 2) continue;
                if (list.Count > 1000) continue; // Safety skip for extreme density clusters
                
                bool[] processed = new bool[list.Count];
                int totalCellIterations = 0;

                for (int i = 0; i < list.Count; i++)
                {
                    if (processed[i]) continue;
                    List<int> group = new List<int>();
                    group.Add(list[i]);
                    processed[i] = true;
                    
                    Vector3 pA = flatVerts[list[i]];
                    Vector3 nA = flatNormals[list[i]];

                    for (int j = i + 1; j < list.Count; j++)
                    {
                        if (processed[j]) continue;
                        if (++totalCellIterations > 5000) break; // Hard limit per cell to prevent Inspector lockup

                        Vector3 pB = flatVerts[list[j]];
                        float dx = Mathf.Abs(pA.x - pB.x);
                        if (dx > 0.005f) continue;
                        float dy = Mathf.Abs(pA.y - pB.y);
                        if (dy > 0.005f) continue;
                        
                        float dz = Mathf.Abs(pA.z - pB.z);
                        if (spline.loop && dz > lastTotalFlatLength * 0.5f) dz = Mathf.Abs(dz - lastTotalFlatLength);
                        
                        if (dz < 0.005f)
                        {
                            if (Vector3.Dot(nA, flatNormals[list[j]]) > 0.95f)
                            {
                                group.Add(list[j]);
                                processed[j] = true;
                            }
                        }
                    }
                    if (group.Count > 1) sharedNormalGroups.Add(group.ToArray());
                    if (totalCellIterations > 5000) break;
                }
            }
        }


        // --- ASSIGN SUBMESH TRIANGLES ALREADY DONE IN VISIBLE PASS ---

        // --- COMMIT METADATA ARRAYS ---
        cachedVertexFlatCenters = vFlatCenters_temp.ToArray();
        cachedVertexIsRoad = vIsRoad_temp.ToArray();
        cachedVertexElementIndex = vElementIndices_temp.ToArray();
        
        cachedCollFlatCenters = collFlatCenters_temp.ToArray();
        cachedCollIsRoad = collIsRoad_temp.ToArray();
        cachedCollIsBoxProxy = collIsBoxProxy_temp.ToArray();
        cachedCollVertElementIndex = collElementIndices_temp.ToArray();
        cachedSnapPairs = snapPairs_temp.ToArray();

        BakeToSerialization();
    }

    private void RebuildMaterialsOnly()
    {
        materialsDirty = false;
        if (cachedStaticMesh == null || segmentRemaps.Count == 0 || segmentToElementMap == null || segmentToElementMap.Length != segmentCount)
        {
            staticMeshDirty = true; RebuildStaticMesh(); return;
        }

        Dictionary<Material, List<int>> subIndices = new Dictionary<Material, List<int>>();
        Material fallbackMat = null;

        // Calculate safe subdivision limit
        int safeSubLevel = 4;
        Mesh bm = sourceMesh;
        if (sourceMode == SourceMode.Prefab && sourcePrefab != null)
        {
            CombinePrefab(sourcePrefab);
            bm = combinedPrefabMesh;
        }
        if (bm == null && useMixedMeshes && mixedMeshes != null)
        {
            for (int eIdx = 0; eIdx < mixedMeshes.Length; eIdx++)
            {
                if (mixedMeshes[eIdx].mode == SourceMode.Mesh && mixedMeshes[eIdx].mesh != null) { bm = mixedMeshes[eIdx].mesh; break; }
                else if (mixedMeshes[eIdx].mode == SourceMode.Prefab && mixedMeshes[eIdx].prefab != null) {
                    CombinePrefab(mixedMeshes[eIdx].prefab);
                    bm = combinedPrefabMesh; break;
                }
            }
        }
        if (bm != null && bm.vertexCount > 0)
        {
            const long vertexBudget = 1500000;
            while (safeSubLevel > 0 && (long)segmentCount * bm.vertexCount * (1L << safeSubLevel) > vertexBudget)
                safeSubLevel--;
        }

        int remapIdx = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            int primaryElementIdx = -1;
            List<int> overlayElementIndices = new List<int>();

            if (useMixedMeshes && mixedMeshes != null)
            {
                for (int eIdx = 0; eIdx < mixedMeshes.Length; eIdx++)
                {
                    var e = mixedMeshes[eIdx];
                    if (i >= e.startIndex && i <= e.endIndex)
                    {
                        if (e.colliderMode == ColliderDeformation.Prop) overlayElementIndices.Add(eIdx);
                        else primaryElementIdx = eIdx;
                    }
                }
            }

            List<int> elementsToProcess = new List<int>();
            // Only fall back to the single source (eIdx -1) when NO element covers this segment.
            // If only Prop layer(s) cover it, render just the prop(s) - never a phantom base copy.
            if (primaryElementIdx >= 0)
            {
                elementsToProcess.Add(primaryElementIdx);
                elementsToProcess.AddRange(overlayElementIndices);
            }
            else if (overlayElementIndices.Count > 0)
            {
                elementsToProcess.AddRange(overlayElementIndices);
            }
            else
            {
                elementsToProcess.Add(-1);
            }

            foreach (int eIdx in elementsToProcess)
            {
                List<MeshPart> matsParts = new List<MeshPart>();
                Material[] mats = null; ColliderDeformation deformMode;
                Mesh cmOverall = GetResultingMesh(i, eIdx, out mats, out deformMode, matsParts);
                if (cmOverall == null || matsParts.Count == 0) continue;

                foreach (var part in matsParts)
                {
                    if (remapIdx >= segmentRemaps.Count) break;
                    int[] remap = segmentRemaps[remapIdx++];

                    // --- ROBUST INVERSION LOGIC (Material Refresh Pass) ---
                    SplineMeshElement elementData = null;
                    if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length) elementData = mixedMeshes[eIdx];
                    
                    int partSubLevel = (useMixedMeshes && elementData != null) ? elementData.subdivisions : subdivisions;
                    partSubLevel = Mathf.Min(partSubLevel, safeSubLevel);
                    Vector3 localForward = GetLocalForwardVector();
                    Vector3 forwardDir = part.localMatrix.transpose.MultiplyVector(localForward).normalized;
                    Mesh partMesh = SubdivideMesh(part.mesh, partSubLevel, forwardDir);

                    float fX = (elementData != null && elementData.flipX) ? -1f : 1f;
                    float fY = (elementData != null && elementData.flipY) ? -1f : 1f;
                    float fZ = (elementData != null && elementData.flipZ) ? -1f : 1f;
                    Vector3 eScale = (elementData != null) ? elementData.elementScale : Vector3.one;
                    Vector3 pFinalScl = new Vector3(meshScale.x * fX * eScale.x, meshScale.y * fY * eScale.y, meshScale.z * fZ * eScale.z);
                    bool invertedV = (pFinalScl.x < 0) ^ (pFinalScl.y < 0) ^ (pFinalScl.z < 0);

                    for (int s = 0; s < partMesh.subMeshCount; s++)
                    {
                        Material mat = GetMaterialForSubmesh(part.materials, s);
                        if (mat == null)
                        {
                            if (fallbackMat == null) fallbackMat = new Material(Shader.Find("Standard"));
                            mat = fallbackMat;
                        }
                        if (!subIndices.ContainsKey(mat)) subIndices[mat] = new List<int>();
                        int[] tris = partMesh.GetTriangles(s);
                        if (invertedV)
                        {
                            for (int tIdx = 0; tIdx < tris.Length; tIdx += 3)
                            {
                                subIndices[mat].Add(remap[tris[tIdx]]);
                                subIndices[mat].Add(remap[tris[tIdx + 2]]);
                                subIndices[mat].Add(remap[tris[tIdx + 1]]);
                            }
                        }
                        else
                        {
                            foreach (int t in tris) subIndices[mat].Add(remap[t]);
                        }
                    }
                }
            }
        }

        cachedStaticMesh.subMeshCount = subIndices.Count;
        cachedStaticMaterials = new Material[subIndices.Count];
        int subIdx = 0;
        foreach (var kv in subIndices) 
        {
            cachedStaticMaterials[subIdx] = kv.Key;
            cachedStaticMesh.SetTriangles(kv.Value.ToArray(), subIdx++);
        }
    }


    private void BakeToSerialization()
    {
        if (cachedStaticMesh == null) return;
        
        // --- OPTIMIZATION: Skip serialization for large meshes to fix Undo/Performance issues ---
        // Large arrays (10k+ verts) in serialized fields cause extreme Unity Inspector/Undo lag.
        // We rely on 'RebuildStaticMesh' to recreate them on load if they are not serialized.
        if (cachedStaticMesh.vertexCount > 10000)
        {
            serVerts = null;
            serNormals = null;
            serUvs = null;
            serSegmentIndices = null;
            serVertexElementIndices = null;
            serVertexFlatCenters = null;
            serVertexIsRoad = null;
            return;
        }

        serVerts = cachedStaticMesh.vertices;
        serNormals = cachedStaticMesh.normals;
        serUvs = cachedStaticMesh.uv;
        serSegmentIndices = cachedVertexSegmentIndices.ToArray();
        serLastStepDist = lastStepDistance;
        serLastMeshLen = lastMeshLengthZ;
        serLastMinZ = lastMinZ;
        serLastTotalLen = lastTotalFlatLength;

        serSubmeshes = new SerializedSubmesh[cachedStaticMesh.subMeshCount];
        for (int i = 0; i < serSubmeshes.Length; i++)
        {
            serSubmeshes[i].triangles = cachedStaticMesh.GetTriangles(i);
            serSubmeshes[i].material = (i < cachedStaticMaterials.Length) ? cachedStaticMaterials[i] : null;
        }

        // Bake Collider Data
        if (cachedCollFlatVerts != null && cachedCollFlatVerts.Length > 10000)
        {
            serCollVerts = null;
            serCollVertSegmentIndices = null;
            serCollZMults = null;
            serHasColliderBake = false;
        }
        else
        {
            serCollVerts = cachedCollFlatVerts;
            serCollVertSegmentIndices = cachedCollVertSegmentIndices;
            serCollZMults = cachedCollZMults;
            serHasColliderBake = serCollVerts != null && serCollVerts.Length > 0;
        }
        
        serVertexElementIndices = cachedVertexElementIndex;
        serCollVertElementIndices = cachedCollVertElementIndex;

        if (sharedNormalGroups != null)
        {
            serNormalGroups = new SerializedNormalGroup[sharedNormalGroups.Count];
            for (int i = 0; i < sharedNormalGroups.Count; i++)
            {
                serNormalGroups[i] = new SerializedNormalGroup { indices = sharedNormalGroups[i] };
            }
        }

        serVertexFlatCenters = cachedVertexFlatCenters;
        serVertexIsRoad = ArrayToFloat(cachedVertexIsRoad);
        serCollFlatCenters = cachedCollFlatCenters;
        serCollIsRoad = ArrayToFloat(cachedCollIsRoad);
        serCollIsBoxProxy = ArrayToFloat(cachedCollIsBoxProxy);
    }

    private void HydrateStaticFromSerialized()
    {
        if (serVerts == null || serVerts.Length == 0) return;

        if (cachedStaticMesh == null)
        {
            cachedStaticMesh = new Mesh();
            cachedStaticMesh.name = "Hydrated Static Cache";
            cachedStaticMesh.hideFlags = HideFlags.HideAndDontSave;
        }

        cachedStaticMesh.Clear();
        if (serVerts.Length > 65535) cachedStaticMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        cachedStaticMesh.vertices = serVerts;
        cachedStaticMesh.normals = serNormals;
        cachedStaticMesh.uv = serUvs;
        
        cachedVertexSegmentIndices = new List<int>(serSegmentIndices);
        lastStepDistance = serLastStepDist;
        lastMeshLengthZ = serLastMeshLen;
        lastMinZ = serLastMinZ;
        lastTotalFlatLength = serLastTotalLen;

        cachedStaticMesh.subMeshCount = serSubmeshes.Length;
        cachedStaticMaterials = new Material[serSubmeshes.Length];
        for (int i = 0; i < serSubmeshes.Length; i++)
        {
            cachedStaticMesh.SetTriangles(serSubmeshes[i].triangles, i);
            cachedStaticMaterials[i] = serSubmeshes[i].material;
        }

        // Hydrate Collider
        if (serHasColliderBake)
        {
            cachedCollFlatVerts = serCollVerts;
            cachedCollVertSegmentIndices = serCollVertSegmentIndices;
            cachedCollZMults = serCollZMults;
        }

        if (serNormalGroups != null)
        {
            sharedNormalGroups = new List<int[]>();
            foreach (var group in serNormalGroups) sharedNormalGroups.Add(group.indices);
        }

        cachedVertexFlatCenters = serVertexFlatCenters;
        cachedVertexIsRoad = FloatToBool(serVertexIsRoad);
        cachedCollFlatCenters = serCollFlatCenters;
        cachedCollIsRoad = FloatToBool(serCollIsRoad);
        cachedCollIsBoxProxy = FloatToBool(serCollIsBoxProxy);

        staticMeshDirty = false;
    }

    private double lastColliderRequestTime = -1;
    private void RequestColliderUpdate()
    {
        lastColliderRequestTime = UnityEditor.EditorApplication.timeSinceStartup;
    }


    

    private void UpdateCollider()
    {
        if (generateMeshCollider)
        {
            MeshCollider mc = GetComponent<MeshCollider>();
            if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
            
            Mesh meshToAssign = (generatedColliderMesh != null && generatedColliderMesh.vertexCount > 0) ? generatedColliderMesh : generatedMesh;

            // PERFORMANCE: Re-assigning sharedMesh (even to the same mesh) 
            // is the only way to trigger a PhysX bake in the editor.
            // We do it here because UpdateCollider is only called when 
            // physics actually needs a refresh (non-animating or first build).
            mc.sharedMesh = null;
            mc.sharedMesh = meshToAssign;
            
            // Helpful for Editor visualization:
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(mc);
        }
    }

    private bool IsRoadElement(Mesh m, ColliderDeformation mode)
    {
        if (mode == ColliderDeformation.Road) return true;
        if (mode == ColliderDeformation.Prop) return false;
        if (m == null) return true;

        string mn = m.name.ToLower();
        bool isPropKeyword = mn.Contains("lamp") || mn.Contains("post") || mn.Contains("pillar") || mn.Contains("support") || mn.Contains("stand") || mn.Contains("tube") || mn.Contains("rurk") || mn.Contains("prop");
        if (isPropKeyword) return false;

        bool isExplicitRoad = mn.Contains("road") || mn.Contains("asphalt") || mn.Contains("track") || mn.Contains("path") || mn.Contains("lane") || mn.Contains("nawierzchnia") || mn.Contains("jezdnia") ||
                              mn.Contains("bridge") || mn.Contains("platform") || mn.Contains("walkway") || mn.Contains("sidewalk") || mn.Contains("pavement") || mn.Contains("most") || mn.Contains("chodnik") || mn.Contains("peron");
        if (isExplicitRoad) return true;

        // Geometric Heuristic: A road is typically long (>85% of standard segment) and wider than it is tall
        Bounds b = m.bounds;
        float partLenZ = Mathf.Abs(MapToStandardLocal(b.size).z);
        bool looksLikeRoad = (partLenZ > lastMeshLengthZ * 0.85f) && (b.size.x > b.size.y * 0.6f);
        
        return looksLikeRoad;
    }

    private Material GetMaterialForSubmesh(Material[] mats, int submeshIndex)
    {
        if (mats != null && mats.Length > 0)
        {
            return (submeshIndex < mats.Length) ? mats[submeshIndex] : mats[mats.Length - 1];
        }
        return null;
    }
    private Mesh GetResultingMesh(int segmentIndex, int eIdx, out Material[] mats, out ColliderDeformation deformMode, List<MeshPart> outParts = null)
    {
        deformMode = ColliderDeformation.Auto;
        if (useMixedMeshes && mixedMeshes != null && eIdx >= 0 && eIdx < mixedMeshes.Length)
        {
            var e = mixedMeshes[eIdx];
            deformMode = e.colliderMode;
            if (e.mode == SourceMode.Mesh && e.mesh != null)
            {
                mats = (e.materials != null && e.materials.Length > 0) ? e.materials : materials;
                if (outParts != null)
                {
                    outParts.Add(new MeshPart { mesh = e.mesh, localMatrix = Matrix4x4.identity, materials = mats });
                }
                return e.mesh;
            }
            else if (e.mode == SourceMode.Prefab && e.prefab != null)
            {
                if (elementMeshCache == null) elementMeshCache = new Dictionary<GameObject, Mesh>();
                if (elementMatCache == null) elementMatCache = new Dictionary<GameObject, Material[]>();

                if (elementMeshCache.TryGetValue(e.prefab, out Mesh cachedM) && cachedM != null)
                {
                    mats = (e.materials != null && e.materials.Length > 0) ? e.materials : elementMatCache[e.prefab];
                    if (outParts != null) CombinePrefab(e.prefab, outParts); 
                    return cachedM;
                }

                CombinePrefabToCache(e.prefab, out Mesh combined, out Material[] combinedMats, outParts);
                elementMeshCache[e.prefab] = combined;
                elementMatCache[e.prefab] = combinedMats;
                
                mats = (e.materials != null && e.materials.Length > 0) ? e.materials : combinedMats;
                return combined;
            }
        }

        mats = materials;
        if (sourceMode == SourceMode.Prefab && sourcePrefab != null)
        {
            if (combinedPrefabMesh != null && lastCombinedPrefab == sourcePrefab)
            {
                if (outParts != null) CombinePrefab(sourcePrefab, outParts);
                mats = combinedPrefabMaterials; return combinedPrefabMesh;
            }
            CombinePrefab(sourcePrefab, outParts);
            mats = combinedPrefabMaterials; return combinedPrefabMesh;
        }
        
        if (outParts != null && sourceMesh != null)
        {
            outParts.Add(new MeshPart { mesh = sourceMesh, localMatrix = Matrix4x4.identity, materials = materials });
        }
        return sourceMesh;
    }

    private void CombinePrefabToCache(GameObject prefab, out Mesh combinedMesh, out Material[] combinedMaterials, List<MeshPart> outParts = null)
    {
        // Internal helper to avoid modifying global prefab cache
        CombinePrefab(prefab, outParts);
        combinedMesh = combinedPrefabMesh;
        combinedMaterials = combinedPrefabMaterials;
    }

    private void MarkCachesDirty()
    {
        if (elementMeshCache != null)
        {
            foreach (var m in elementMeshCache.Values)
            {
                if (m != null && m.name.Contains("Combined")) DestroyImmediate(m);
            }
            elementMeshCache.Clear();
        }
        elementMatCache?.Clear();
    }

    private void CombinePrefab(GameObject prefab, List<MeshPart> outParts = null)
    {
        MeshFilter[] mfs = prefab.GetComponentsInChildren<MeshFilter>();
        if (mfs.Length == 0) return;

        // Optimized Shortcut: If the prefab has only one MeshFilter at its root, use it directly.
        // This avoids expensive Mesh.CombineMeshes calls for simple objects.
        if (mfs.Length == 1)
        {
            MeshFilter singleMf = mfs[0];
            // ProBuilder objects rebuild their geometry from the ProBuilderMesh component, so the
            // resolver returns a freshly compiled mesh when sharedMesh is null/non-readable.
            Mesh singleMesh = ProBuilderSupport.ResolveRenderMesh(singleMf);
            bool singleIsPb = ProBuilderSupport.IsProBuilderBacked(singleMf);
            if (singleMesh != null && singleMf.transform == prefab.transform)
            {
            // OPTIMIZATION: Check readability without triggering a heavy re-import loop.
            // ProBuilder-compiled meshes are always readable, so the check is skipped for them.
            if (!singleMesh.isReadable && !singleIsPb)
            {
                if (!Application.isPlaying)
                {
                    Debug.LogWarning($"[EasyLine] Mesh '{singleMesh.name}' in Prefab '{prefab.name}' is not readable! Please click 'Fix Mesh Readability' in the Inspector.");
                }
                return;
            }

            MeshRenderer mr = singleMf.GetComponent<MeshRenderer>();
                if (mr != null && mr.enabled)
                {
                    if (outParts != null)
                    {
                        outParts.Add(new MeshPart {
                            mesh = singleMesh,
                            localMatrix = prefab.transform.worldToLocalMatrix * singleMf.transform.localToWorldMatrix,
                            materials = mr.sharedMaterials
                        });
                    }
                    combinedPrefabMesh = singleMesh;
                    combinedPrefabMaterials = mr.sharedMaterials;
                    lastCombinedPrefab = prefab;
                    return;
                }
            }
        }

        Dictionary<Material, List<CombineInstance>> matToCI = new Dictionary<Material, List<CombineInstance>>();
        List<Material> newMaterials = new List<Material>();

        foreach (var mf in mfs)
        {
            // Resolve through the ProBuilder bridge: returns the plain sharedMesh for normal
            // objects, or a compiled readable mesh for ProBuilder objects with no baked mesh.
            Mesh srcMesh = ProBuilderSupport.ResolveRenderMesh(mf);
            if (srcMesh == null) continue;
            bool isPb = ProBuilderSupport.IsProBuilderBacked(mf);

            // OPTIMIZATION: Check readability without triggering a heavy re-import loop.
            // ProBuilder-compiled meshes are always readable, so the check is skipped for them.
            if (!srcMesh.isReadable && !isPb)
            {
                if (!Application.isPlaying)
                {
                    Debug.LogWarning($"[EasyLine] Mesh '{srcMesh.name}' in Prefab '{prefab.name}' is not readable! Please click 'Fix Mesh Readability' in the Inspector.");
                }
                continue;
            }
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled) continue;

            if (outParts != null)
            {
                outParts.Add(new MeshPart {
                    mesh = srcMesh,
                    localMatrix = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix,
                    materials = mr.sharedMaterials
                });
            }

            Material[] sm = mr.sharedMaterials;
            for (int s = 0; s < srcMesh.subMeshCount; s++)
            {
                Material m = (s < sm.Length) ? sm[s] : null;
                if (m == null) continue;

                if (!matToCI.ContainsKey(m)) matToCI[m] = new List<CombineInstance>();
                CombineInstance ci = new CombineInstance(); ci.mesh = srcMesh; ci.subMeshIndex = s;
                ci.transform = GetRelativeMatrix(prefab.transform, mf.transform);
                matToCI[m].Add(ci);
            }
        }
        if (matToCI.Count == 0) return;
        List<Mesh> submeshes = new List<Mesh>(); List<Material> finalMats = new List<Material>();
        foreach (var kv in matToCI)
        {
            Mesh subm = new Mesh(); subm.CombineMeshes(kv.Value.ToArray(), true, true);
            submeshes.Add(subm); finalMats.Add(kv.Key);
        }
        CombineInstance[] finalCombine = new CombineInstance[submeshes.Count];
        for (int i = 0; i < submeshes.Count; i++) { finalCombine[i].mesh = submeshes[i]; finalCombine[i].transform = Matrix4x4.identity; }
        combinedPrefabMesh = new Mesh(); combinedPrefabMesh.name = "Combined Prefab Mesh";
        combinedPrefabMesh.CombineMeshes(finalCombine, false, false);
        combinedPrefabMaterials = finalMats.ToArray(); lastCombinedPrefab = prefab;
    }

    private Matrix4x4 GetRelativeMatrix(Transform root, Transform target)
    {
        if (target == root) return Matrix4x4.identity;
        return GetRelativeMatrix(root, target.parent) * Matrix4x4.TRS(target.localPosition, target.localRotation, target.localScale);
    }

    // --- PRE-DEFORMATION SUBDIVISION ---
    // Midpoint (1->4) subdivision densifies the source mesh so it bends smoothly along the curve.
    // It keeps the exact same silhouette/bounds, so all sizing/stretch logic stays valid. Results
    // are cached per static rebuild because the same layer mesh is reused across many segments.

    private void ClearSubdivCache()
    {
        if (subdivCache == null) { subdivCache = new Dictionary<(Mesh, int, Vector3), Mesh>(); return; }
        foreach (var m in subdivCache.Values)
        {
            if (m == null) continue;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        }
        subdivCache.Clear();
    }

    private Mesh SubdivideMesh(Mesh src, int level, Vector3 forwardDir)
    {
        if (src == null) return null;
        level = Mathf.Clamp(level, 0, 4);
        if (level <= 0) return src;

        if (subdivCache == null) subdivCache = new Dictionary<(Mesh, int, Vector3), Mesh>();
        var key = (src, level, forwardDir);
        if (subdivCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        // Find min and max Z projections of the source mesh to define bounds
        Vector3[] verts = src.vertices;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        for (int i = 0; i < verts.Length; i++)
        {
            float z = Vector3.Dot(verts[i], forwardDir);
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        float length = maxZ - minZ;
        if (length < 0.001f) return src;

        // Generate cutting planes (equidistant intervals)
        int numCuts = (1 << level) - 1; // e.g. level 1 -> 1 cut, level 2 -> 3 cuts, level 3 -> 7 cuts
        List<float> cutPlanes = new List<float>(numCuts);
        for (int k = 1; k <= numCuts; k++)
        {
            cutPlanes.Add(minZ + (k / (float)(numCuts + 1)) * length);
        }

        Mesh current = src;
        for (int i = 0; i < cutPlanes.Count; i++)
        {
            Mesh next = SliceMeshByPlane(current, cutPlanes[i], forwardDir);
            if (current != src)
            {
                if (Application.isPlaying) Destroy(current); else DestroyImmediate(current);
            }
            current = next;
        }

        current.hideFlags = HideFlags.HideAndDontSave;
        current.name = src.name + "_Subdiv" + level;
        subdivCache[key] = current;
        return current;
    }

    private static Mesh SliceMeshByPlane(Mesh m, float zCut, Vector3 forwardDir)
    {
        Vector3[] verts = m.vertices;
        Vector3[] norms = m.normals;
        Vector2[] uvs = m.uv;
        bool hasN = norms != null && norms.Length == verts.Length;
        bool hasU = uvs != null && uvs.Length == verts.Length;

        List<Vector3> newVerts = new List<Vector3>(verts);
        List<Vector3> newNorms = hasN ? new List<Vector3>(norms) : null;
        List<Vector2> newUvs = hasU ? new List<Vector2>(uvs) : null;

        // Cache for split vertices on edges to avoid duplicates and cracks
        Dictionary<long, int> splitCache = new Dictionary<long, int>();

        int GetSplitVertex(int a, int b)
        {
            long ek = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (splitCache.TryGetValue(ek, out int idx)) return idx;

            float da = Vector3.Dot(verts[a], forwardDir);
            float db = Vector3.Dot(verts[b], forwardDir);
            float t = (zCut - da) / (db - da);
            t = Mathf.Clamp01(t);

            idx = newVerts.Count;
            newVerts.Add(Vector3.Lerp(verts[a], verts[b], t));
            if (hasN) newNorms.Add(Vector3.Normalize(Vector3.Lerp(norms[a], norms[b], t)));
            if (hasU) newUvs.Add(Vector2.Lerp(uvs[a], uvs[b], t));

            splitCache[ek] = idx;
            return idx;
        }

        int subMeshCount = m.subMeshCount;
        int[][] newTris = new int[subMeshCount][];

        for (int s = 0; s < subMeshCount; s++)
        {
            int[] tris = m.GetTriangles(s);
            List<int> outTris = new List<int>(tris.Length * 2);

            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];

                float da = Vector3.Dot(verts[a], forwardDir);
                float db = Vector3.Dot(verts[b], forwardDir);
                float dc = Vector3.Dot(verts[c], forwardDir);

                // Check which edges cross the plane
                bool crossAB = (da - zCut) * (db - zCut) < -0.00001f;
                bool crossBC = (db - zCut) * (dc - zCut) < -0.00001f;
                bool crossCA = (dc - zCut) * (da - zCut) < -0.00001f;

                int caseVal = 0;
                if (crossAB) caseVal |= 1;
                if (crossBC) caseVal |= 2;
                if (crossCA) caseVal |= 4;

                if (caseVal == 3) // AB and BC split
                {
                    int ab = GetSplitVertex(a, b);
                    int bc = GetSplitVertex(b, c);
                    outTris.Add(b); outTris.Add(bc); outTris.Add(ab);
                    outTris.Add(bc); outTris.Add(c); outTris.Add(a);
                    outTris.Add(bc); outTris.Add(a); outTris.Add(ab);
                }
                else if (caseVal == 6) // BC and CA split
                {
                    int bc = GetSplitVertex(b, c);
                    int ca = GetSplitVertex(c, a);
                    outTris.Add(c); outTris.Add(ca); outTris.Add(bc);
                    outTris.Add(ca); outTris.Add(a); outTris.Add(b);
                    outTris.Add(ca); outTris.Add(b); outTris.Add(bc);
                }
                else if (caseVal == 5) // CA and AB split
                {
                    int ca = GetSplitVertex(c, a);
                    int ab = GetSplitVertex(a, b);
                    outTris.Add(a); outTris.Add(ab); outTris.Add(ca);
                    outTris.Add(ab); outTris.Add(b); outTris.Add(c);
                    outTris.Add(ab); outTris.Add(c); outTris.Add(ca);
                }
                else
                {
                    // No intersection
                    outTris.Add(a); outTris.Add(b); outTris.Add(c);
                }
            }
            newTris[s] = outTris.ToArray();
        }

        Mesh result = new Mesh();
        result.indexFormat = newVerts.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        result.SetVertices(newVerts);
        if (hasN) result.SetNormals(newNorms);
        if (hasU) result.SetUVs(0, newUvs);
        result.subMeshCount = subMeshCount;
        for (int s = 0; s < subMeshCount; s++) result.SetTriangles(newTris[s], s);
        if (!hasN) result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }

    private Vector3 GetLocalForwardVector()
    {
        switch (forwardAxis)
        {
            case ForwardAxis.X: return Vector3.right;
            case ForwardAxis.NegativeX: return Vector3.left;
            case ForwardAxis.Y: return Vector3.up;
            case ForwardAxis.NegativeY: return Vector3.down;
            case ForwardAxis.NegativeZ: return Vector3.back;
            case ForwardAxis.Z:
            default: return Vector3.forward;
        }
    }

    private Vector3 MapToStandardLocal(Vector3 pt) => BezierSpline.MapToStandardLocal(pt, (BezierSpline.ForwardAxis)forwardAxis);
    private Vector3 MapNormalToStandardLocal(Vector3 n) => MapToStandardLocal(n);

#if UNITY_EDITOR
    public bool HasNonReadableMeshes()
    {
        #if UNITY_EDITOR
        // Check single source
        if (sourceMode == SourceMode.Mesh && sourceMesh != null && !sourceMesh.isReadable) return true;
        if (sourceMode == SourceMode.Prefab && sourcePrefab != null)
        {
            foreach (var mf in sourcePrefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !mf.sharedMesh.isReadable && !ProBuilderSupport.IsProBuilderBacked(mf)) return true;
        }

        // Check mixed meshes
        if (useMixedMeshes && mixedMeshes != null)
        {
            foreach (var elem in mixedMeshes)
            {
                if (elem.mode == SourceMode.Mesh && elem.mesh != null && !elem.mesh.isReadable) return true;
                if (elem.mode == SourceMode.Prefab && elem.prefab != null)
                {
                    foreach (var mf in elem.prefab.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null && !mf.sharedMesh.isReadable && !ProBuilderSupport.IsProBuilderBacked(mf)) return true;
                }
            }
        }
        #endif
        return false;
    }

    [ContextMenu("Fix Non-Readable Meshes (For Play Mode)")]
    public void FixNonReadableMeshes()
    {
        #if UNITY_EDITOR
        bool changed = false;
        
        // Fix single source
        if (sourceMode == SourceMode.Mesh && sourceMesh != null)
        {
            if (FixMeshImportSetting(sourceMesh)) changed = true;
        }
        else if (sourceMode == SourceMode.Prefab && sourcePrefab != null)
        {
            foreach (var mf in sourcePrefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !ProBuilderSupport.IsProBuilderBacked(mf) && FixMeshImportSetting(mf.sharedMesh)) changed = true;
        }
        
        // Fix mixed meshes
        if (useMixedMeshes && mixedMeshes != null)
        {
            foreach (var elem in mixedMeshes)
            {
                if (elem.mode == SourceMode.Mesh && elem.mesh != null)
                {
                    if (FixMeshImportSetting(elem.mesh)) changed = true;
                }
                else if (elem.mode == SourceMode.Prefab && elem.prefab != null)
                {
                    foreach (var mf in elem.prefab.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null && !ProBuilderSupport.IsProBuilderBacked(mf) && FixMeshImportSetting(mf.sharedMesh)) changed = true;
                }
            }
        }
        
        if (changed)
        {
            Debug.Log("[EasyLine] Successfully updated import settings! Models now have Read/Write enabled.");
            staticMeshDirty = true;
            RefreshMixedMeshes(false, true);
        }
        else
        {
            Debug.Log("[EasyLine] No models found that required fixing, or everything is already enabled.");
        }
        #endif
    }
    public void ExportToOBJ()
    {
        if (generatedMesh == null || generatedMesh.vertexCount == 0)
        {
            Debug.LogWarning("[EasyLine] Nothing to export. Mesh is empty.");
            return;
        }

        string folderPath = "Assets/EasyLine/BakedAssets";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/EasyLine"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "EasyLine");
                
            UnityEditor.AssetDatabase.CreateFolder("Assets/EasyLine", "BakedAssets");
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"Export_{gameObject.name}_{timestamp}.obj";
        string filePath = System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), folderPath, fileName);
        string unityPath = System.IO.Path.Combine(folderPath, fileName);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("# EasyLine OBJ Export");
        sb.AppendLine($"# Source: {gameObject.name}");
        sb.AppendLine($"# Timestamp: {timestamp}");
        sb.AppendLine("");

        Vector3[] vertices = generatedMesh.vertices;
        Vector3[] normals = generatedMesh.normals;
        Vector2[] uvs = generatedMesh.uv;

        System.IFormatProvider ic = System.Globalization.CultureInfo.InvariantCulture;

        foreach (Vector3 v in vertices)
        {
            Vector3 wv = transform.TransformPoint(v);
            sb.AppendLine(string.Format(ic, "v {0} {1} {2}", -wv.x, wv.y, wv.z));
        }
        sb.AppendLine("");

        foreach (Vector3 n in normals)
        {
            Vector3 wn = transform.TransformDirection(n);
            sb.AppendLine(string.Format(ic, "vn {0} {1} {2}", -wn.x, wn.y, wn.z));
        }
        sb.AppendLine("");

        foreach (Vector2 uv in uvs)
        {
            sb.AppendLine(string.Format(ic, "vt {0} {1}", uv.x, uv.y));
        }

        for (int sub = 0; sub < generatedMesh.subMeshCount; sub++)
        {
            sb.AppendLine("");
            sb.AppendLine($"g Submesh_{sub}");
            sb.AppendLine($"usemtl Material_{sub}");

            int[] triangles = generatedMesh.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                // OBJ is 1-indexed. Face format: v/vt/vn
                // Flipping winding order for correct front-facing in Blender (Unity is Clockwise, OBJ is Counter-Clockwise in many importers)
                sb.AppendLine(string.Format("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", 
                    triangles[i + 2] + 1, triangles[i + 1] + 1, triangles[i] + 1));
            }
        }

        System.IO.File.WriteAllText(filePath, sb.ToString());
        UnityEditor.AssetDatabase.Refresh();
        
        Debug.Log($"[EasyLine] Successfully exported to OBJ: {unityPath}");
        UnityEditor.EditorUtility.DisplayDialog("EasyLine Export", $"Mesh exported to {unityPath}\n\nYou can now import this file into Blender.", "OK");
    }

    public void BakeToPrefab()
    {
        if (generatedMesh == null || generatedMesh.vertexCount == 0)
        {
            Debug.LogWarning("[EasyLine] Nothing to bake. Mesh is empty.");
            return;
        }

        string folderPath = "Assets/EasyLine/BakedAssets";
        if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/EasyLine"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "EasyLine");
                
            UnityEditor.AssetDatabase.CreateFolder("Assets/EasyLine", "BakedAssets");
        }

        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseName = $"Baked_{gameObject.name}_{timestamp}";
        string meshPath = System.IO.Path.Combine(folderPath, baseName + "_Mesh.asset");
        string prefabPath = System.IO.Path.Combine(folderPath, baseName + ".prefab");

        // 1. Create a persistent copy of the mesh
        Mesh bakeMesh = Instantiate(generatedMesh);
        bakeMesh.name = baseName + "_Mesh";
        UnityEditor.AssetDatabase.CreateAsset(bakeMesh, meshPath);
        
        // 2. Manage Collider Mesh if needed
        Mesh colliderAsset = null;
        if (generateMeshCollider && generatedColliderMesh != null && generatedColliderMesh != generatedMesh)
        {
            string collPath = System.IO.Path.Combine(folderPath, baseName + "_Collider.asset");
            colliderAsset = Instantiate(generatedColliderMesh);
            colliderAsset.name = baseName + "_Collider";
            UnityEditor.AssetDatabase.CreateAsset(colliderAsset, collPath);
        }

        UnityEditor.AssetDatabase.SaveAssets();

        // 3. Create a temporary GameObject to save as Prefab
        GameObject tempObj = new GameObject(baseName);
        tempObj.transform.position = Vector3.zero; // Prefabs are often kept at zero
        tempObj.transform.rotation = Quaternion.identity;
        tempObj.transform.localScale = transform.localScale;

        MeshFilter mf = tempObj.AddComponent<MeshFilter>();
        mf.sharedMesh = bakeMesh;

        MeshRenderer mr = tempObj.AddComponent<MeshRenderer>();
        mr.sharedMaterials = GetComponent<MeshRenderer>().sharedMaterials;

        if (generateMeshCollider)
        {
            MeshCollider mc = tempObj.AddComponent<MeshCollider>();
            mc.sharedMesh = colliderAsset != null ? colliderAsset : bakeMesh;
        }

        // 4. Save as Prefab
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(tempObj, prefabPath);
        
        // Cleanup temp object
        DestroyImmediate(tempObj);

        UnityEditor.AssetDatabase.Refresh();
        
        // Select the new prefab
        Object prefabAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(prefabPath);
        UnityEditor.Selection.activeObject = prefabAsset;

        Debug.Log($"[EasyLine] Successfully baked to prefab: {prefabPath}");
    }

    private bool FixMeshImportSetting(Mesh m)
    {
        if (m.isReadable) return false;
        string path = UnityEditor.AssetDatabase.GetAssetPath(m);
        if (string.IsNullOrEmpty(path)) return false;
        
        UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.ModelImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            return true;
        }
        return false;
    }
#endif

    private float[] ArrayToFloat(bool[] arr) {
        if (arr == null) return null;
        float[] f = new float[arr.Length];
        for (int i = 0; i < arr.Length; i++) f[i] = arr[i] ? 1f : 0f;
        return f;
    }
    private bool[] FloatToBool(float[] arr) {
        if (arr == null) return null;
        bool[] b = new bool[arr.Length];
        for (int i = 0; i < arr.Length; i++) b[i] = arr[i] > 0.5f;
        return b;
    }
}
}
