using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Valve.VR;
  using taskAssets.Instructions;
using UXF;

public class AvatarHandler : MonoBehaviour
{
    [SerializeField] private GameObject avatar, participantRig, avatarLabRig;
    [SerializeField] private List<Transform> participantAvatarTargets;
    [SerializeField] private AvatarSetup avatarSetup;
    [SerializeField] private AvatarCalibration avatarCalibration;
    [SerializeField] private InstructionAudio instructionAudio;
    [SerializeField] private AudioClip instructionsArmLength;
    [SerializeField] private AudioClip[] instructionsCalibrationConfirmation;
    [SerializeField] private bool testing = false;
    public SteamVR_Action_Boolean triggerWatcher, touchpadWatcher;
    public SteamVR_Input_Sources handType;
    private bool _calibrationRequested = false;
    private bool _coroutineRunning = false;
    public bool _avatarCalibrationStage = false;

    void Awake()
    {
        if(participantRig == null)
            {
                participantRig = GameObject.Find("participantRig");
                if(participantRig == null) participantRig = Instantiate(avatarLabRig, Vector3.zero, Quaternion.identity, GameObject.Find("AvatarLabObjects").transform);
                foreach (GameObject tracker in GameObject.FindGameObjectsWithTag("trackerModels"))
                {
                    tracker.layer = 3;
                }
            }
        if (SceneManager.GetActiveScene().name == "participantCalibration" & SceneManager.sceneCount < 2)
        {
            triggerWatcher.AddOnStateDownListener(TriggerDown, handType);
            touchpadWatcher.AddOnStateDownListener(TouchpadDown, handType);
        }
        else
        {
            avatar = GameObject.Find("ParticipantAvatar");
            if (avatar != null)
            {
                // Add the renderer to light layers 1, 2 and 3.
                avatar.GetComponentInChildren<SkinnedMeshRenderer>().renderingLayerMask = 1 << 1 | 1 << 2 | 1 << 3;
                //Find the target transforms
                FindTargets(participantRig.transform);
                // Change the rendering layer of all of the gameobjects that have names starting with "vr_tracker" to 3 (Hidden from Participant).
                return;
            } else
            {
                LoadAndCalibrateAvatar();
            }
        }
    }

    public void LoadAndCalibrateAvatar()
    {
        avatar = PlayerPrefs.GetString("Chosen Avatar") != null ?
            avatarSetup.LoadAvatarPrefab(PlayerPrefs.GetString("Chosen Avatar")) :
            avatarSetup.LoadAvatarPrefab(avatarSetup.FindAvatarPrefab(testing));
        avatar.GetComponentInChildren<SkinnedMeshRenderer>().renderingLayerMask = 1 << 1 | 1 << 2 | 1 << 3;

        //Find the transforms in the participantRig that are named as targets.
        FindTargets(participantRig.transform);
        avatarSetup.AttachTargets(avatar, participantAvatarTargets);
        ApplyTargetOffsets();
        avatarCalibration.CalibrationSetup(avatar.transform);
        avatarCalibration.ApplyCalibrationSettings();
        if (SceneManager.GetActiveScene().name == "AvatarLab")
        {
            AttachAnchors();
        }
    }

    private void FindTargets(Transform participantRig)
    {
        participantAvatarTargets ??= new List<Transform>();
        if(participantAvatarTargets.Count > 0) participantAvatarTargets.Clear();
        string[] targetNames = new string[] { 
            "left_hand_target", 
            "right_hand_target", 
            "right_elbow_target", 
            "right_foot_target", 
            "left_foot_target", 
            "head_target" };
        foreach(string targetName in targetNames)
        {
            Transform target = BoneUtilities.SearchHierarchyForBone(participantRig, targetName);
            if((targetName == "right_foot_target" || targetName == "left_foot_target") && SceneManager.GetActiveScene().name == "AvatarLab")
            {
                target.gameObject.SetActive(false);
                continue;
            }
            
            if (target != null) participantAvatarTargets.Add(target);
        }
    }

    private void ApplyTargetOffsets()
    {
        //Find the target transforms in the list of participantAvatarTargets, and then apply the offsets.
        foreach (Transform target in participantAvatarTargets)
        {
            if (target.name == "left_hand_target")
            {
                target.localPosition = new Vector3(
                    PlayerPrefs.GetFloat("wristTargetLeftX", target.localPosition.x),
                    PlayerPrefs.GetFloat("wristTargetLeftY", target.localPosition.y),
                    PlayerPrefs.GetFloat("wristTargetLeftZ", target.localPosition.z)
                    );
                target.localRotation = new Quaternion(
                    PlayerPrefs.GetFloat("wristTargetLeftQX", target.localRotation.x),
                    PlayerPrefs.GetFloat("wristTargetLeftQY", target.localRotation.y),
                    PlayerPrefs.GetFloat("wristTargetLeftQZ", target.localRotation.z),
                    PlayerPrefs.GetFloat("wristTargetLeftQW", target.localRotation.w)
                    );
            }
            else if (target.name == "right_hand_target")
            {
                target.localPosition = new Vector3(
                    PlayerPrefs.GetFloat("wristTargetRightX", target.localPosition.x),
                    PlayerPrefs.GetFloat("wristTargetRightY", target.localPosition.y),
                    PlayerPrefs.GetFloat("wristTargetRightZ", target.localPosition.z)
                    );
                target.localRotation = new Quaternion(
                    PlayerPrefs.GetFloat("wristTargetRightQX", target.localRotation.x),
                    PlayerPrefs.GetFloat("wristTargetRightQY", target.localRotation.y),
                    PlayerPrefs.GetFloat("wristTargetRightQZ", target.localRotation.z),
                    PlayerPrefs.GetFloat("wristTargetRightQW", target.localRotation.w)
                    );
            }
            else if (target.name == "right_elbow_target")
            {
                target.localPosition = new Vector3(
                    PlayerPrefs.GetFloat("elbowTargetRightX", target.localPosition.x),
                    PlayerPrefs.GetFloat("elbowTargetRightY", target.localPosition.y),
                    PlayerPrefs.GetFloat("elbowTargetRightZ", target.localPosition.z)
                    );
            }
        }
    }

