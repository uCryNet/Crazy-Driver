using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OmniVehicleAi
{
    public class SetChaseTarget : MonoBehaviour
    {
        public AIVehicleController AiVehicleController;
        public Transform DestinationTarget;
        public Vector3 TargetPosition { get; private set; }
        public Vector3 LastTargetPosition { get; private set; }

        [Tooltip("when distance to current target position from last target position is greater than this value, vehicle path will be updated. " +
            "Also, if vehicle is in this area, its mode will be switched to target follow")]
        public float pathUpdateRadius = 10f;

        public Button SetDestinationButton;
        public Button CloseDestinationViewButton;

        public Cinemachine.CinemachineVirtualCamera VehicleCamera;
        public Cinemachine.CinemachineVirtualCamera SelectDectinationCamera;


        bool destinationSelectionOpened = false;

        private void Start()
        {
            CloseDestinationSelectionView();

            SetDestinationButton.onClick.AddListener(OpenDestinationSelectionView);
            CloseDestinationViewButton.onClick.AddListener(CloseDestinationSelectionView);

            AiVehicleController.pathProgressTracker.OnLapCompleted.AddListener(SwitchToTargetFollowMode);
        }

        public void SwitchToTargetFollowMode()
        {
            AiVehicleController.switchAiMode(AIVehicleController.Ai_Mode.TargetFollow);
        }

        public void OpenDestinationSelectionView()
        {
            SelectDectinationCamera.Priority = 10;
            VehicleCamera.Priority = 0;
            SetDestinationButton.gameObject.SetActive(false);
            CloseDestinationViewButton.gameObject.SetActive(true);


            destinationSelectionOpened = true;
        }

        public void CloseDestinationSelectionView()
        {
            SelectDectinationCamera.Priority = 0;
            VehicleCamera.Priority = 10;
            SetDestinationButton.gameObject.SetActive(true);
            CloseDestinationViewButton.gameObject.SetActive(false);

            destinationSelectionOpened = false;
        }

        private void Update()
        {
            if(!destinationSelectionOpened) return;

            if (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    TargetPosition = hit.point;
                    DestinationTarget.position = TargetPosition;
                    if (Vector3.Distance(TargetPosition, LastTargetPosition) > pathUpdateRadius)
                    {
                        AiVehicleController.DriveToDestination(hit.point);
                        LastTargetPosition = TargetPosition;
                    }
                }

            }

            if(Vector3.Distance(AiVehicleController.vehicleTransform.position, TargetPosition) < pathUpdateRadius)
            {
                AiVehicleController.switchAiMode(AIVehicleController.Ai_Mode.TargetFollow);
            }
            else
            {
                AiVehicleController.switchAiMode(AIVehicleController.Ai_Mode.PathFollow);
            }

        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(LastTargetPosition, pathUpdateRadius);
        }


#if UNITY_EDITOR

        // create a simple editor for the path finding scene manager
        [CustomEditor(typeof(SetChaseTarget))]
        public class SetChaseTargetEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                // draw the default inspector
                DrawDefaultInspector();

                // add space
                EditorGUILayout.Space();

                // editing tutorial
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Instructions", EditorStyles.boldLabel);

                GUIStyle instructionStyle = new GUIStyle(EditorStyles.label);
                instructionStyle.wordWrap = true; // Ensures wrapping

                string text = "when in Set Destination mode, press left ctrl and left click on ground to make Ai drive to Destination.";
                EditorGUILayout.LabelField(text, instructionStyle);

                GUILayout.EndVertical();

            }
        }

#endif
    }
}
