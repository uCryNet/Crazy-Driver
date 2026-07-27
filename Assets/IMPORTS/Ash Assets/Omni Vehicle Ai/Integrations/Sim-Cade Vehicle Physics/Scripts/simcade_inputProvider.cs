using UnityEngine;
using Ashsvp;
using System;

namespace OmniVehicleAi
{
    public class simcade_inputProvider : MonoBehaviour
    {
        // Reference to the user's custom vehicle controller
        public SimcadeVehicleController vehicleController; // Change this to the actual vehicle controller type in your project
        public AIVehicleController aiVehicleController; // AI input provider

        // Enum to switch between Player and AI input modes
        public enum InputType { Player, Ai };
        public InputType inputType; // Control mode (Player or AI)


        [Serializable]
        public class KeyboardInput
        {
            public KeyCode steerLeft = KeyCode.A;
            public KeyCode steerRight = KeyCode.D;
            public KeyCode accelerate = KeyCode.W;
            public KeyCode decelerate = KeyCode.S;
            public KeyCode handBrake = KeyCode.Space;
        }

        public KeyboardInput keyboardInput = new KeyboardInput();

        [Serializable]
        public class MobileInput
        {
            public UiButton_SVP steerLeft;
            public UiButton_SVP steerRight;
            public UiButton_SVP accelerate;
            public UiButton_SVP decelerate;
            public UiButton_SVP handBrake;
        }

        public bool useMobileInput = false;
        public MobileInput mobileInput = new MobileInput();


        // Variables to hold input values
        public float AccelerationInput { get; private set; }
        public float SteerInput { get; private set; }
        public float HandbrakeInput { get; private set; }

        private void Update()
        {
            // Switch between input types based on the selected input type
            if (inputType == InputType.Player)
            {
                ProvidePlayerInput(); // If player-controlled, get player inputs
            }
            else
            {
                ProvideAiInput(); // If AI-controlled, get AI inputs
            }
        }

        // Function to provide AI inputs to the vehicle controller
        private void ProvideAiInput()
        {
            // Get AI inputs from AI vehicle controller
            SteerInput = aiVehicleController.GetSteerInput();
            AccelerationInput = aiVehicleController.GetAccelerationInput();
            HandbrakeInput = aiVehicleController.GetHandBrakeInput();

            float speed = aiVehicleController.LocalVehiclevelocity.z;


            if(Mathf.Abs(speed) < 5 && Mathf.Abs(AccelerationInput) > 0.1f &&  HandbrakeInput > 0.1f)
            {
                HandbrakeInput = 0;
            }


            // Provide these inputs to vehicle controller
            vehicleController.ProvideInputs(AccelerationInput, SteerInput, HandbrakeInput);
        }

        // Function to provide Player inputs to the vehicle controller
        private void ProvidePlayerInput()
        {
            float tempSteerInput = GetKeyboardSteerInput();
            float tempAccelerationInput = GetKeyboardAccelerationInput();
            float tempHandbrakeInput = GetKeyboardHandbrakeInput();

            if (useMobileInput)
            {
                tempSteerInput = GetMobileSteerInput();
                tempAccelerationInput = GetMobileAccelerationInput();
                tempHandbrakeInput = GetMobileHandbrakeInput();
            }

            AccelerationInput = Mathf.Abs(tempAccelerationInput) > 0 ? Mathf.Lerp(AccelerationInput, tempAccelerationInput, 15 * Time.deltaTime) : 0;
            SteerInput = Mathf.Abs(tempSteerInput) > 0 ? Mathf.Lerp(SteerInput, tempSteerInput, 15 * Time.deltaTime)
                : Mathf.Lerp(SteerInput, tempSteerInput, 25 * Time.deltaTime);
            HandbrakeInput = tempHandbrakeInput;

            // Provide these inputs to vehicle controller
            vehicleController.ProvideInputs(AccelerationInput, SteerInput, HandbrakeInput);
        }

        private float GetKeyboardSteerInput()
        {
            float steerInput = 0f;
            if (Input.GetKey(keyboardInput.steerLeft))
                steerInput -= 1f;
            if (Input.GetKey(keyboardInput.steerRight))
                steerInput += 1f;
            return steerInput;
        }

        private float GetKeyboardAccelerationInput()
        {
            float accelInput = 0f;
            if (Input.GetKey(keyboardInput.accelerate))
                accelInput += 1f;
            if (Input.GetKey(keyboardInput.decelerate))
                accelInput -= 1f;
            return accelInput;
        }

        private float GetKeyboardHandbrakeInput()
        {
            return Input.GetKey(keyboardInput.handBrake) ? 1f : 0f;
        }


        private float GetMobileSteerInput()
        {
            float steerInput = 0f;
            if (mobileInput.steerLeft.isPressed)
                steerInput -= 1f;
            if (mobileInput.steerRight.isPressed)
                steerInput += 1f;
            return steerInput;
        }

        private float GetMobileAccelerationInput()
        {
            float accelInput = 0f;
            if (mobileInput.accelerate.isPressed)
                accelInput += 1f;
            if (mobileInput.decelerate.isPressed)
                accelInput -= 1f;
            return accelInput;
        }

        private float GetMobileHandbrakeInput()
        {
            return mobileInput.handBrake.isPressed ? 1f : 0f;
        }

    }

}
