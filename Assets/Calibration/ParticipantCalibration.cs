using System.Collections;
using taskAssets.Instructions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UXF;
using Valve.VR;
using ViveSR.anipal.Eye;

namespace Calibration
{
    /// <summary>
    /// Handles the participant calibration scene, which involves calibrating the Vive Pro eye tracker and walking around a model of the lab to a random-ish-ly positioned green area.
    /// </summary>
    public class ParticipantCalibration : MonoBehaviour
    {
        private float _timeAtStart;
        [SerializeField] private bool playAudio = true;

        // Bools to control whether the calibration steps are skipped. Useful for debugging.
        [SerializeField] private bool skip_universe_validation = true;
        [SerializeField] private bool skip_eye_tracking = true;
        [SerializeField] private bool skip_targetcalibration = true;
        [SerializeField] private bool skip_walking = true;
        [SerializeField] private bool skip_avatar_calibration = true;
        [SerializeField] private bool manual_calibration = true;
        [SerializeField] private bool skip_wrist_calibration = true;
        [SerializeField] private bool skip_elbow_calibration = true;
        [SerializeField] private bool skip_upperarm_calibration = true;
        [SerializeField] private bool skip_head_calibration = true;
    
        private AudioSource _audioSource;
        public InstructionAudio instructionAudio;
        public AudioClip finished;
    
        [SerializeField] private AudioClip[] instructionsEyeTracker;
        [SerializeField] private AudioClip[] instructionsEyeTrackerPost;
        [SerializeField] private AudioClip[] instructionsAvatar;
        [SerializeField] private AudioClip[] instructionsAvatar2;
        [SerializeField] private AudioClip[] instructionsAvatar3;

        [SerializeField] private AudioClip[] instructionsWalk;
        [SerializeField] private AudioClip[] instructionsEnd1;
        [SerializeField] private AudioClip[] instructionsEnd2;
        [SerializeField] private AudioClip instructionsWristPivot;
        [SerializeField] private AudioClip elbow, upperarm, head;
        [SerializeField] private AudioClip instructionsManualWristPivot;
        [SerializeField] private AudioClip confirmationAudio;
        [SerializeField] private AudioClip targetSaved;
        [SerializeField] private AudioClip endWalkAudio;

    
        [FormerlySerializedAs("instructionsHMD")] public TextMeshProUGUI instructionsHmd;
        public TextMeshProUGUI instructionsWalking, instructionsSeat;
    
        public SteamVR_Action_Boolean triggerWatcher, touchpadWatcher;
        public SteamVR_Input_Sources handType;

        [SerializeField] private AvatarCalibration avatarCalibration;
    
        private readonly float[] _xCoords = new float[]{-2, 2, -3};
        private readonly float[] _zCoords = new float[]{0, 1, -1.5f};
        private readonly float[] _yCoords = new float[]{0, 0, 0};
    
        [SerializeField] private AvatarHandler avatarHandler;
        [SerializeField] private GameObject calibrationCube;
        [SerializeField] private GameObject footTrackerRight;
        [SerializeField] private GameObject footTrackerLeft;
        public GameObject[] destroyAfterSceneLoad;

        private bool _coroutineEntered, _walkTargetEntered, _eyeTracking, 
        targets_calibrated, inTargetCalibration = false, _xPressed = false, _zPressed = false, 
        _triggerClicked = false, _touchpadClicked = false, confirmTargets = false;
        private float lastInput = 0;

        // Start is called before the first frame update
        private void Start()
        {
            _audioSource = GetComponent<AudioSource>();

            _walkTargetEntered = false;
            _coroutineEntered = false;
        }

        // Update is called once per frame
        private void Update()
        {
            if(!_coroutineEntered)
            {
                _coroutineEntered = true;
                _walkTargetEntered = false;
                StartCoroutine(ParticipantWalk());
            }
            // Use keyboard input to determine whether eye tracking is to be used. Necessary due to calibration failures for some participants.
            if(Keyboard.current.zKey.isPressed)
            {
                _eyeTracking = true;
                _zPressed = true;
            } else if(Keyboard.current.xKey.isPressed)
            {
                _eyeTracking = false;
                _xPressed = true;
            } else if(Keyboard.current.fKey.isPressed)
            {
                confirmTargets = false;
            }
        }

