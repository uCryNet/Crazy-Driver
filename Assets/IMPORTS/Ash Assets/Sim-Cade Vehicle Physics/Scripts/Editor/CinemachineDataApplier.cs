using UnityEngine;
using UnityEditor;
using System.IO;
using Unity.Cinemachine;
using System.Collections.Generic;

public class CinemachineDataApplier : EditorWindow
{
    [MenuItem("Tools/Apply Cinemachine V3 Upgrades from JSON")]
    public static void ApplyData()
    {
        string jsonPath = Path.Combine(Application.dataPath, "CinemachineV2Backup.json");
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"Could not find JSON backup at {jsonPath}. Please run the extractor first.");
            return;
        }

        string jsonOutput = File.ReadAllText(jsonPath);
        CM2Database database = new CM2Database();
        EditorJsonUtility.FromJsonOverwrite(jsonOutput, database);

        Debug.Log($"Loaded {database.Prefabs.Count} prefab configurations from JSON.");

        foreach (var data in database.Prefabs)
        {
            string folderPath = "Assets/Ash Assets/Sim-Cade Vehicle Physics/Prefabs/Car Presets";
            string[] guids = AssetDatabase.FindAssets($"{data.PrefabName} t:Prefab", new[] { folderPath });
            
            if (guids.Length == 0)
            {
                Debug.LogWarning($"Could not find prefab {data.PrefabName} in {folderPath}");
                continue;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Debug.Log($"Applying V3 upgrade to prefab: {path}");

            using (var editingScope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = editingScope.prefabContentsRoot;

                // 1. Find the old Cinemachine object
                Transform vcamTransform = null;
                if (!string.IsNullOrEmpty(data.VcamPath))
                {
                    vcamTransform = root.transform.Find(data.VcamPath);
                }

                if (vcamTransform == null)
                {
                    // Fallback search by name if path fails
                    Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allTransforms)
                    {
                        if (t.name == "CM vcam1" || t.name == "CM vcam")
                        {
                            vcamTransform = t;
                            break;
                        }
                    }
                }

                if (vcamTransform == null)
                {
                    Debug.LogWarning($"Could not find Vcam inside prefab {data.PrefabName}");
                    continue;
                }

                GameObject vcamObj = vcamTransform.gameObject;

                // 2. Clear old Cinemachine components safely without destroying the object 
                //    (so we preserve Transform LocalPosition, Rotation, and any other non-cinemachine user scripts!)
                var oldComponents = vcamObj.GetComponents<MonoBehaviour>();
                foreach (var comp in oldComponents)
                {
                    if (comp != null && comp.GetType().FullName.Contains("Cinemachine"))
                    {
                        DestroyImmediate(comp);
                    }
                }

                // 3. Create the new Cinemachine object components in-place
                CinemachineCamera newCam = vcamObj.AddComponent<CinemachineCamera>();

                // 4. Resolve Transforms (Follow/LookAt)
                if (!string.IsNullOrEmpty(data.FollowTargetPath))
                {
                    newCam.Target.TrackingTarget = root.transform.Find(data.FollowTargetPath);
                }
                if (!string.IsNullOrEmpty(data.LookAtTargetPath))
                {
                    newCam.Target.LookAtTarget = root.transform.Find(data.LookAtTargetPath);
                    newCam.Target.CustomLookAtTarget = (newCam.Target.LookAtTarget != null);
                }

                // 5. Core assignments
                newCam.Priority.Value = data.Priority;
                newCam.StandbyUpdate = (CinemachineCamera.StandbyUpdateMode)data.StandbyUpdate;

                // 6. Lens assignments
                var newLens = newCam.Lens;
                newLens.FieldOfView = data.FieldOfView;
                newLens.NearClipPlane = data.NearClipPlane;
                newLens.FarClipPlane = data.FarClipPlane;
                newLens.Dutch = data.Dutch;
                newLens.ModeOverride = data.Orthographic ? LensSettings.OverrideModes.Orthographic : LensSettings.OverrideModes.None;
                newLens.OrthographicSize = data.OrthographicSize;
                newLens.PhysicalProperties.LensShift = data.LensShift;
                newCam.Lens = newLens;

                // 7. Transposer (CinemachineFollow)
                if (data.HasTransposer)
                {
                    CinemachineFollow followComp = vcamObj.AddComponent<CinemachineFollow>();
                    followComp.FollowOffset = data.FollowOffset;
                    
                    var trackerSettings = new Unity.Cinemachine.TargetTracking.TrackerSettings();
                    trackerSettings.BindingMode = (Unity.Cinemachine.TargetTracking.BindingMode)data.BindingMode; 
                    trackerSettings.PositionDamping = new Vector3(data.XDamping, data.YDamping, data.ZDamping);
                    trackerSettings.RotationDamping = new Vector3(data.PitchDamping, data.YawDamping, data.RollDamping);
                    trackerSettings.AngularDampingMode = (Unity.Cinemachine.TargetTracking.AngularDampingMode)data.AngularDampingMode;
                    trackerSettings.QuaternionDamping = data.AngularDamping;
                    followComp.TrackerSettings = trackerSettings;
                }

                // 8. Composer (CinemachineRotationComposer)
                if (data.HasComposer)
                {
                    CinemachineRotationComposer composerComp = vcamObj.AddComponent<CinemachineRotationComposer>();
                    composerComp.TargetOffset = data.TrackedObjectOffset;
                    
                    composerComp.Lookahead = new LookaheadSettings
                    {
                        Enabled = data.LookaheadTime > 0,
                        Time = data.LookaheadTime,
                        Smoothing = data.LookaheadSmoothing,
                        IgnoreY = data.LookaheadIgnoreY
                    };
                    
                    composerComp.Damping = new Vector2(data.HorizontalDamping, data.VerticalDamping);
                    
                    composerComp.Composition = new ScreenComposerSettings
                    {
                        ScreenPosition = new Vector2(data.ScreenX, data.ScreenY) - new Vector2(0.5f, 0.5f), 
                        DeadZone = new ScreenComposerSettings.DeadZoneSettings
                        { 
                            Enabled = (data.DeadZoneWidth > 0 || data.DeadZoneHeight > 0), 
                            Size = new Vector2(data.DeadZoneWidth, data.DeadZoneHeight) 
                        },
                        HardLimits = new ScreenComposerSettings.HardLimitSettings
                        {
                            Enabled = true,
                            Size = new Vector2(data.SoftZoneWidth, data.SoftZoneHeight),
                            Offset = new Vector2(data.BiasX, data.BiasY) * 2f
                        }
                    };
                    
                    composerComp.CenterOnActivate = data.CenterOnActivate;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("Successfully applied all Cinemachine V3 upgrades to prefabs from JSON!");
    }
}
