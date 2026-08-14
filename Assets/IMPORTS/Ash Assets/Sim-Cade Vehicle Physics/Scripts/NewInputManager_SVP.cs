using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ashsvp
{
    public class NewInputManager_SVP : MonoBehaviour
    {
        public SimcadeVehicleController SimcadeVehicleController;

#if ENABLE_INPUT_SYSTEM
        public InputAction Accelerate;
        public InputAction Steer;
        public InputAction HandBrake;

        public float SteerInput { get; private set; }
        public float AccelerationInput { get; private set; }
        public float HandbrakeInput { get; private set; }

        public bool useInputLerping = true;
        public float AccelerationInput_IncreaseSpeed = 15;

        public float SteerInput_IncreaseSpeed = 15;
        public float SteerInput_DecreaseSpeed = 25;


        [ContextMenu("Add Default Bindings")]
        public void AddDefaultBindings()
        {
            if (Accelerate == null)
                Accelerate = new InputAction("Accelerate", InputActionType.Value);
            if (Steer == null)
                Steer = new InputAction("Steer", InputActionType.Value);
            if (HandBrake == null)
                HandBrake = new InputAction("HandBrake", InputActionType.Button);

            if (Accelerate.bindings.Count == 0)
            {
                var keyboardAccelerate = Accelerate.AddCompositeBinding("1DAxis");
                Accelerate.ChangeBinding(keyboardAccelerate.bindingIndex).WithName("Keyboard");
                keyboardAccelerate.With("Negative", "<Keyboard>/s");
                keyboardAccelerate.With("Positive", "<Keyboard>/w");
                var gamepadAccelerate = Accelerate.AddCompositeBinding("1DAxis");
                Accelerate.ChangeBinding(gamepadAccelerate.bindingIndex).WithName("Gamepad");
                gamepadAccelerate.With("Negative", "<Gamepad>/leftTrigger");
                gamepadAccelerate.With("Positive", "<Gamepad>/rightTrigger");
            }

            if (Steer.bindings.Count == 0)
            {
                var keyboardSteer = Steer.AddCompositeBinding("1DAxis");
                Steer.ChangeBinding(keyboardSteer.bindingIndex).WithName("Keyboard");
                keyboardSteer.With("Negative", "<Keyboard>/a");
                keyboardSteer.With("Positive", "<Keyboard>/d");
                var gamepadSteer = Steer.AddCompositeBinding("1DAxis");
                Steer.ChangeBinding(gamepadSteer.bindingIndex).WithName("Gamepad");
                gamepadSteer.With("Negative", "<Gamepad>/leftStick/left");
                gamepadSteer.With("Positive", "<Gamepad>/leftStick/right");
            }

            if (HandBrake.bindings.Count == 0)
            {
                HandBrake.AddBinding("<Keyboard>/space");
                HandBrake.AddBinding("<Gamepad>/buttonSouth");
            }
        }


        private void Start()
        {
            SimcadeVehicleController = GetComponent<SimcadeVehicleController>();
        }

        private void Update()
        {
            float tempSteerInput = GetSteerInput();
            float tempAccelerationInput = GetAccelerationInput();
            float tempHandbrakeInput = GetHandbrakeInput();

            if (useInputLerping)
            {
                AccelerationInput = Mathf.Abs(tempAccelerationInput) > 0 ? Mathf.Lerp(AccelerationInput, tempAccelerationInput, AccelerationInput_IncreaseSpeed * Time.deltaTime) : 0;
                SteerInput = Mathf.Abs(tempSteerInput) > 0 ? Mathf.Lerp(SteerInput, tempSteerInput, SteerInput_IncreaseSpeed * Time.deltaTime)
                    : Mathf.Lerp(SteerInput, tempSteerInput, SteerInput_DecreaseSpeed * Time.deltaTime);
            }
            else
            {
                AccelerationInput = tempAccelerationInput;
                SteerInput = tempSteerInput;
            }

            HandbrakeInput = tempHandbrakeInput;

            SimcadeVehicleController.ProvideInputs(AccelerationInput, SteerInput, HandbrakeInput);

        }

        private float GetSteerInput()
        {
            float steerInput = 0f;
            if (Steer != null)
            {
                if (!Steer.enabled)
                    Steer.Enable();

                steerInput = Mathf.Clamp(Steer.ReadValue<float>(), -1f, 1f);
            }
            return steerInput;
        }

        private float GetAccelerationInput()
        {
            float accelInput = 0f;
            if (Accelerate != null)
            {
                if (!Accelerate.enabled)
                    Accelerate.Enable();

                accelInput = Mathf.Clamp(Accelerate.ReadValue<float>(), -1f, 1f);
            }
            return accelInput;
        }

        private float GetHandbrakeInput()
        {
            float handBrakeInput = 0f;
            if (HandBrake != null)
            {
                if (!HandBrake.enabled)
                    HandBrake.Enable();

                handBrakeInput = HandBrake.IsPressed() ? 1f : 0f;
            }

            return handBrakeInput;
        }
#endif
    }
}
