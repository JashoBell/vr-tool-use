using System.Collections;
using System.Collections.Generic;
using BOLL7708;
using MovementTracking;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UXF;
using Valve.VR;
using UnityEngine.InputSystem;

namespace Calibration
{
    /// <summary>
    /// A utility class that calibrates the SteamVR universe and corrects the position/rotation of SteamVR objects using a singular SteamVR tracker placed at the origin.
    /// Position and yaw adjustments are applied directly to the universe. Pitch and roll adjustments are applied to the OpenVRUniverseOffset transform.
    /// Allows for manual adjustments as well, in case the automatic calibration is not adequate due to error with the tracker.
    /// </summary>
    public class ExperimentCalibration : MonoBehaviour
    {
        public enum CalibrationMode { OneTracker, TwoTrackers, ThreeTrackers };
        public CalibrationMode calibrationMode = CalibrationMode.OneTracker;
        public bool isAlignedPosition, isAlignedRotation;
        private bool _fixRoll, _fixPitch, _fixPlane, _threeTrackerCalibrated = false;
        public float originPositionOffset, _yawRotationalOffset, _pitchRotationalOffset, _rollRotationalOffset;
        private const float RotationOffsetTolerance = .0005f;
        private Queue<float> _orientationCorrectionsPitch;
        private Queue<float> _orientationCorrectionsRoll;
        public int _orientationCorrectionsToStore = 8;

        /// <summary>
        /// The object to use as the origin for the SteamVR/OpenVR universe. Should contain a SteamVRTrackedObjectPlus component.
        /// </summary>
        private GameObject _originTrackerObject;
        public GameObject supplementaryTracker, supplementaryTracker2;

        public string experimentalSceneName = "AvatarLab";


        /// <summary>
        /// Should be component to _originTrackerObject. Used to establish the origin of the SteamVR/OpenVR universe and address any ongoing rotational/positional error.
        /// </summary>
        private SteamVRTrackedObjectPlus _originTracker;
        /// <summary>
        /// Used to transform between SteamVR/OpenVR and Unity coordinate systems.
        /// </summary>
        private EasyOpenVRSingleton _trackingInstance;

        private Vector3 _desiredOrigin;


        /// <summary>
        /// The remaining (rotational) offset of the origin tracker after the initial calibration. Pitch and roll adjustments to the SteamVR/OpenVR universe are not supported, so
        /// this transform is used to correct for them.
        /// </summary>
        public Transform OpenVRUniverseOffset;

        // In the event that the calibration is not adequate, additional manual adjustments can be made here.
        public Vector3 autoRotationOffset, manualRotationOffset, lastRecordedRotationOffset = Vector3.zero;
        public Vector3 manualPositionOffset, lastRecordedPositionOffset = Vector3.zero;
        public Vector3 manualScaleOffset = Vector3.one, lastRecordedScaleOffset;

        public bool sessionEventAssigned = false;
        public bool coroutineEntered = false;
        
        // The trackers sat at this height (on the table)
        private const float RigHeight = .731f;  // PPT Rig: .031f Table: .7305f;
        public float OuterRigHeight = 0f; //  PPT Rig: .037f;

        // Vive tracker origin sits 1cm from the base.
        private const float TrackerHeight = .00907f;
        private const float CubeTrackerHeight = .063f;
        private const float TrackerHeightViveOne = .0086f; 
        private bool _offsetsCurrent = false;
        private bool _trackersEnabled = false;


        // Start is called before the first frame update
        void Start()
        {
            _originTrackerObject = this.gameObject;
            DontDestroyOnLoad(_originTrackerObject);
            _orientationCorrectionsPitch = new Queue<float>(_orientationCorrectionsToStore);
            _orientationCorrectionsRoll = new Queue<float>(_orientationCorrectionsToStore);
        }

        private void UpdateExperimentDetails(Trial currentTrial)
        {
            currentTrial.result["openvr_position_offset_x"] = OpenVRUniverseOffset.position.x;
            currentTrial.result["openvr_position_offset_y"] = OpenVRUniverseOffset.position.y;
            currentTrial.result["openvr_position_offset_z"] = OpenVRUniverseOffset.position.z;
            currentTrial.result["openvr_rotation_offset_x"] = OpenVRUniverseOffset.eulerAngles.x;
            currentTrial.result["openvr_rotation_offset_y"] = OpenVRUniverseOffset.eulerAngles.y;
            currentTrial.result["openvr_rotation_offset_z"] = OpenVRUniverseOffset.eulerAngles.z;
            currentTrial.result["openvr_scale_offset_x"] = OpenVRUniverseOffset.localScale.x;
            currentTrial.result["openvr_scale_offset_y"] = OpenVRUniverseOffset.localScale.y;
            currentTrial.result["openvr_scale_offset_z"] = OpenVRUniverseOffset.localScale.z;
        }