        private void OnTriggerEnter(Collider other) {
            if(other.name.Contains("target"))
            {
                _walkTargetEntered = true;
            }
        }

        private bool GateInput()
        {
            if(Time.time - lastInput < 1f) return false;
            lastInput = Time.time;
            return true;
        }

        private void ResetInputBools()
        {
            _triggerClicked = false;
            _touchpadClicked = false;
            confirmTargets = false;
        }
    
        /// <summary>
        /// Generates a point cloud around the approximated target area, which iteratively shrinks toward the true target while the participant pivots the limb it is
        /// attached to.
        /// </summary>
        /// <returns></returns>
        private IEnumerator CalibrateInverseKinematicTargets()
        {


            inTargetCalibration = true;


            ResetInputBools();
            AutoPivotPointFinder autoPivotPointFinder = AutoPivotPointFinder.Instance;
            
            // Wrist target position
            Transform wristTargetRight = GameObject.Find("right_hand_target").transform;
            Transform wristTargetLeft = GameObject.Find("left_hand_target").transform;
            if(!skip_wrist_calibration)
            {
                Transform handTrackerRight = GameObject.Find("right_hand_tracker").transform;
                while (!confirmTargets)
                {
                    ResetInputBools();
                    touchpadWatcher.RemoveOnStateDownListener(TouchpadDown, handType);
                    instructionAudio.PlayAudio(instructionsWristPivot);
                    autoPivotPointFinder.target = wristTargetRight;
                    StartCoroutine(autoPivotPointFinder.IsolateJointPosition(handTrackerRight, 30, 0.0055f, 720f, "x", 4, 3, false));
                    yield return new WaitUntil(() => autoPivotPointFinder.pivotPointFound);
                    autoPivotPointFinder.pivotPointFound = false;
                    // Use the right wrist target to set the left wrist target (not ideal, but need to save time)
                    wristTargetLeft.localPosition = new Vector3(
                        -wristTargetRight.localPosition.x,
                        wristTargetRight.localPosition.y, 
                        wristTargetRight.localPosition.z);
                    print("Hand Tracker-relative wrist position: " + wristTargetRight.localPosition);
                    touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                }
                SaveWristTargets(wristTargetRight, wristTargetLeft);
                instructionAudio.PlayAudio(targetSaved);
            }
            else 
            {
                LoadWristTargets(wristTargetRight, wristTargetLeft);
            }





            ResetInputBools();


            // Elbow target position
            ResetInputBools();
            Transform elbowTargetRight = GameObject.Find("right_elbow_target").transform;
            if(!skip_elbow_calibration)
            {
                Transform elbowCalibrationAssist = GameObject.Find("elbow_calibration_tracker").transform;
                while (!confirmTargets)
                {
                    yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
                    instructionAudio.PlayAudio(elbow);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                    ResetInputBools();
                    touchpadWatcher.RemoveOnStateDownListener(TouchpadDown, handType);
                    autoPivotPointFinder.target = elbowTargetRight;
                    StartCoroutine(autoPivotPointFinder.IsolateJointPosition(elbowCalibrationAssist, 30, 0.025f, 540f, "x", 4, 3, false));
                    yield return new WaitUntil(() => autoPivotPointFinder.pivotPointFound);
                    autoPivotPointFinder.pivotPointFound = false;
                    print("Right Elbow Tracker-relative Elbow position: " + elbowTargetRight.localPosition);
                    touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                }
                elbowCalibrationAssist.gameObject.SetActive(false);
                SaveElbowTargets(elbowTargetRight);
                instructionAudio.PlayAudio(targetSaved);
                yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
            }
            else
            {
                LoadElbowTargets(elbowTargetRight);
            }

            ResetInputBools();
            Transform upperArmTargetRight = GameObject.Find("right_upperarm_target").transform;
            if(!skip_upperarm_calibration)
            {
                Transform elbowTracker = GameObject.Find("right_elbow_tracker").transform;

                while (!confirmTargets)
                {

                    yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
                    instructionAudio.PlayAudio(upperarm);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                    ResetInputBools();
                    touchpadWatcher.RemoveOnStateDownListener(TouchpadDown, handType);
                    autoPivotPointFinder.target = upperArmTargetRight;
                    StartCoroutine(autoPivotPointFinder.IsolateJointPosition(elbowTracker, 30, 0.025f, 540f, "x", 4, 3, false));
                    ResetInputBools();                
                    yield return new WaitUntil(() => autoPivotPointFinder.pivotPointFound);
                    autoPivotPointFinder.pivotPointFound = false;
                    print("Right Elbow Tracker-relative Upper Arm position: " + upperArmTargetRight.localPosition);
                    touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                }
                SaveUpperArmTargets(upperArmTargetRight); 
                ResetInputBools();
                instructionAudio.PlayAudio(targetSaved);
                yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
            }           
            else {
                LoadUpperArmTargets(upperArmTargetRight);
            }



            Transform headTarget = GameObject.Find("head_target").transform;
            if(!skip_head_calibration)
            {
                Transform headTracker = GameObject.Find("participant_hmd").transform;
                while (!confirmTargets && !skip_head_calibration)
                {
                    yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
                    instructionAudio.PlayAudio(head);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                    ResetInputBools();
                    touchpadWatcher.RemoveOnStateDownListener(TouchpadDown, handType);
                    autoPivotPointFinder.target = headTarget;
                    StartCoroutine(autoPivotPointFinder.IsolateJointPosition(headTracker, 15, 0.035f, 200f, "x", 4, 3, false));
                    ResetInputBools();                
                    yield return new WaitUntil(() => autoPivotPointFinder.pivotPointFound);
                    autoPivotPointFinder.pivotPointFound = false;
                    print("HMD-relative head position: " + headTarget.localPosition);
                    touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);
                    yield return new WaitUntil(AdvanceCoroutineOnInput);
                }      
                ResetInputBools();     
                SaveHeadTargets(headTarget); 
                instructionAudio.PlayAudio(targetSaved);
                yield return new WaitUntil(()=> !instructionAudio.IsPlaying());
            }
            else {
                LoadHeadTargets(headTarget);
            }

