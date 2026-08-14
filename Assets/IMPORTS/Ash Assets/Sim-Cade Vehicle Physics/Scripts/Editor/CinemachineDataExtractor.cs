using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[System.Serializable]
public class CM2PrefabData
{
    public string PrefabName;
    public string VcamPath;
    
    // Core Vcam
    public string FollowTargetPath;
    public string LookAtTargetPath;
    public int Priority;
    public int StandbyUpdate;
    public int BlendHint;
    public bool InheritPosition;
    
    // Lens
    public float FieldOfView;
    public float NearClipPlane;
    public float FarClipPlane;
    public float OrthographicSize;
    public bool Orthographic;
    public float Dutch;
    public Vector2 LensShift;
    public float FocusDistance;
    public int ModeOverride;
    public int GateFit;

    // Transposer
    public bool HasTransposer;
    public int BindingMode;
    public Vector3 FollowOffset;
    public float XDamping;
    public float YDamping;
    public float ZDamping;
    public float PitchDamping;
    public float YawDamping;
    public float RollDamping;
    public int AngularDampingMode;
    public float AngularDamping;

    // Composer
    public bool HasComposer;
    public Vector3 TrackedObjectOffset;
    public float LookaheadTime;
    public float LookaheadSmoothing;
    public bool LookaheadIgnoreY;
    public float HorizontalDamping;
    public float VerticalDamping;
    public float ScreenX;
    public float ScreenY;
    public float DeadZoneWidth;
    public float DeadZoneHeight;
    public float SoftZoneWidth;
    public float SoftZoneHeight;
    public float BiasX;
    public float BiasY;
    public bool CenterOnActivate;
}

[System.Serializable]
public class CM2Database
{
    public List<CM2PrefabData> Prefabs = new List<CM2PrefabData>();
}