    private void TriggerDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        if (SceneManager.GetActiveScene().name != "participantCalibration" || !_avatarCalibrationStage) return;
        Utilities.UXFDebugLog("Trigger click registered.");
        switch (avatarCalibration.calibrationStatus)
        {
            case AvatarCalibration.CalibrationStatus.Uninitiated:
                if (_coroutineRunning) break;
                StartCoroutine(nameof(HandleAvatarCalibration));
                break;
            case AvatarCalibration.CalibrationStatus.Uncalibrated:
                _calibrationRequested = true;
                break;
            case AvatarCalibration.CalibrationStatus.Calibrated:
                if (_coroutineRunning) break;
                avatarCalibration.ResetCalibration();
                StartCoroutine(nameof(HandleAvatarCalibration));
                break;
        }
    }

    public void AttachAnchors()
    {
        var participantAvatarAnchorParent = GameObject.Find("chairAnchors");
        DontDestroyOnLoad(participantAvatarAnchorParent);
        avatarSetup.AttachAnchors(GetAvatar(), participantAvatarAnchorParent.transform);
    }


    private void TouchpadDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        if (SceneManager.GetActiveScene().name != "participantCalibration" || !_avatarCalibrationStage) return;
        if (avatarCalibration.calibrationStatus is AvatarCalibration.CalibrationStatus.Calibrated)
        {
         avatarCalibration.calibrationStatus = AvatarCalibration.CalibrationStatus.Confirmed;
         UXF.Utilities.UXFDebugLog("[AvatarHandler] Avatar calibration confirmed.");
         triggerWatcher.RemoveOnStateDownListener(TriggerDown, handType);
         touchpadWatcher.RemoveOnStateDownListener(TouchpadDown, handType);
         return;
        }
        Utilities.UXFDebugLog("Touchpad click registered.");
    }

    public void BeginAvatarCalibration()
    {
        if (_coroutineRunning) return;
        _avatarCalibrationStage = true;
        StartCoroutine(nameof(HandleAvatarCalibration));
    }

    /// <summary>
    /// Handles the avatar calibration process.
    /// </summary>
    /// <returns></returns>
    private IEnumerator HandleAvatarCalibration()
    {
        _coroutineRunning = true;
        // If the avatar is not already present, load it and initiate calibration.
        if (avatar == null)
        {
            // Find and instantiate the avatar based on participant preferences from the menu.
            var avatarPath = avatarSetup.FindAvatarPrefab(testing);
            if (avatarPath == null)
            {
                Debug.Log("No avatar found");
                yield break;
            }
            Debug.Log("Avatar path:" + avatarPath);
            avatar = avatarSetup.LoadAvatarPrefab(avatarPath);
            avatar.name = "ParticipantAvatar";
            DontDestroyOnLoad(avatar);
            // Set up the VRIK component to target the correct game objects.
            FindTargets(participantRig.transform);
            avatarSetup.AttachTargets(avatar, participantAvatarTargets);
            avatarCalibration.CalibrationSetup(avatar.transform);
        }

        // Begin calibrating the avatar.
        while (avatarCalibration.calibrationStatus is AvatarCalibration.CalibrationStatus.Uncalibrated)
        {
            yield return new WaitUntil(() => _calibrationRequested);
            avatarCalibration.CalibrateAvatarHeight();
                        yield return new WaitUntil(
                () => touchpadWatcher.GetStateDown(handType));
            instructionAudio.PlayAudio(instructionsArmLength);

            yield return new WaitForSeconds(instructionsArmLength.length + 2f);
            yield return avatarCalibration.CalibrateArmLength();

            instructionAudio.PlayAudioSequentially(instructionsCalibrationConfirmation);
            yield return new WaitUntil(
                () => touchpadWatcher.GetStateDown(handType) 
                || triggerWatcher.GetStateDown(handType));
        }
        _coroutineRunning = false;
    }
    
    public GameObject GetAvatar()
    {
        return avatar;
    }

    private bool advanceCoroutineOnInput()
    {
        if(Keyboard.current.rightArrowKey.wasReleasedThisFrame || touchpadWatcher.GetStateDown(handType))
        {
            return true;
        }
        return Keyboard.current.rightArrowKey.wasReleasedThisFrame || touchpadWatcher.GetStateDown(handType) || Keyboard.current.fKey.wasReleasedThisFrame;
    }

    
}