            PlayerPrefs.Save();

            inTargetCalibration = false;
            targets_calibrated = true;
        }

        private IEnumerator ManualWristAndElbowAdjustment()
        {
                Transform wristTargetRight = GameObject.Find("right_hand_target").transform;
                Transform wristTargetLeft = GameObject.Find("left_hand_target").transform;
                Transform elbowTargetRight = GameObject.Find("right_elbow_target").transform;
            
                calibrationCube.SetActive(true);
                instructionAudio.PlayAudio(instructionsManualWristPivot);
                // var _manualPivotPointFinder = this.GetComponent<ManualPivotPointFinder>();
                // _manualPivotPointFinder.ChangeTargetTransform(wristTargetRight);
                // yield return StartCoroutine(_manualPivotPointFinder.AdjustTarget());
                // yield return new WaitUntil(() => _manualPivotPointFinder.adjustmentConfirmed);
                // instructionAudio.PlayAudio(targetSaved);

                // _manualPivotPointFinder.ChangeTargetTransform(wristTargetLeft);
                // StartCoroutine(_manualPivotPointFinder.AdjustTarget());
                // yield return new WaitUntil(() => _manualPivotPointFinder.adjustmentConfirmed);
                // instructionAudio.PlayAudio(targetSaved);
                
                // _manualPivotPointFinder.ChangeTargetTransform(elbowTargetRight);
                // StartCoroutine(_manualPivotPointFinder.AdjustTarget());
                // yield return new WaitUntil(() => _manualPivotPointFinder.adjustmentConfirmed);
                yield return new WaitUntil(AdvanceCoroutineOnInput);
                instructionAudio.PlayAudio(targetSaved);
                calibrationCube.SetActive(false);
                SaveWristTargets(wristTargetRight, wristTargetLeft);
                SaveElbowTargets(elbowTargetRight);
                targets_calibrated = true;
        }