public class CinemachineDataExtractor : EditorWindow
{
    [MenuItem("Tools/Extract Cinemachine V2 Data to JSON")]
    public static void ExtractData()
    {
        string folderPath = "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Car Presets";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

        CM2Database database = new CM2Database();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                Component[] oldVcams = prefab.GetComponentsInChildren(System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine") ?? System.Type.GetType("Unity.Cinemachine.CinemachineVirtualCamera, Unity.Cinemachine"), true);
                
                foreach (Component oldVcam in oldVcams)
                {
                    CM2PrefabData data = new CM2PrefabData();
                    data.PrefabName = prefab.name;
                    data.VcamPath = AnimationUtility.CalculateTransformPath(oldVcam.transform, prefab.transform);

                    SerializedObject soOldVcam = new SerializedObject(oldVcam);

                    // Core
                    Transform follow = (Transform)soOldVcam.FindProperty("m_Follow")?.objectReferenceValue;
                    if (follow != null) data.FollowTargetPath = AnimationUtility.CalculateTransformPath(follow, prefab.transform);

                    Transform lookAt = (Transform)soOldVcam.FindProperty("m_LookAt")?.objectReferenceValue;
                    if (lookAt != null) data.LookAtTargetPath = AnimationUtility.CalculateTransformPath(lookAt, prefab.transform);

                    data.Priority = soOldVcam.FindProperty("m_Priority")?.intValue ?? 10;
                    data.StandbyUpdate = soOldVcam.FindProperty("m_StandbyUpdate")?.intValue ?? 2;
                    
                    SerializedProperty transitions = soOldVcam.FindProperty("m_Transitions");
                    if (transitions != null)
                    {
                        data.BlendHint = transitions.FindPropertyRelative("m_BlendHint")?.intValue ?? 0;
                        data.InheritPosition = transitions.FindPropertyRelative("m_InheritPosition")?.boolValue ?? false;
                    }

                    // Lens
                    SerializedProperty lens = soOldVcam.FindProperty("m_Lens");
                    if (lens != null)
                    {
                        data.FieldOfView = lens.FindPropertyRelative("FieldOfView")?.floatValue ?? 40f;
                        data.NearClipPlane = lens.FindPropertyRelative("NearClipPlane")?.floatValue ?? 0.1f;
                        data.FarClipPlane = lens.FindPropertyRelative("FarClipPlane")?.floatValue ?? 5000f;
                        data.Dutch = lens.FindPropertyRelative("Dutch")?.floatValue ?? 0f;
                        data.Orthographic = lens.FindPropertyRelative("Orthographic")?.boolValue ?? false;
                        data.OrthographicSize = lens.FindPropertyRelative("OrthographicSize")?.floatValue ?? 10f;
                        data.LensShift = lens.FindPropertyRelative("LensShift")?.vector2Value ?? Vector2.zero;
                        data.FocusDistance = lens.FindPropertyRelative("FocusDistance")?.floatValue ?? 10f;
                        data.GateFit = lens.FindPropertyRelative("GateFit")?.intValue ?? 2;
                        data.ModeOverride = lens.FindPropertyRelative("ModeOverride")?.intValue ?? 0;
                    }

                    // Transposer
                    Component transposer = oldVcam.gameObject.GetComponentInChildren(System.Type.GetType("Cinemachine.CinemachineTransposer, Cinemachine") ?? System.Type.GetType("Unity.Cinemachine.CinemachineTransposer, Unity.Cinemachine"), true);
                    data.HasTransposer = (transposer != null);
                    if (transposer != null)
                    {
                        SerializedObject soTransposer = new SerializedObject(transposer);
                        data.BindingMode = soTransposer.FindProperty("m_BindingMode")?.intValue ?? 1;
                        data.FollowOffset = soTransposer.FindProperty("m_FollowOffset")?.vector3Value ?? Vector3.zero;
                        data.XDamping = soTransposer.FindProperty("m_XDamping")?.floatValue ?? 1f;
                        data.YDamping = soTransposer.FindProperty("m_YDamping")?.floatValue ?? 1f;
                        data.ZDamping = soTransposer.FindProperty("m_ZDamping")?.floatValue ?? 1f;
                        data.PitchDamping = soTransposer.FindProperty("m_PitchDamping")?.floatValue ?? 0f;
                        data.YawDamping = soTransposer.FindProperty("m_YawDamping")?.floatValue ?? 0f;
                        data.RollDamping = soTransposer.FindProperty("m_RollDamping")?.floatValue ?? 0f;
                        data.AngularDampingMode = soTransposer.FindProperty("m_AngularDampingMode")?.intValue ?? 0;
                        data.AngularDamping = soTransposer.FindProperty("m_AngularDamping")?.floatValue ?? 0f;
                    }

                    // Composer
                    Component composer = oldVcam.gameObject.GetComponentInChildren(System.Type.GetType("Cinemachine.CinemachineComposer, Cinemachine") ?? System.Type.GetType("Unity.Cinemachine.CinemachineComposer, Unity.Cinemachine"), true);
                    data.HasComposer = (composer != null);
                    if (composer != null)
                    {
                        SerializedObject soComposer = new SerializedObject(composer);
                        data.TrackedObjectOffset = soComposer.FindProperty("m_TrackedObjectOffset")?.vector3Value ?? Vector3.zero;
                        data.LookaheadTime = soComposer.FindProperty("m_LookaheadTime")?.floatValue ?? 0f;
                        data.LookaheadSmoothing = soComposer.FindProperty("m_LookaheadSmoothing")?.floatValue ?? 0f;
                        data.LookaheadIgnoreY = soComposer.FindProperty("m_LookaheadIgnoreY")?.boolValue ?? false;
                        data.HorizontalDamping = soComposer.FindProperty("m_HorizontalDamping")?.floatValue ?? 0.5f;
                        data.VerticalDamping = soComposer.FindProperty("m_VerticalDamping")?.floatValue ?? 0.5f;
                        data.ScreenX = soComposer.FindProperty("m_ScreenX")?.floatValue ?? 0.5f;
                        data.ScreenY = soComposer.FindProperty("m_ScreenY")?.floatValue ?? 0.5f;
                        data.DeadZoneWidth = soComposer.FindProperty("m_DeadZoneWidth")?.floatValue ?? 0f;
                        data.DeadZoneHeight = soComposer.FindProperty("m_DeadZoneHeight")?.floatValue ?? 0f;
                        data.SoftZoneWidth = soComposer.FindProperty("m_SoftZoneWidth")?.floatValue ?? 0.8f;
                        data.SoftZoneHeight = soComposer.FindProperty("m_SoftZoneHeight")?.floatValue ?? 0.8f;
                        data.BiasX = soComposer.FindProperty("m_BiasX")?.floatValue ?? 0f;
                        data.BiasY = soComposer.FindProperty("m_BiasY")?.floatValue ?? 0f;
                        data.CenterOnActivate = soComposer.FindProperty("m_CenterOnActivate")?.boolValue ?? true;
                    }

                    database.Prefabs.Add(data);
                }
            }
        }

        string exportPath = Path.Combine(Application.dataPath, "CinemachineV2Backup.json");
        string jsonOutput = EditorJsonUtility.ToJson(database, true);
        File.WriteAllText(exportPath, jsonOutput);

        AssetDatabase.Refresh();
        Debug.Log($"Successfully extracted V2 data for {database.Prefabs.Count} cameras to {exportPath}");
    }
}
