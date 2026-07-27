using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace EasyLine
{
[CustomEditor(typeof(SplineMeshDeformer))]
public class SplineMeshDeformerEditor : Editor
{
    private bool showAdvanced = true;
    private HashSet<int> renamingIndices = new HashSet<int>();

    // Serialized properties for conditional visibility
    SerializedProperty splineProp, sourceModeProp, sourceMeshProp, sourcePrefabProp, materialsProp;
    SerializedProperty useMixedMeshesProp, mixedMeshesProp;
    SerializedProperty segmentCountProp, forwardAxisProp, overlapOffsetProp;
    SerializedProperty mergeOverlappingProp, mergeDistanceProp, smoothNormalsProp;
    SerializedProperty curveBendDensityProp, stretchToFitProp;
    SerializedProperty subdivisionsProp;
    SerializedProperty generateWorldUVsProp, worldUvScaleProp;
    SerializedProperty generateColliderProp, simplificationProp, simplifyPropsAsBoxesProp;
    SerializedProperty smoothBendProp;
    SerializedProperty autoDeformProp;
    SerializedProperty meshScaleProp;
    SerializedProperty deformMeshProp;
    SerializedProperty animateConveyorProp, conveyorSpeedProp;
    SerializedProperty fastAnimationModeProp, staticColliderWhileAnimatingProp;
    SerializedProperty lockRotXProp, lockRotYProp, lockRotZProp;
    SerializedProperty lockPropsRotXProp, lockPropsRotYProp, lockPropsRotZProp;
    SerializedProperty forcePropsUprightProp, deformPropsProp;
    SerializedProperty globalPropsPosOffsetProp;

    private void OnEnable()
    {
        splineProp = serializedObject.FindProperty("spline");
        sourceModeProp = serializedObject.FindProperty("sourceMode");
        sourceMeshProp = serializedObject.FindProperty("sourceMesh");
        sourcePrefabProp = serializedObject.FindProperty("sourcePrefab");
        materialsProp = serializedObject.FindProperty("materials");
        useMixedMeshesProp = serializedObject.FindProperty("useMixedMeshes");
        mixedMeshesProp = serializedObject.FindProperty("mixedMeshes");
        segmentCountProp = serializedObject.FindProperty("segmentCount");
        forwardAxisProp = serializedObject.FindProperty("forwardAxis");
        overlapOffsetProp = serializedObject.FindProperty("overlapOffset");
        mergeOverlappingProp = serializedObject.FindProperty("mergeOverlappingVertices");
        mergeDistanceProp = serializedObject.FindProperty("mergeDistance");
        smoothNormalsProp = serializedObject.FindProperty("smoothNormals");
        curveBendDensityProp = serializedObject.FindProperty("curveBendDensity");
        subdivisionsProp = serializedObject.FindProperty("subdivisions");
        stretchToFitProp = serializedObject.FindProperty("stretchToFitCurve");
        generateWorldUVsProp = serializedObject.FindProperty("generateWorldSpaceUVs");
        worldUvScaleProp = serializedObject.FindProperty("worldUvScale");
        generateColliderProp = serializedObject.FindProperty("generateMeshCollider");
        simplificationProp = serializedObject.FindProperty("colliderSimplification");
        smoothBendProp = serializedObject.FindProperty("smoothBendStrength");
        autoDeformProp = serializedObject.FindProperty("autoDeform");
        meshScaleProp = serializedObject.FindProperty("meshScale");
        deformMeshProp = serializedObject.FindProperty("deformMesh");
        animateConveyorProp = serializedObject.FindProperty("animateConveyor");
        conveyorSpeedProp = serializedObject.FindProperty("conveyorSpeed");
        fastAnimationModeProp = serializedObject.FindProperty("fastAnimationMode");
        staticColliderWhileAnimatingProp = serializedObject.FindProperty("staticColliderWhileAnimating");
        lockRotXProp = serializedObject.FindProperty("lockRotationX");
        lockRotYProp = serializedObject.FindProperty("lockRotationY");
        lockRotZProp = serializedObject.FindProperty("lockRotationZ");
        lockPropsRotXProp = serializedObject.FindProperty("lockPropsRotationX");
        lockPropsRotYProp = serializedObject.FindProperty("lockPropsRotationY");
        lockPropsRotZProp = serializedObject.FindProperty("lockPropsRotationZ");
        simplifyPropsAsBoxesProp = serializedObject.FindProperty("simplifyPropsAsBoxes");
        forcePropsUprightProp = serializedObject.FindProperty("forcePropsUpright");
        deformPropsProp = serializedObject.FindProperty("deformProps");
        globalPropsPosOffsetProp = serializedObject.FindProperty("globalPropsPositionOffset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SplineMeshDeformer deformer = (SplineMeshDeformer)target;

        // --- TITLE ---
        DrawHeader("EasyLine Spline Deformer", new Color(0.2f, 0.6f, 1f, 0.2f));
        
        // --- 1. CURVE SETUP ---
        DrawSectionHeader("📍 Curve Setup", new Color(0.3f, 1f, 0.4f, 0.15f));
        EditorGUILayout.PropertyField(splineProp, new GUIContent("Spline Path", splineProp.tooltip));
        if (splineProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a BezierSpline to begin deformation.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // --- 2. MESH MODE ---
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(useMixedMeshesProp, new GUIContent("Use Mixed Meshes - LAYERS", useMixedMeshesProp.tooltip));
        if (EditorGUI.EndChangeCheck() && useMixedMeshesProp.boolValue)
        {
            // Switching ON: carry the current single Mesh/Prefab source into Layer 0 so the user
            // keeps what they already had as the base layer (instead of an empty "None" element).
            SeedFirstLayerFromSingleSource(deformer);
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            deformer.RefreshMixedMeshes(true, true);
        }

        if (useMixedMeshesProp.boolValue)
        {
            DrawMixedMeshesList(deformer);
            EditorGUILayout.Space(5);
            
            // Helpful note for the user instead of a full settings block
            EditorGUILayout.HelpBox("💡 Mixed Mode Active: Every segment is defined by the list above.", MessageType.None);
        }
        else
        {
            // --- MESH SOURCE ---
            DrawSectionHeader("📦 Mesh Source", new Color(1f, 0.7f, 0.2f, 0.15f));
            
            EditorGUILayout.PropertyField(sourceModeProp, new GUIContent("Source Mode", sourceModeProp.tooltip));
            EditorGUILayout.Space(2);

            if (sourceModeProp.enumValueIndex == (int)SplineMeshDeformer.SourceMode.Mesh)
            {
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object currentObj = sourceMeshProp.objectReferenceValue;
                UnityEngine.Object newObj = EditorGUILayout.ObjectField(
                    new GUIContent("Source Mesh", sourceMeshProp.tooltip),
                    currentObj,
                    typeof(UnityEngine.Object),
                    true
                );
                if (EditorGUI.EndChangeCheck())
                {
                    if (newObj is GameObject go)
                    {
                        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                        if (mf != null)
                        {
                            Mesh extractedMesh = ProBuilderSupport.ResolveRenderMesh(mf);
                            sourceMeshProp.objectReferenceValue = extractedMesh;
                            
                            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                            if (mr != null && mr.sharedMaterials != null)
                            {
                                materialsProp.arraySize = mr.sharedMaterials.Length;
                                for (int mIdx = 0; mIdx < mr.sharedMaterials.Length; mIdx++)
                                    materialsProp.GetArrayElementAtIndex(mIdx).objectReferenceValue = mr.sharedMaterials[mIdx];
                            }
                        }
                    }
                    else
                    {
                        sourceMeshProp.objectReferenceValue = newObj as Mesh;
                    }
                    
                    serializedObject.ApplyModifiedProperties();
                    if (sourceMeshProp.objectReferenceValue != null)
                    {
                        deformer.FixNonReadableMeshes();
                        if (deformer.segmentCount == 1)
                        {
                            deformer.RecalculateSegmentCountFromMesh((Mesh)sourceMeshProp.objectReferenceValue, Vector3.one);
                        }
                        serializedObject.Update(); // Sync UI
                    }
                }
                EditorGUILayout.PropertyField(materialsProp, new GUIContent("Materials", materialsProp.tooltip), true);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(sourcePrefabProp, new GUIContent("Source Prefab", sourcePrefabProp.tooltip));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    if (sourcePrefabProp.objectReferenceValue != null)
                    {
                        // Smart Automation: Fix readability and recalculate segment count on assignment
                        deformer.FixNonReadableMeshes();
                        if (deformer.segmentCount == 1)
                        {
                            deformer.RecalculateSegmentCountFromPrefab((GameObject)sourcePrefabProp.objectReferenceValue, Vector3.one);
                        }
                        serializedObject.Update(); // Sync UI
                    }
                }
                if (sourcePrefabProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Assign a Prefab (or a ProBuilder object / prefab) to begin.", MessageType.Info);
                }
                else if (sourcePrefabProp.objectReferenceValue is GameObject pbGo && EasyLine.ProBuilderSupport.ContainsProBuilder(pbGo))
                {
                    string note = EasyLine.ProBuilderSupport.IsAvailable
                        ? "ProBuilder object detected. Geometry is rebuilt directly from the ProBuilderMesh component (works even when the prefab has no baked mesh)."
                        : "ProBuilder object detected, but the ProBuilder package was not found. Geometry may be missing on prefab assets.";
                    EditorGUILayout.HelpBox(note, EasyLine.ProBuilderSupport.IsAvailable ? MessageType.None : MessageType.Warning);
                }
            }
            
            // --- READABILITY WARNING ---
            if (deformer.HasNonReadableMeshes())
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Performance Warning", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Some meshes are not 'Readable'. This will prevent them from deforming correctly in Play Mode or on some platforms.", MessageType.Warning);
                if (GUILayout.Button("Fix Mesh Readability Now", GUILayout.Height(30)))
                {
                    deformer.FixNonReadableMeshes();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndVertical();
            }

            // --- SUBDIVISION (smoothing before deformation) ---
            EditorGUILayout.Space(3);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(subdivisionsProp, 0, 4, new GUIContent("Longitudinal Cut (subdivision)", subdivisionsProp.tooltip));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
            }
            if (subdivisionsProp.intValue > 0)
                EditorGUILayout.HelpBox("Mesh is densified ~" + (1 << subdivisionsProp.intValue) + "x along the spline axis before bending (visible mesh only). Use the lowest level that looks smooth.", MessageType.None);

            // Live diagnostic: shows the actual output mesh size so subdivision effect is visible
            // here in the Inspector (no Console needed). If this number does NOT grow when you raise
            // Subdivisions, the densification is not being applied.
            var diagMf = deformer.GetComponent<MeshFilter>();
            if (diagMf != null && diagMf.sharedMesh != null)
                EditorGUILayout.HelpBox("Output mesh: " + diagMf.sharedMesh.vertexCount + " verts. (This number should grow as you raise Subdivisions.)", MessageType.Info);
        } // End of Mesh Source

        EditorGUILayout.Space(5);

        // --- 3. ARRAY SETTINGS ---
        DrawSectionHeader("📐 Array Settings", new Color(0.8f, 0.4f, 1f, 0.15f));
        segmentCountProp.intValue = EditorGUILayout.DelayedIntField(new GUIContent("Segment Count", segmentCountProp.tooltip), segmentCountProp.intValue);
        EditorGUILayout.PropertyField(forwardAxisProp, new GUIContent("Forward Axis", forwardAxisProp.tooltip));
        EditorGUILayout.PropertyField(overlapOffsetProp, new GUIContent("Overlap Offset", overlapOffsetProp.tooltip));
        EditorGUILayout.PropertyField(meshScaleProp, new GUIContent("Mesh Scale", meshScaleProp.tooltip));
        bool oldDeform = deformMeshProp.boolValue;
        EditorGUILayout.PropertyField(deformMeshProp, new GUIContent("Deform Mesh", deformMeshProp.tooltip));
        if (deformMeshProp.boolValue != oldDeform && !deformMeshProp.boolValue)
        {
            stretchToFitProp.boolValue = false;
        }

        EditorGUILayout.Space(5);

        // --- 4. ADVANCED OPTIONS ---
        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
        showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "⚙ Advanced Options");
        GUI.backgroundColor = Color.white;
        
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);

            // Merging
            EditorGUILayout.PropertyField(mergeOverlappingProp, new GUIContent("Merge Vertices", mergeOverlappingProp.tooltip));
            if (mergeOverlappingProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(mergeDistanceProp, new GUIContent("Merge Distance", mergeDistanceProp.tooltip));
                if (mergeDistanceProp.floatValue < 0f) mergeDistanceProp.floatValue = 0f;
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.PropertyField(smoothNormalsProp, new GUIContent("Smooth Normals", smoothNormalsProp.tooltip));

            EditorGUILayout.Space(3);

            // Curve behavior
            EditorGUILayout.PropertyField(curveBendDensityProp, new GUIContent("Curve Bend Density", curveBendDensityProp.tooltip));
            EditorGUILayout.PropertyField(smoothBendProp, new GUIContent("Smooth Bend Strength", smoothBendProp.tooltip));
            bool oldStretch = stretchToFitProp.boolValue;
            EditorGUILayout.PropertyField(stretchToFitProp, new GUIContent("Stretch to Fit Curve", stretchToFitProp.tooltip));
            if (stretchToFitProp.boolValue != oldStretch && stretchToFitProp.boolValue)
            {
                deformMeshProp.boolValue = true;
            }

            EditorGUILayout.Space(3);

            // UVs
            EditorGUILayout.PropertyField(generateWorldUVsProp, new GUIContent("Generate World UVs", generateWorldUVsProp.tooltip));
            if (generateWorldUVsProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(worldUvScaleProp, new GUIContent("World UV Scale", worldUvScaleProp.tooltip));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(3);

            // --- GLOBAL CONSTRAINTS REMOVED ---
            // As requested, we now rely on the Mixed Meshes list for all granular constraints (Locks, Upright, etc.)
            // This keeps the main Inspector clean and focused on the core spline geometry.

            // Animation
            EditorGUILayout.LabelField("Animation", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(animateConveyorProp, new GUIContent("Animate Conveyor", animateConveyorProp.tooltip));
            if (animateConveyorProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(conveyorSpeedProp, new GUIContent("Conveyor Speed", conveyorSpeedProp.tooltip));
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Performance", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(fastAnimationModeProp, new GUIContent("Fast Animation Mode", fastAnimationModeProp.tooltip));
                EditorGUILayout.PropertyField(staticColliderWhileAnimatingProp, new GUIContent("Static Collider", staticColliderWhileAnimatingProp.tooltip));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(3);

            // Collider
            EditorGUILayout.LabelField("Physics", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(generateColliderProp, new GUIContent("Generate Mesh Collider", generateColliderProp.tooltip));
            if (generateColliderProp.boolValue)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.BeginDisabledGroup(!deformMeshProp.boolValue);
                if (!deformMeshProp.boolValue) simplificationProp.intValue = 1;
                simplificationProp.intValue = EditorGUILayout.IntSlider(new GUIContent("Simplification (1-20)", simplificationProp.tooltip), simplificationProp.intValue, 1, 20);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.PropertyField(simplifyPropsAsBoxesProp, new GUIContent("Simplify Props as Boxes", simplifyPropsAsBoxesProp.tooltip));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    deformer.RefreshMixedMeshes(false, true); // Force collider rebuild
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // --- BOTTOM TOOLS ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(autoDeformProp, new GUIContent("🔄 Auto Live", autoDeformProp.tooltip));
        
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("▶  Deform Now", GUILayout.Height(24)))
        {
            Undo.RecordObject(deformer.gameObject, "Deform Mesh");
            deformer.Deform();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        DrawSectionHeader("🚀 Export & Baking", new Color(1f, 0.2f, 0.5f, 0.15f));
        
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.5f, 0.2f);
        if (GUILayout.Button("📦 Bake to Prefab", GUILayout.Height(30)))
        {
            deformer.BakeToPrefab();
        }

        GUI.backgroundColor = new Color(0.7f, 0.9f, 0.3f);
        if (GUILayout.Button("💾 Export to OBJ", GUILayout.Height(30)))
        {
            deformer.ExportToOBJ();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Bake to Prefab: Saves as Unity Prefab + Mesh.\nExport to OBJ: Saves as .obj for Blender/3dsMax.", MessageType.Info);

        // Auto-deform on change
        if (serializedObject.ApplyModifiedProperties() && deformer.autoDeform)
        {
            // Optimized: only refresh mixed meshes if values actually changed, 
            // and use the throttled method to avoid freezing the Inspector.
            deformer.RefreshMixedMeshes(false);
        }
    }

    private void DrawHeader(string text, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        GUI.backgroundColor = color;
        GUI.Box(rect, "", EditorStyles.helpBox);
        GUI.backgroundColor = Color.white;
        
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 14;
        style.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
        GUI.Label(rect, text, style);
    }

    private void DrawSectionHeader(string text, Color color)
    {
        Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, color);
        
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.9f, 0.9f) : Color.black;
        style.padding.left = 5;
        GUI.Label(rect, text, style);
    }

    private void DrawMixedMeshesList(SplineMeshDeformer deformer)
    {
        EditorGUILayout.LabelField("Mesh Elements (Ranges)", EditorStyles.miniBoldLabel);
        
        for (int i = 0; i < mixedMeshesProp.arraySize; i++)
        {
            SerializedProperty element = mixedMeshesProp.GetArrayElementAtIndex(i);
            SerializedProperty nameProp = element.FindPropertyRelative("elementName");
            SerializedProperty mode = element.FindPropertyRelative("mode");
            SerializedProperty mesh = element.FindPropertyRelative("mesh");
            SerializedProperty prefab = element.FindPropertyRelative("prefab");
            SerializedProperty start = element.FindPropertyRelative("startIndex");
            SerializedProperty end = element.FindPropertyRelative("endIndex");
            SerializedProperty mats = element.FindPropertyRelative("materials");
            SerializedProperty cMode = element.FindPropertyRelative("colliderMode");

            // --- GENERATE UNIQUE COLOR FOR THIS ELEMENT ---
            float hue = (i * 0.35f) % 1.0f; // Better hue spread
            Color elementColor = Color.HSVToRGB(hue, 0.5f, EditorGUIUtility.isProSkin ? 0.45f : 0.9f);
            elementColor.a = 0.12f; // Subtle background tint
            
            GUI.backgroundColor = elementColor;
            Rect elementRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Solid tint behind all properties
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(elementRect, elementColor);
            }
            
            EditorGUILayout.BeginHorizontal();
            
            string dName = string.IsNullOrEmpty(nameProp.stringValue) ? $"Element {i}" : nameProp.stringValue;
            GUIContent lblCtx = new GUIContent("  " + dName + "  ");
            Vector2 lblSize = EditorStyles.boldLabel.CalcSize(lblCtx);
            Rect lblRect = GUILayoutUtility.GetRect(lblSize.x, lblSize.y);
            lblRect.y += 1;
            
            // Draw colored background for the name
            EditorGUI.DrawRect(lblRect, elementColor * 2.0f);
            GUI.Label(lblRect, lblCtx, EditorStyles.boldLabel);
            
            // Rename Toggle
            if (GUILayout.Button("✎", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                if (renamingIndices.Contains(i)) renamingIndices.Remove(i);
                else renamingIndices.Add(i);
            }
            
            // Validation warning
            if (start.intValue > end.intValue)
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("(Invalid Range!)", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            if (i > 0 && GUILayout.Button("▲", GUILayout.Width(20)))
            {
                mixedMeshesProp.MoveArrayElement(i, i - 1);
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
                return;
            }
            if (i < mixedMeshesProp.arraySize - 1 && GUILayout.Button("▼", GUILayout.Width(20)))
            {
                mixedMeshesProp.MoveArrayElement(i, i + 1);
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
                return;
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                mixedMeshesProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(true); 
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (renamingIndices.Contains(i))
            {
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.DelayedTextField("New Name", nameProp.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    nameProp.stringValue = newName;
                    serializedObject.ApplyModifiedProperties();
                    // Optionally close renaming after enter
                    // renamingIndices.Remove(i); 
                }
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            start.intValue = EditorGUILayout.DelayedIntField(new GUIContent("From Index"), start.intValue);
            end.intValue = EditorGUILayout.DelayedIntField(new GUIContent("To Index"), end.intValue);
            
            // --- FILL TO END BUTTON ---
            if (GUILayout.Button("⇥", EditorStyles.miniButton, GUILayout.Width(25)))
            {
                int maxIdx = (deformer.segmentCount > 0) ? deformer.segmentCount - 1 : 0;
                end.intValue = maxIdx;
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (mesh.objectReferenceValue != null || prefab.objectReferenceValue != null)
                    deformer.RefreshMixedMeshes(false, true);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(mode, new GUIContent("Mode"));
            
            if (mode.enumValueIndex == (int)SplineMeshDeformer.SourceMode.Mesh)
            {
                EditorGUI.BeginChangeCheck();
                UnityEngine.Object currentObj = mesh.objectReferenceValue;
                UnityEngine.Object newObj = EditorGUILayout.ObjectField(
                    new GUIContent("Mesh Asset"),
                    currentObj,
                    typeof(UnityEngine.Object),
                    true
                );
                if (EditorGUI.EndChangeCheck())
                {
                    if (newObj is GameObject go)
                    {
                        MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                        if (mf != null)
                        {
                            mesh.objectReferenceValue = ProBuilderSupport.ResolveRenderMesh(mf);
                            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                            if (mr != null && mr.sharedMaterials != null)
                            {
                                mats.arraySize = mr.sharedMaterials.Length;
                                for (int mIdx = 0; mIdx < mr.sharedMaterials.Length; mIdx++)
                                    mats.GetArrayElementAtIndex(mIdx).objectReferenceValue = mr.sharedMaterials[mIdx];
                            }
                        }
                    }
                    else
                    {
                        mesh.objectReferenceValue = newObj as Mesh;
                    }
                    
                    serializedObject.ApplyModifiedProperties();
                    deformer.FixNonReadableMeshes();
                    
                    if (i == 0 && deformer.segmentCount == 1 && mesh.objectReferenceValue != null)
                    {
                        deformer.RecalculateSegmentCountFromMesh((Mesh)mesh.objectReferenceValue, Vector3.one);
                    }
                    
                    serializedObject.Update();
                    deformer.RefreshMixedMeshes(true, true);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prefab, new GUIContent("Prefab Asset"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    deformer.FixNonReadableMeshes();
                    if (i == 0 && deformer.segmentCount == 1 && prefab.objectReferenceValue != null)
                    {
                        deformer.RecalculateSegmentCountFromPrefab((GameObject)prefab.objectReferenceValue, Vector3.one);
                    }
                    serializedObject.Update();
                    deformer.RefreshMixedMeshes(true, true);
                }
            }

            if (mode.enumValueIndex == (int)SplineMeshDeformer.SourceMode.Mesh)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(mats, new GUIContent("Material Overrides"), true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    deformer.RefreshMixedMeshes(false, false); // Only materials changed!
                }
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(cMode, new GUIContent("Deformation & Collider"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
            }

            // --- PER-LAYER SUBDIVISION (smoothing before deformation) ---
            SerializedProperty layerSubdiv = element.FindPropertyRelative("subdivisions");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntSlider(layerSubdiv, 0, 4, new GUIContent("Longitudinal Cut (subdivision)", layerSubdiv.tooltip));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
            }

            // --- ELEMENT CONSTRAINTS (COLLAPSIBLE) ---
            EditorGUI.BeginChangeCheck();
            SerializedProperty fUp = element.FindPropertyRelative("forceUpright");
            SerializedProperty dProps = element.FindPropertyRelative("deformProps");
            SerializedProperty lPX = element.FindPropertyRelative("lockPropX");
            SerializedProperty lPY = element.FindPropertyRelative("lockPropY");
            SerializedProperty lPZ = element.FindPropertyRelative("lockPropZ");
            SerializedProperty pOff = element.FindPropertyRelative("positionOffset");
            SerializedProperty eScale = element.FindPropertyRelative("elementScale");
            SerializedProperty fX = element.FindPropertyRelative("flipX");
            SerializedProperty fY = element.FindPropertyRelative("flipY");
            SerializedProperty fZ = element.FindPropertyRelative("flipZ");
            SerializedProperty stretch = element.FindPropertyRelative("stretchToIndexEnds");
            SerializedProperty isExp = element.FindPropertyRelative("isExpanded");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            isExp.boolValue = EditorGUILayout.Foldout(isExp.boolValue, "Element Specific Constraints", true, EditorStyles.foldoutHeader);
            
            if (isExp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(2);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(stretch, new GUIContent("Stretch To Index Ends"));
                if (EditorGUI.EndChangeCheck() && stretch.boolValue)
                {
                    // Stretching a prop to fill its slot only works if the prop is allowed to deform.
                    dProps.boolValue = true;
                }
                EditorGUILayout.PropertyField(element.FindPropertyRelative("allowBoxSimplification"), new GUIContent("Allow Box Simplification"));
                EditorGUILayout.PropertyField(eScale, new GUIContent("Local Scale"));
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Flip Axis");
                fX.boolValue = EditorGUILayout.ToggleLeft("X", fX.boolValue, GUILayout.Width(45));
                fY.boolValue = EditorGUILayout.ToggleLeft("Y", fY.boolValue, GUILayout.Width(45));
                fZ.boolValue = EditorGUILayout.ToggleLeft("Z", fZ.boolValue, GUILayout.Width(45));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(pOff, new GUIContent("Position Offset"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative("rotationOffset"), new GUIContent("Rotation Offset"));

                EditorGUILayout.Space(5);
                
                // --- CONTEXTUAL UI: Hide Rotation & Bend for Road mode ---
                if (cMode.enumValueIndex != (int)SplineMeshDeformer.ColliderDeformation.Road)
                {
                    EditorGUILayout.LabelField("Rotation & Bend", EditorStyles.miniBoldLabel);
                    EditorGUILayout.PropertyField(fUp, new GUIContent("Force 100% Upright"));
                    EditorGUILayout.PropertyField(dProps, new GUIContent("Deform Props"));
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Lock Rotation");
                    lPX.boolValue = EditorGUILayout.ToggleLeft("X", lPX.boolValue, GUILayout.Width(45));
                    lPY.boolValue = EditorGUILayout.ToggleLeft("Y", lPY.boolValue, GUILayout.Width(45));
                    lPZ.boolValue = EditorGUILayout.ToggleLeft("Z", lPZ.boolValue, GUILayout.Width(45));
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                deformer.RefreshMixedMeshes(false, true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.HelpBox("💡 Priority logic: Elements lower in the list override those above them. You can use this to 'layer' different meshes.", MessageType.Info);
        if (GUILayout.Button("🛠 Sort by Start Index", GUILayout.Height(20)))
        {
            SortMixedMeshes();
        }

        if (GUILayout.Button("+ Add Element", GUILayout.Height(22)))
        {
            int count = mixedMeshesProp.arraySize;
            int nextStart = 0;
            int effectiveCount = deformer.segmentCount;
            int maxIdx = (effectiveCount > 0) ? effectiveCount - 1 : 0;

            if (count > 0)
            {
                SerializedProperty last = mixedMeshesProp.GetArrayElementAtIndex(count - 1);
                nextStart = last.FindPropertyRelative("endIndex").intValue + 1;
            }

            if (nextStart > maxIdx)
            {
                // If the next logical start is beyond the total segments,
                // default to the last 5 segments of the existing spline.
                nextStart = Mathf.Max(0, maxIdx - 4);
            }

            mixedMeshesProp.arraySize++;
            SerializedProperty newElement = mixedMeshesProp.GetArrayElementAtIndex(count);
            
            // Explicitly reset fields for the new element to avoid Unity's duplication behavior
            newElement.FindPropertyRelative("startIndex").intValue = nextStart;
            newElement.FindPropertyRelative("endIndex").intValue = maxIdx;
            
            // Reset assets to null to avoid duplicating the previous element's mesh/prefab
            newElement.FindPropertyRelative("mesh").objectReferenceValue = null;
            newElement.FindPropertyRelative("prefab").objectReferenceValue = null;
            newElement.FindPropertyRelative("materials").arraySize = 0;
            newElement.FindPropertyRelative("elementScale").vector3Value = Vector3.one;
            newElement.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
            newElement.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
            newElement.FindPropertyRelative("flipX").boolValue = false;
            newElement.FindPropertyRelative("flipY").boolValue = false;
            newElement.FindPropertyRelative("flipZ").boolValue = false;

            serializedObject.ApplyModifiedProperties();
            deformer.RefreshMixedMeshes(false, true); // Only geometry/segment count might have shifted, but assets haven't changed!
        }
        
        if (deformer.segmentCount > 200)
        {
            EditorGUILayout.HelpBox("Warning: High segment count (>200) with mixed meshes or merging enabled may cause UI lag during editing. Consider disabling 'Auto Live' while adjusting large tracks.", MessageType.Warning);
        }
    }

    // When enabling Mixed Meshes (LAYERS), make sure Layer 0 mirrors the single source the user
    // already had set up. Only fills an empty first element - never overwrites existing layers.
    private void SeedFirstLayerFromSingleSource(SplineMeshDeformer deformer)
    {
        if (mixedMeshesProp.arraySize == 0) mixedMeshesProp.arraySize = 1;

        SerializedProperty e0 = mixedMeshesProp.GetArrayElementAtIndex(0);
        SerializedProperty meshP = e0.FindPropertyRelative("mesh");
        SerializedProperty prefabP = e0.FindPropertyRelative("prefab");

        // If Layer 0 already references something, leave the user's setup alone.
        if (meshP.objectReferenceValue != null || prefabP.objectReferenceValue != null) return;

        int maxIdx = (deformer.segmentCount > 0) ? deformer.segmentCount - 1 : 0;

        e0.FindPropertyRelative("startIndex").intValue = 0;
        e0.FindPropertyRelative("endIndex").intValue = maxIdx;
        e0.FindPropertyRelative("mode").enumValueIndex = sourceModeProp.enumValueIndex;
        e0.FindPropertyRelative("elementScale").vector3Value = Vector3.one;
        e0.FindPropertyRelative("positionOffset").vector3Value = Vector3.zero;
        e0.FindPropertyRelative("rotationOffset").vector3Value = Vector3.zero;
        e0.FindPropertyRelative("flipX").boolValue = false;
        e0.FindPropertyRelative("flipY").boolValue = false;
        e0.FindPropertyRelative("flipZ").boolValue = false;
        e0.FindPropertyRelative("subdivisions").intValue = subdivisionsProp.intValue; // carry over smoothing

        // Mode follows the single source: Prefab -> Prefab, otherwise Mesh.
        if (sourceModeProp.enumValueIndex == (int)SplineMeshDeformer.SourceMode.Prefab)
        {
            prefabP.objectReferenceValue = sourcePrefabProp.objectReferenceValue;
            meshP.objectReferenceValue = null;
        }
        else
        {
            meshP.objectReferenceValue = sourceMeshProp.objectReferenceValue;
            prefabP.objectReferenceValue = null;
        }

        // Copy the single source's materials into the layer's overrides.
        SerializedProperty dstMats = e0.FindPropertyRelative("materials");
        dstMats.arraySize = materialsProp.arraySize;
        for (int k = 0; k < materialsProp.arraySize; k++)
            dstMats.GetArrayElementAtIndex(k).objectReferenceValue = materialsProp.GetArrayElementAtIndex(k).objectReferenceValue;
    }

    private void SortMixedMeshes()
    {
        // Simple bubble sort or similar for SerializedProperty array
        for (int i = 0; i < mixedMeshesProp.arraySize - 1; i++)
        {
            for (int j = 0; j < mixedMeshesProp.arraySize - i - 1; j++)
            {
                int startA = mixedMeshesProp.GetArrayElementAtIndex(j).FindPropertyRelative("startIndex").intValue;
                int startB = mixedMeshesProp.GetArrayElementAtIndex(j + 1).FindPropertyRelative("startIndex").intValue;
                if (startA > startB)
                {
                    mixedMeshesProp.MoveArrayElement(j, j + 1);
                }
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
}
}