        /// <summary>
        /// Establishes the current offset of the origin tracker from its intended position at the origin. This offset is used to correct the position of SteamVR objects.
        /// </summary>
        private void GetUpdatedOffsets()
        {
            _desiredOrigin = new Vector3(_originTrackerObject.transform.position.x,
                _originTrackerObject.transform.position.y - (RigHeight + TrackerHeight),
                _originTrackerObject.transform.position.z);

            originPositionOffset = _desiredOrigin.magnitude;
            _yawRotationalOffset = Quaternion.LookRotation(_originTrackerObject.transform.TransformDirection(Vector3.up), _originTrackerObject.transform.TransformDirection(Vector3.forward)).eulerAngles.y;

            isAlignedPosition = originPositionOffset < .005;
            isAlignedRotation = _yawRotationalOffset < RotationOffsetTolerance || _yawRotationalOffset > 360 - RotationOffsetTolerance;
            _offsetsCurrent = true;

        }

        private bool CriterionAssessment()
        {
            return !isAlignedPosition || !isAlignedRotation;
        }

        /// <summary>
        /// Grabs the current tracking universe's position/rotation, compares it to what it should be, and calibrates if the deviation is large enough.
        /// </summary>
        private IEnumerator RecalibrateUniverse()
        {
            _originTracker = _originTrackerObject.GetComponent<SteamVRTrackedObjectPlus>();
            yield return new WaitForSeconds(2f);
            _trackingInstance = EasyOpenVRSingleton.Instance;
            if (!_trackingInstance.IsInitialized()) _trackingInstance.Init();
            _originTracker.enabled = true;
            yield return new WaitForSeconds(5f);
            yield return new WaitUntil(() => _originTracker.assigned);
            
            while(CriterionAssessment())
            {
                while(((!isAlignedPosition || !isAlignedRotation ) & _offsetsCurrent))
                {

                    if(!isAlignedPosition)
                    {
                        CalibrateChaperonePosition();
                        _offsetsCurrent = false;
                    }

                    yield return new WaitForSeconds(.1f);

                    if(!isAlignedRotation)
                    {
                        CalibrateChaperoneOrientation();
                        _offsetsCurrent = false;
                    }
                }
                if(SceneManager.GetActiveScene().name == experimentalSceneName)
                {
                    if(!sessionEventAssigned)
                {
                    Session.instance.onTrialBegin.AddListener(UpdateExperimentDetails);
                    sessionEventAssigned = true;
                }

                    yield return new WaitForSeconds(10f);
                } else
                {
                    yield return new WaitForSeconds(3f);
                }       
            }
        
            coroutineEntered = false;
            yield break;
        }

        /// <summary>
        /// Takes the X and Z offsets of a tracker positioned at the origin, and the y offset of a secondary tracker placed on a floor or table, and uses them as the new SteamVR origin.
        /// </summary>
        private void CalibrateChaperonePosition() {
            var state = OpenVR.Chaperone.GetCalibrationState();
            if (state != ChaperoneCalibrationState.OK)
            {
                return;
            }
        
            var currentOffset = _originTrackerObject.transform.position;

            var rt = new SteamVR_Utils.RigidTransform
            {
                pos = new Vector3(
                    currentOffset.x, 
                    currentOffset.y - (RigHeight + TrackerHeight), 
                    -currentOffset.z
                ),
                rot = Quaternion.Inverse(_originTrackerObject.transform.rotation) * Quaternion.identity
            };

            var originOffset = EasyOpenVRSingleton.UnityUtils.MatrixToPosition(rt.ToHmdMatrix34());

            _trackingInstance.MoveUniverse(originOffset, false, true);
        }

        /// <summary>
        /// Uses the SteamVR Chaperone API to rotate the SteamVR universe (and a Transform) to match the physical orientation of the origin tracker.
        /// </summary>
        private void CalibrateChaperoneOrientation() {
            var state = OpenVR.Chaperone.GetCalibrationState();
            if (state != ChaperoneCalibrationState.OK)
            {
                Debug.Log("GetCalibrationState() = " + state.ToString());
                return;
            }

            //Rotate the tracking space until the offset is reduced below half a mm.
            OpenVR.ChaperoneSetup.RevertWorkingCopy();
            HmdMatrix34_t standingPos = new();
            HmdMatrix34_t sittingPos = new();


            Vector3 rotationalOffset = Quaternion.Inverse(Quaternion.LookRotation(
                _originTrackerObject.transform.TransformDirection(Vector3.up), 
                _originTrackerObject.transform.TransformDirection(Vector3.forward)
            )).eulerAngles;
            _yawRotationalOffset = rotationalOffset.y;

            if(_yawRotationalOffset > RotationOffsetTolerance) FixYaw(standingPos, sittingPos);

            switch(calibrationMode)
            {
                case CalibrationMode.OneTracker:
                    if(! _threeTrackerCalibrated)
                    {
                        _yawRotationalOffset = rotationalOffset.y;
                        _pitchRotationalOffset = rotationalOffset.x;
                        _rollRotationalOffset = rotationalOffset.z;

                        if(_pitchRotationalOffset >= RotationOffsetTolerance)
                        {
                            autoRotationOffset = UpdateRotationCorrection(rotationalOffset);
                            if(manualRotationOffset == Vector3.zero) OpenVRUniverseOffset.eulerAngles = autoRotationOffset;
                        }
                    }
                    return;
                // At the moment, the Two and ThreeTracker modes don't do anything. Inter-device variance is too high, and most of the time 
                // they don't get closer to correct than the OneTracker mode as a result.
                case CalibrationMode.TwoTrackers:
                    break;
                case CalibrationMode.ThreeTrackers:
                    break;

            }

        }

