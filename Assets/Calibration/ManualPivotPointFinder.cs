using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Calibration
{
    /// <summary>
    /// A manual means of adjusting a transform's position, rotation, or scale in a built Unity scene. Originally intended to use this for
    /// making adjustments to wrist rotation and placement, but swapped to using the Editor for the experiment.
    /// </summary>
    public class ManualTransformAdjustment : MonoBehaviour
    {
        public Transform targetTransform;
        public Vector3 originalCameraPosition;
        public Camera researcherCamera;
        public TextMeshProUGUI displayText;

        public bool adjustmentConfirmed = false;

        public enum AdjustmentMode { Position, Rotation, Scale };
        public AdjustmentMode adjustmentMode = AdjustmentMode.Position;

        public enum Plane { YZ, XZ };
        public Plane plane = Plane.YZ;

        public enum Axis { X, Y, Z };
        public Axis axis = Axis.X;

        private float _rateLimitTime = 0.1f;
        private readonly float _hoverDistance = 0.5f;

        private bool InputRateLimiter(float rateLimit)
        {
            if (Time.time > _rateLimitTime + rateLimit)
            {
                _rateLimitTime = Time.time;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void ChangeTargetTransform(Transform newTarget)
        {
            targetTransform = newTarget;
            CameraAdjustment();
            adjustmentConfirmed = false;
        }

        private void CameraAdjustment()
        {
            if(researcherCamera == null)
            {
                return;
            }
            var researcherCameraTransform = researcherCamera.transform;
            var calibrationCubeTransform = GameObject.Find("calibration_object").transform;

            switch (adjustmentMode)
            {
                case AdjustmentMode.Position:
                    switch (plane)
                    {
                        case Plane.YZ:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.left * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.up);
                            break;
                        case Plane.XZ:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.up * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.right);
                            break;
                    }
                    break;
                case AdjustmentMode.Rotation:
                    switch (axis)
                    {
                        case Axis.X:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.right * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.right);
                            break;
                        case Axis.Y:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.up * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.up);
                            break;
                        case Axis.Z:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.forward * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.forward);
                            break;
                    }
                    break;
                case AdjustmentMode.Scale:
                    switch (axis)
                    {
                        case Axis.X:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.forward * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.right);
                            break;
                        case Axis.Y:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.left * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.up);
                            break;
                        case Axis.Z:
                            researcherCameraTransform.position = calibrationCubeTransform.position + (Vector3.left * _hoverDistance);
                            researcherCameraTransform.LookAt(calibrationCubeTransform, Vector3.forward);
                            break;
                    }
                    break;
            }
        }

        public IEnumerator AdjustTarget()
        {
            adjustmentConfirmed = false;
            originalCameraPosition = researcherCamera.transform.position;
            while (!adjustmentConfirmed)
            {
                if (Keyboard.current.upArrowKey.isPressed)
                {
                    float amount = Time.deltaTime;
                    AdjustValueUpdown(amount);
                }
                else if (Keyboard.current.downArrowKey.isPressed)
                {
                    float amount = Time.deltaTime * -1f;
                    AdjustValueUpdown(amount);
                }
                else if (Keyboard.current.leftArrowKey.isPressed)
                {
                    float amount = Time.deltaTime;
                    AdjustValueLeftRight(amount);
                }
                else if (Keyboard.current.rightArrowKey.isPressed)
                {
                    float amount = Time.deltaTime * -1f;
                    AdjustValueLeftRight(amount);
                }



                if (Keyboard.current.leftBracketKey.isPressed)
                {
                    if (InputRateLimiter(.5f))
                    {
                        CycleAdjustmentMode(out var currIndex, false);
                    }
                }

                if (Keyboard.current.rightBracketKey.isPressed)
                {
                    if (InputRateLimiter(.5f))
                    {
                        CycleAdjustmentMode(out var currIndex, true);
                    }
                }

                if (Keyboard.current.commaKey.isPressed)
                {
                    if (InputRateLimiter(.5f))
                    {
                        ChangePlaneOrAxis();
                    }
                }


                if (Keyboard.current.enterKey.isPressed)
                {
                    if (InputRateLimiter(1f))
                    {
                        adjustmentConfirmed = true;
                    }
                }

                yield return new WaitForSeconds(0);
            }
        }

        private void CycleAdjustmentMode(out int currIndex, bool isIncrement)
        {
            currIndex = isIncrement ? IncrementIndex((int)adjustmentMode) : DecrementIndex((int)adjustmentMode);
            adjustmentMode = (AdjustmentMode)currIndex;
            UpdateDisplay();
            CameraAdjustment();
        }

        private void ChangePlaneOrAxis()
        {
            switch (adjustmentMode)
            {
                case AdjustmentMode.Position:
                    plane = plane == Plane.YZ ? Plane.XZ : Plane.YZ;
                    break;
                case AdjustmentMode.Rotation:
                case AdjustmentMode.Scale:
                    var currIndex = DecrementIndex((int)axis);
                    axis = (Axis)currIndex;
                    break;
            }

            UpdateDisplay();
            CameraAdjustment();
        }

        private static int IncrementIndex(int currentIndex)
        {
            currentIndex++;
            if (currentIndex > 2)
                currentIndex = 0;
            return currentIndex;
        }
        private static int DecrementIndex(int currentIndex)
        {
            currentIndex--;
            if (currentIndex < 0)
                currentIndex = 2;
            return currentIndex;
        }

        /// <summary>
        /// Deactivates the object, resetting the camera position and rotation.
        /// </summary>
        public void Deactivate()
        {
            researcherCamera.transform.position = new Vector3(0, 2, -.5f);
            researcherCamera.transform.rotation.eulerAngles.Set(90, 0, 0);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the display text based on the current adjustment mode.
        /// </summary>
        private void UpdateDisplay()
        {
            displayText.text = adjustmentMode switch
            {
                AdjustmentMode.Position => plane switch
                {
                    Plane.YZ => "Position - YZ Plane",
                    Plane.XZ => "Position - XZ Plane",
                    _ => displayText.text
                },
                AdjustmentMode.Rotation => axis switch
                {
                    Axis.X => "Rotation - X Axis",
                    Axis.Y => "Rotation - Y Axis",
                    Axis.Z => "Rotation - Z Axis",
                    _ => displayText.text
                },
                AdjustmentMode.Scale => axis switch
                {
                    Axis.X => "Scale - X Axis",
                    Axis.Y => "Scale - Y Axis",
                    Axis.Z => "Scale - Z Axis",
                    _ => displayText.text
                },
                _ => displayText.text
            };
        }

        /// <summary>
        /// Adjusts a value based on the current adjustment mode and the keyboard input.
        /// </summary>
        /// <param name="amount">Amount to change the value</param>
        private void AdjustValueUpdown(float amount)
        {
            switch (adjustmentMode)
            {
                case AdjustmentMode.Position:
                    amount *= 0.01f;
                    switch (plane)
                    {
                        case Plane.YZ:
                            targetTransform.position = new Vector3(targetTransform.position.x, targetTransform.position.y + amount, targetTransform.position.z);
                            break;
                   
                        case Plane.XZ:
                            targetTransform.position = new Vector3(targetTransform.position.x + amount, targetTransform.position.y, targetTransform.position.z);
                            break;
                    }
                    break;
                case AdjustmentMode.Rotation:
                    amount *= 25f;
                    switch (axis)
                    {
                        case Axis.X:
                            targetTransform.eulerAngles = new Vector3(targetTransform.eulerAngles.x + amount, targetTransform.eulerAngles.y, targetTransform.eulerAngles.z);
                            break;
                        case Axis.Y:                    
                            targetTransform.eulerAngles = new Vector3(targetTransform.eulerAngles.x, targetTransform.eulerAngles.y + amount, targetTransform.eulerAngles.z);
                            break;
                        case Axis.Z:
                            targetTransform.eulerAngles = new Vector3(targetTransform.eulerAngles.x, targetTransform.eulerAngles.y, targetTransform.eulerAngles.z + amount);
                            break;
                    }
                    break;
                case AdjustmentMode.Scale:
                    amount *= 0.01f;
                    switch (axis)
                    {
                        case Axis.X:
                            targetTransform.localScale = new Vector3(targetTransform.localScale.x + amount, targetTransform.localScale.y, targetTransform.localScale.z);
                            break;
                        case Axis.Y:
                            targetTransform.localScale = new Vector3(targetTransform.localScale.x, targetTransform.localScale.y + amount, targetTransform.localScale.z);
                            break;
                        case Axis.Z:
                            targetTransform.localScale = new Vector3(targetTransform.localScale.x, targetTransform.localScale.y, targetTransform.localScale.z + amount);
                            break;
                    }
                    break;
            }
        }
        private void AdjustValueLeftRight(float amount)
        {
            switch (adjustmentMode)
            {
                case AdjustmentMode.Position:
                    amount *= 0.01f;
                    switch (plane)
                    {
                        case Plane.YZ:
                            targetTransform.localPosition = new Vector3(targetTransform.localPosition.x, targetTransform.localPosition.y, targetTransform.localPosition.z + amount);
                            break;
                        
                        case Plane.XZ:
                            targetTransform.localPosition = new Vector3(targetTransform.localPosition.x, targetTransform.localPosition.y, targetTransform.localPosition.z + amount);
                            break;
                    }
                    break;
            }
        }
    }
}