        private static void SaveElbowTargets(Transform elbowTargetRight)
        {
            PlayerPrefs.SetFloat("elbowTargetRightX", elbowTargetRight.localPosition.x);
            PlayerPrefs.SetFloat("elbowTargetRightY", elbowTargetRight.localPosition.y);
            PlayerPrefs.SetFloat("elbowTargetRightZ", elbowTargetRight.localPosition.z);
        }

        private static void SaveHeadTargets(Transform headTarget)
        {
            PlayerPrefs.SetFloat("headTargetX", headTarget.localPosition.x);
            PlayerPrefs.SetFloat("headTargetY", headTarget.localPosition.y);
            PlayerPrefs.SetFloat("headTargetZ", headTarget.localPosition.z);
        }

        private static void SaveUpperArmTargets(Transform upperArmTargetRight)
        {
            PlayerPrefs.SetFloat("upperArmTargetRightX", upperArmTargetRight.localPosition.x);
            PlayerPrefs.SetFloat("upperArmTargetRightY", upperArmTargetRight.localPosition.y);
            PlayerPrefs.SetFloat("upperArmTargetRightZ", upperArmTargetRight.localPosition.z);
        }

        private static void SaveWristTargets(Transform wristTargetRight, Transform wristTargetLeft)
        {
            PlayerPrefs.SetFloat("wristTargetRightX", wristTargetRight.localPosition.x);
            PlayerPrefs.SetFloat("wristTargetRightY", wristTargetRight.localPosition.y);
            PlayerPrefs.SetFloat("wristTargetRightZ", wristTargetRight.localPosition.z);
            PlayerPrefs.SetFloat("wristTargetRightQX", wristTargetRight.localRotation.x);
            PlayerPrefs.SetFloat("wristTargetRightQY", wristTargetRight.localRotation.y);
            PlayerPrefs.SetFloat("wristTargetRightQZ", wristTargetRight.localRotation.z);
            PlayerPrefs.SetFloat("wristTargetRightQW", wristTargetRight.localRotation.w);

            print("Right wrist target saved");

            PlayerPrefs.SetFloat("wristTargetLeftX", wristTargetLeft.localPosition.x);
            PlayerPrefs.SetFloat("wristTargetLeftY", wristTargetLeft.localPosition.y);
            PlayerPrefs.SetFloat("wristTargetLeftZ", wristTargetLeft.localPosition.z);
            PlayerPrefs.SetFloat("wristTargetLeftQX", wristTargetLeft.localRotation.x);
            PlayerPrefs.SetFloat("wristTargetLeftQY", wristTargetLeft.localRotation.y);
            PlayerPrefs.SetFloat("wristTargetLeftQZ", wristTargetLeft.localRotation.z);
            PlayerPrefs.SetFloat("wristTargetLeftQW", wristTargetLeft.localRotation.w);

            print("Left wrist target saved");
        }