        /// <summary>
        /// Fixes the yaw of the SteamVR tracking space by rotating it around the Y axis. Changes the universe itself in order to make the
        /// OpenVR api recordings match more closely to Unity.
        /// </summary>
        /// <param name="standingPos"></param>
        /// <param name="sittingPos"></param>
        private void FixYaw(HmdMatrix34_t standingPos, HmdMatrix34_t sittingPos)
        {
            if (!(Mathf.Abs(_yawRotationalOffset) >= RotationOffsetTolerance)) return;

            //Get the current SteamVR tracking space
            OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref standingPos);
            OpenVR.ChaperoneSetup.GetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPos);
                
            standingPos = standingPos.RotateY(_yawRotationalOffset);
            sittingPos = sittingPos.RotateY(_yawRotationalOffset);
                    
            OpenVR.ChaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref standingPos);
            OpenVR.ChaperoneSetup.SetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPos);
            OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live);
        }

        private Vector3 UpdateRotationCorrection(Vector3 rotationalOffset)
        {
            // If the queues are full, remove the oldest value and add the newest
            if (_orientationCorrectionsPitch.Count >= _orientationCorrectionsToStore)
                {
                    _orientationCorrectionsPitch.Dequeue();
                }
            if (_orientationCorrectionsRoll.Count >= _orientationCorrectionsToStore)
                {
                    _orientationCorrectionsRoll.Dequeue();
                }
            // Make sure the pitch and roll are within the range of 0 to 360
            if(rotationalOffset.x > 180)
            {
                rotationalOffset.x -= 360;
            }
            if(rotationalOffset.z > 180)
            {
                rotationalOffset.z -= 360;
            }
            // Add the newest pitch and roll values to their respective queues
            _orientationCorrectionsPitch.Enqueue(rotationalOffset.x);
            _orientationCorrectionsRoll.Enqueue(-rotationalOffset.z);

            // If there are not enough values, return the current offset
            if(_orientationCorrectionsPitch.Count < _orientationCorrectionsToStore/2 || _orientationCorrectionsRoll.Count < _orientationCorrectionsToStore/2) return OpenVRUniverseOffset.eulerAngles;

            // Calculate the average of the pitch and roll values
            float pitchAverage = 0f;
            float rollAverage = 0f;
            foreach(float v in _orientationCorrectionsPitch)
            {
                pitchAverage += v;
            }
            pitchAverage /= _orientationCorrectionsPitch.Count;
            foreach(float v in _orientationCorrectionsRoll)
            {
                rollAverage += v;
            }
            rollAverage /= _orientationCorrectionsRoll.Count;


        return new Vector3(pitchAverage, 0, rollAverage);
        }

        private float _lastOffsetUpdate = 0f;

        private void Update()
        {

            if (_lastOffsetUpdate > .1f)
            {
                GetUpdatedOffsets();
                _lastOffsetUpdate = 0f;
            }
            else
            {
                _lastOffsetUpdate += Time.deltaTime;
            }
            if(Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                calibrationMode = CalibrationMode.OneTracker;
            } else if(Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                calibrationMode = CalibrationMode.TwoTrackers;
            } else if(Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                calibrationMode = CalibrationMode.ThreeTrackers;
            }
 
            if(manualPositionOffset != Vector3.zero)
            {
                OpenVRUniverseOffset.transform.position = manualPositionOffset;
            }
            if(manualRotationOffset != Vector3.zero)
            {
                OpenVRUniverseOffset.transform.eulerAngles = autoRotationOffset + manualRotationOffset;
            } 
            if(manualScaleOffset != Vector3.one)
            {
                OpenVRUniverseOffset.transform.localScale = manualScaleOffset;
            }

            if (coroutineEntered) return;
            coroutineEntered = true;
            StartCoroutine(RecalibrateUniverse());
        }
    }
}
