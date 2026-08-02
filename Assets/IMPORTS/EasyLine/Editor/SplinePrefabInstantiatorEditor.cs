using UnityEngine;
using UnityEditor;

namespace EasyLine
{
[CustomEditor(typeof(SplinePrefabInstantiator))]
public class SplinePrefabInstantiatorEditor : Editor
{
    private bool showAdvanced = true;

    // Serialized properties
    SerializedProperty splineProp, prefabProp;
    SerializedProperty spacingModeProp, spacingProp, instanceCountProp, globalOffsetProp;
    SerializedProperty positionOffsetProp, rotationOffsetProp, baseScaleProp;
    SerializedProperty forwardAxisProp, alignToSplineProp, stayUprightProp, deformMeshesProp, stretchToFitProp;
    SerializedProperty positionJitterProp, randomRotationProp, scaleRandomnessProp;
    SerializedProperty autoUpdateProp;

    private void OnEnable()
    {
        splineProp = serializedObject.FindProperty("spline");
        prefabProp = serializedObject.FindProperty("prefab");
        spacingModeProp = serializedObject.FindProperty("spacingMode");
        spacingProp = serializedObject.FindProperty("spacing");
        instanceCountProp = serializedObject.FindProperty("instanceCount");
        globalOffsetProp = serializedObject.FindProperty("globalOffset");
        
        positionOffsetProp = serializedObject.FindProperty("positionOffset");
        rotationOffsetProp = serializedObject.FindProperty("rotationOffset");
        baseScaleProp = serializedObject.FindProperty("baseScale");
        forwardAxisProp = serializedObject.FindProperty("forwardAxis");
        alignToSplineProp = serializedObject.FindProperty("alignToSpline");
        stayUprightProp = serializedObject.FindProperty("stayUpright");
        deformMeshesProp = serializedObject.FindProperty("deformMeshes");
        stretchToFitProp = serializedObject.FindProperty("stretchToFit");
        
        positionJitterProp = serializedObject.FindProperty("positionJitter");
        randomRotationProp = serializedObject.FindProperty("randomRotation");
        scaleRandomnessProp = serializedObject.FindProperty("scaleRandomness");
        autoUpdateProp = serializedObject.FindProperty("autoUpdate");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SplinePrefabInstantiator instantiator = (SplinePrefabInstantiator)target;

        // --- TITLE ---
        DrawHeader("EasyLine Prefab Instantiator", EasyLineEditorUI.Curve);

        // --- 1. CURVE SETUP ---
        DrawSectionHeader("Curve Setup", EasyLineEditorUI.Curve);
        EditorGUILayout.PropertyField(splineProp, new GUIContent("Spline Path", splineProp.tooltip));
        if (splineProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a BezierSpline to begin placement.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);

        // --- 2. PREFAB SETTINGS ---
        DrawSectionHeader("Prefab Settings", EasyLineEditorUI.Source);
        EditorGUILayout.PropertyField(prefabProp, new GUIContent("Source Prefab", prefabProp.tooltip));
        if (prefabProp.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign a prefab to start instantiating.", MessageType.Info);
        }

        EditorGUILayout.Space(5);

        // --- 3. SPAWN SETTINGS ---
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(spacingModeProp, new GUIContent("Spacing Mode", spacingModeProp.tooltip));
        
        if (spacingModeProp.enumValueIndex == (int)SplinePrefabInstantiator.SpacingMode.TotalCount)
        {
            EditorGUILayout.PropertyField(instanceCountProp, new GUIContent("Instance Count", instanceCountProp.tooltip));
        }
        else
        {
            EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing Distance", spacingProp.tooltip));
        }

        EditorGUILayout.PropertyField(globalOffsetProp, new GUIContent("Global Offset", globalOffsetProp.tooltip));
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Instance Transformations", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(positionOffsetProp, new GUIContent("Position Offset", positionOffsetProp.tooltip));
        EditorGUILayout.PropertyField(rotationOffsetProp, new GUIContent("Rotation Offset", rotationOffsetProp.tooltip));
        EditorGUILayout.PropertyField(baseScaleProp, new GUIContent("Base Scale", baseScaleProp.tooltip));
        EditorGUILayout.PropertyField(forwardAxisProp, new GUIContent("Forward Axis", forwardAxisProp.tooltip));

        EditorGUILayout.Space(5);

        // --- 4. ALIGNMENT & RANDOMIZATION ---
        GUI.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
        showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "Alignment & Variation");
        GUI.backgroundColor = Color.white;
        
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("Alignment", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(alignToSplineProp, new GUIContent("Align to Path", alignToSplineProp.tooltip));
            if (alignToSplineProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(stayUprightProp, new GUIContent("Stay Upright (No Roll)", stayUprightProp.tooltip));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.PropertyField(deformMeshesProp, new GUIContent("Deform Meshes (Hybrid)", deformMeshesProp.tooltip));
            if (deformMeshesProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(stretchToFitProp, new GUIContent("Smart Stretch to Fit", stretchToFitProp.tooltip));
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("Bending each instance independently. High poly prefabs may impact performance.", MessageType.Info);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Randomization", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(positionJitterProp, new GUIContent("Position Jitter", positionJitterProp.tooltip));
            EditorGUILayout.PropertyField(randomRotationProp, new GUIContent("Random Rotation", randomRotationProp.tooltip));
            EditorGUILayout.PropertyField(scaleRandomnessProp, new GUIContent("Scale Randomness", scaleRandomnessProp.tooltip));

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // --- BOTTOM TOOLS ---
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(autoUpdateProp, new GUIContent("Auto Live", autoUpdateProp.tooltip));

        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button("Spawn Now", GUILayout.Height(24)))
        {
            Undo.RecordObject(instantiator.gameObject, "Update Prefab Instances");
            instantiator.UpdateInstances();
        }
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("Clear", GUILayout.Width(60), GUILayout.Height(24)))
        {
            Undo.RecordObject(instantiator.gameObject, "Clear Prefab Instances");
            instantiator.ClearInstances();
        }
        
        EditorGUILayout.EndHorizontal();

        if (serializedObject.ApplyModifiedProperties())
        {
            if (instantiator.autoUpdate)
            {
                instantiator.UpdateInstances();
            }
        }
    }

    private void DrawHeader(string text, Color accent)
    {
        EasyLineEditorUI.TitleBar(text, accent);
    }

    private void DrawSectionHeader(string text, Color accent)
    {
        EasyLineEditorUI.SectionHeader(text, accent);
    }
}
}