        private void LoadWristTargets(Transform wristTargetRight, Transform wristTargetLeft)
        {
            wristTargetRight.localPosition = new Vector3(PlayerPrefs.GetFloat("wristTargetRightX"), PlayerPrefs.GetFloat("wristTargetRightY"), PlayerPrefs.GetFloat("wristTargetRightZ"));
            wristTargetRight.localRotation = new Quaternion(PlayerPrefs.GetFloat("wristTargetRightQX"), PlayerPrefs.GetFloat("wristTargetRightQY"), PlayerPrefs.GetFloat("wristTargetRightQZ"), PlayerPrefs.GetFloat("wristTargetRightQW"));

            wristTargetLeft.localPosition = new Vector3(PlayerPrefs.GetFloat("wristTargetLeftX"), PlayerPrefs.GetFloat("wristTargetLeftY"), PlayerPrefs.GetFloat("wristTargetLeftZ"));
            wristTargetLeft.localRotation = new Quaternion(PlayerPrefs.GetFloat("wristTargetLeftQX"), PlayerPrefs.GetFloat("wristTargetLeftQY"), PlayerPrefs.GetFloat("wristTargetLeftQZ"), PlayerPrefs.GetFloat("wristTargetLeftQW"));
        }

        private void LoadElbowTargets(Transform elbowTargetRight)
        {
            elbowTargetRight.localPosition = new Vector3(PlayerPrefs.GetFloat("elbowTargetRightX"), PlayerPrefs.GetFloat("elbowTargetRightY"), PlayerPrefs.GetFloat("elbowTargetRightZ"));
        }

        private void LoadUpperArmTargets(Transform upperArmTargetRight)
        {
            upperArmTargetRight.localPosition = new Vector3(PlayerPrefs.GetFloat("upperArmTargetRightX"), PlayerPrefs.GetFloat("upperArmTargetRightY"), PlayerPrefs.GetFloat("upperArmTargetRightZ"));
        }

        private void LoadHeadTargets(Transform headTarget)
        {
            headTarget.localPosition = new Vector3(PlayerPrefs.GetFloat("headTargetX"), PlayerPrefs.GetFloat("headTargetY"), PlayerPrefs.GetFloat("headTargetZ"));
        }

        private bool AdvanceCoroutineOnInput()
        {
            if(Keyboard.current.rightArrowKey.wasReleasedThisFrame || touchpadWatcher.GetStateDown(handType) & inTargetCalibration)
            {
                confirmTargets = true;
                return true;
            }
            return Keyboard.current.rightArrowKey.wasReleasedThisFrame || touchpadWatcher.GetStateDown(handType) || Keyboard.current.fKey.wasReleasedThisFrame;
        }

        private void ActivateFootTrackers()
        {
            footTrackerLeft.SetActive(true);
            footTrackerRight.SetActive(true);
        }

        /// <summary>
        /// Handles the launching of eye tracking calibration and the period of walking. Loads menu scene once finished.
        /// </summary>
        private IEnumerator ParticipantWalk() 
        {

            UniverseCalibrationValidation universeCalibrationValidation = GameObject.Find("universeCalibrationLocation").GetComponent<UniverseCalibrationValidation>();

            triggerWatcher.AddOnStateDownListener(TriggerDown, handType);
            touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);

            if (!skip_universe_validation) yield return new WaitUntil(() => universeCalibrationValidation.universeValidated);
            
            if(!skip_targetcalibration)
            {
                StartCoroutine(CalibrateInverseKinematicTargets());
                yield return new WaitUntil(() => targets_calibrated);
            }

            if(!skip_eye_tracking)
            {
                yield return new WaitUntil(()=>_xPressed || _zPressed);
                if (playAudio) instructionAudio.PlayAudioSequentially(instructionsEyeTracker);
                yield return new WaitUntil(()=>!instructionAudio.IsPlaying());
                bool eyeTrackerCalibrated = false;
                int tries = 0;
                if(_eyeTracking && !eyeTrackerCalibrated && tries < 3)
                {
                    DontDestroyOnLoad(GameObject.Find("SRanipal Eye Framework"));
                    eyeTrackerCalibrated = SRanipal_Eye_v2.LaunchEyeCalibration();
                    tries++;
                }

                yield return new WaitForSeconds(2);
                if (playAudio) instructionAudio.PlayAudioSequentially(instructionsEyeTrackerPost);
            
                yield return new WaitUntil(() => !instructionAudio.IsPlaying());
            }
        
            ActivateFootTrackers();

            if(!skip_avatar_calibration)
            {
                avatarHandler.BeginAvatarCalibration();
                yield return new WaitUntil(() =>
                    avatarCalibration.calibrationStatus is AvatarCalibration.CalibrationStatus.Confirmed);


            if(!skip_walking) gameObject.GetComponent<Renderer>().enabled = true;
            yield return new WaitUntil(AdvanceCoroutineOnInput);
            }
            else
            {
                avatarHandler.LoadAndCalibrateAvatar();
            }
            
            if(!skip_walking)
            {
                if (playAudio) instructionAudio.PlayAudioSequentially(instructionsWalk);
                yield return new WaitUntil(() => !instructionAudio.IsPlaying());
                yield return new WaitUntil(() => _walkTargetEntered);
                // gameObject.transform.position = new Vector3(_xCoords[Random.Range(0, _xCoords.Length)], 
                //     1, 
                //     _zCoords[Random.Range(0, _xCoords.Length)]);


                _timeAtStart = Time.time;

                //for 60 seconds, have participant walk to random corners of the room.


                while(Time.time - _timeAtStart < 60)
                {
                    yield return new WaitUntil(() => _walkTargetEntered);
                    instructionAudio.PlayAudio(confirmationAudio);
                    var newVector = false;
                    var position = gameObject.transform.position;                    
                    while(!newVector)
                    {
                        var tempPosition = position;
                        position = new Vector3(_xCoords[Random.Range(0, _xCoords.Length)], 
                            1, 
                            _zCoords[Random.Range(0, _xCoords.Length)]);
                        gameObject.transform.position = position;
                        newVector = !(tempPosition == position) &
                                    !(Vector3.Distance(tempPosition, position) < 3f);
                    }
                    _walkTargetEntered = false;    

                    yield return new WaitForSeconds(1f);
                }
            
            }
            
            gameObject.transform.position = new Vector3(0, 1, -1f);
            instructionAudio.PlayAudio(endWalkAudio);
            yield return new WaitForSeconds(1f);
            instructionAudio.PlayAudioSequentially(instructionsEnd1);
        
            yield return new WaitUntil(AdvanceCoroutineOnInput);
            avatarHandler.AttachAnchors();
            yield return new WaitUntil(AdvanceCoroutineOnInput);

            if(manual_calibration)
            {
                targets_calibrated = false;
                StartCoroutine(ManualWristAndElbowAdjustment());
                yield return new WaitUntil(() => targets_calibrated);
            }

            yield return new WaitUntil(AdvanceCoroutineOnInput);

            instructionAudio.PlayAudioSequentially(instructionsEnd2);
            yield return new WaitUntil(()=>!instructionAudio.IsPlaying());


            instructionAudio.PlayAudio(confirmationAudio);

            StartCoroutine(LoadSceneAsync());
        }
    
    

        private void TriggerDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            if (SceneManager.GetActiveScene().name != "participantCalibration" || GateInput()) return;
            _triggerClicked = true;
            lastInput = Time.time;
            Utilities.UXFDebugLog("Trigger click registered.");
        
        }

        private void TouchpadDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            if (SceneManager.GetActiveScene().name != "participantCalibration" || GateInput()) return;
            _touchpadClicked = true;
            lastInput = Time.time;
            Utilities.UXFDebugLog("Touchpad click registered.");
        }
    
        IEnumerator LoadSceneAsync()
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("AvatarLab", LoadSceneMode.Additive);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            // Destroy the objects that also exist in the AvatarLab scene
            foreach(var o in destroyAfterSceneLoad)
            {
                Destroy(o);
            }
            avatarCalibration.DestroyFakeHead();
            var OpenVRObjects = GameObject.Find("openVRUniverseObjects");
            DontDestroyOnLoad(OpenVRObjects);
            GameObject.Find("openVRObjects").transform.SetParent(OpenVRObjects.transform);

            SceneManager.UnloadSceneAsync("participantCalibration");
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("AvatarLab"));
        }
    }
}
