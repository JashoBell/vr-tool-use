using System.Collections;
using System;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using MovementTracking;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UXF;
using taskAssets.Instructions;
public class ReachingTask : MonoBehaviour
{

    // UXF Session Object, used to control the initiation and termination of trials.
    public Session session;

    public GameObject experimentControl;

    public Transform participantAvatarRoot;

    // Script that handles the presentation of pre-task instructions.
    private InstructionsDisplay _instructionsDisplay;
    private InstructionAudio _instructionAudio;

    // Script that handles the activation/deactivation of the task stimuli.
    private StimuliSetup _stimuliSetup;

    // Script that controls the activation of tracker objects.
    private TrackerControlHub _trackerControlHub;

    [SerializeField] private AvatarHandler avatarHandler;

    // Audio source and clips.
    public AudioSource audioSource;
    public AudioClip countdown, begin, select, final;
    public List<AudioClip> preTaskAudioManual, preTaskAudioTool, postTaskAudioManual, postTaskAudioTool;

    // The starting location for the task and the canvas for presenting instructions while task is ongoing.
    private GameObject _taskStart, _taskStartLeft, _reachingCanvas;

    // Panel that floats behind target location to inform participant of the current goal.
    private Image _reachingPanel;

    // Text that appears on reachingPanel
    private TextMeshProUGUI _trialText;

    // Boolean variable that indicates whether the block is still ongoing.
    private bool _taskOngoing;

    /// <summary>
    /// An enum that contains the different states of the task, used to:
    ///     1. Control the flow of the task coroutine.
    ///     2. Display instructions to the participant during the task.
    ///     3. Control the consequences of collider events.
    /// </summary>
    private enum TaskState
    {
        Countdown,
        Ongoing,
        Grasp,
        Lift,
        Replace,
        Return,
        End
    };
    [SerializeField] private TaskState taskState;

    public bool isTool, isDuringBlock = false;

    // The colliders of the fingers or tool prongs, depending on the task.
    private string _targetCollider, _targetColliderTwo;

    // Script which controls collider events for the stimulus rectangle.
    public StimuliTriggers stimuliTriggers;

    // Custom UXF Trackers for recording the position of the hand and tool during trials.
    public UXFVRPNTracker pptKinematics, pptKinematicsTool;
    public UXFOpenVRTracker steamVRKinematics, steamVRKinematicsTool;

    private UXFManusTracker _manusKinematics;

    // Custom UXF Tracker for eye-tracking data.
    public UXFEyeTrackerFixations eyeTracker;

    private void PlaySound(string clipName)
    {
        switch (clipName)
        {
            case "countdown":
                audioSource.PlayOneShot(countdown);
                break;
            case "begin":
                audioSource.PlayOneShot(begin);
                break;
            case "select":
                audioSource.PlayOneShot(select);
                break;
            case "final":
                audioSource.PlayOneShot(final);
                break;
        }
    }

    // Collider events that influence the flow of the task coroutine.
    private void OnTriggerEnter(Collider other)
    {
        var colliderObjectName = other.gameObject.name;
        switch (taskState)
        {
            case TaskState.Ongoing:
                if (colliderObjectName == _targetCollider || colliderObjectName == _targetColliderTwo)
                {
                    taskState = TaskState.Grasp;
                    // UXF.Utilities.UXFDebugLog("Object grasped.");
                }
                break;
            case TaskState.Lift:
                if (colliderObjectName == "liftTarget")
                {
                    taskState = TaskState.Replace;
                    // UXF.Utilities.UXFDebugLog("Object lifted.");
                }
                break;
            case TaskState.Replace:
                if (colliderObjectName == "stimuliStart")
                {
                    taskState = TaskState.Return;
                    // UXF.Utilities.UXFDebugLog("Object replaced.");
                }
                break;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var colliderObjectName = other.gameObject.name;
        if (taskState != TaskState.Grasp) return;
        if (colliderObjectName != "stimuliStart") return;
        taskState = TaskState.Lift;
        // UXF.Utilities.UXFDebugLog("Object being lifted.");
    }

    /// <summary>
    /// Coroutine that handles the reaching task
    /// </summary>
    /// <returns></returns>
    public IEnumerator ReachingTaskHandler()
        {
            Utilities.UXFDebugLog("Entered reaching Coroutine.");

            _instructionsDisplay = experimentControl.GetComponent<InstructionsDisplay>();
            _trackerControlHub = experimentControl.GetComponent<TrackerControlHub>();
            _stimuliSetup = experimentControl.GetComponent<StimuliSetup>();
            _instructionAudio = experimentControl.GetComponent<InstructionAudio>();
            eyeTracker = GameObject.FindGameObjectWithTag("participantCamera").GetComponent<UXFEyeTrackerFixations>();

            var preTaskAudio = isTool? preTaskAudioTool : preTaskAudioManual;
            var postTaskAudio = isTool? postTaskAudioTool : postTaskAudioManual;
            var startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            var startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
            bool isTestingMode = session.settings.GetBool("testing_mode");
            _manusKinematics = UXFManusTracker.Instance;
            //Determine number of trials. "test" reduces number of trials for testing progression of blocks.
            //If current block is 0 (i.e., this is the first block of the experiment), start on the chosen start trial.

            int totalTrialNum = CalculateTotalTrials(startTrial);

            //Assign the avatar.
            participantAvatarRoot = avatarHandler.GetAvatar().transform;
            StartCoroutine(ToggleManusScaleBones());
            //Assign the colliders for the task.
            AssignTaskColliders();

            var startTriggers = _taskStart.GetComponent<StartTriggers>();
            var startTriggersLeft = _taskStartLeft.GetComponent<StartTriggers>();
            var startDelay = session.settings.GetInt("start_delay");

            //Loop which runs for n trials, advancing as participants complete the requisite task components.
            for(var currentTrialNum = 1; currentTrialNum <= totalTrialNum; currentTrialNum++)
            {
                if(currentTrialNum == 1 && !isTestingMode)
                {
                    _instructionAudio.PlayAudioSequentially(preTaskAudio);
                    yield return new WaitUntil(()=> _instructionAudio.IsFinished());
                    yield return StartCoroutine(_instructionsDisplay.InstructionsCoroutine(startTriggers.GetTask()));
                }

                Utilities.UXFDebugLog("Trial number " + currentTrialNum + " beginning.");
                _taskOngoing = true;
                _reachingPanel.enabled = true;
                var trackersEnabled = false;
                // Countdown loop. If participant leaves start, reset countdown.
                // A moment before countdown ends, start next trial and begin recording.
                float countdownThreshold = startDelay + 1;
                float countdownTime = 0;
                float lastSound = 0;
                const float startBuffer = 0.05f;
                taskState = TaskState.Countdown;
                while(countdownTime < countdownThreshold)
                {
                    if(lastSound > 1)
                    {
                        PlaySound("countdown");
                        lastSound = 0;
                    }
                    // Just before the countdown ends, begin the next trial and start recording if the trackers have not yet been activated.
                    // This allows the offline filtering of position to have less effect on the trial-relevant position recordings.
                    if(countdownTime > countdownThreshold - startBuffer & !trackersEnabled)
                    {
                        session.BeginNextTrial();
                        session.CurrentTrial.settings.SetValue("phase", "startbuffer");
                        trackersEnabled = InitTrackerRecording();
                    }
                    if(!startTriggers.isReturned || !startTriggersLeft.isReturned)
                    {
                        countdownTime = 0;
                        lastSound = 0;
                    }
                    yield return new WaitUntil(() => startTriggers.isReturned & startTriggersLeft.isReturned);
                    countdownTime += Time.deltaTime;
                    lastSound += Time.deltaTime;
                }

                isDuringBlock = true;
                //Begin trial, note time, make stimulus visible
                PlaySound("begin");
                session.CurrentTrial.settings.SetValue("phase", "begin");
                taskState = TaskState.Ongoing;

                // Wait for participant to leave start.
                // When participant leaves the start, set phase to out.
                yield return new WaitUntil(() => !startTriggers.isReturned);
                session.CurrentTrial.settings.SetValue("phase", "out");

                // Wait for grasp.
                // When participant grasps object, set phase to lift.
                yield return new WaitUntil(() => taskState == TaskState.Lift);
                session.CurrentTrial.settings.SetValue("phase", "lift");

                // Wait until the object has entered the target space above the original position.
                // When this happens, set phase to replace.
                yield return new WaitUntil(() => taskState == TaskState.Replace);
                session.CurrentTrial.settings.SetValue("phase", "replace");
                PlaySound("select");

                // Wait until the object has been replaced.
                // When this happens, set phase to return.
                yield return new WaitUntil(() => taskState == TaskState.Return);
                session.CurrentTrial.settings.SetValue("phase", "return");
                PlaySound("select");

                // Wait until prongs/fingers have returned to starting location.
                // When this happens, set phase to returned.
                yield return new WaitUntil(() => startTriggers.isReturned);
                session.CurrentTrial.settings.SetValue("phase", "returned");
                taskState = TaskState.End;

                // Play a tone to signal the end of the block if it is the last trial.
                PlaySound(session.CurrentTrial == session.CurrentBlock.lastTrial ? "final" : "select");


                //Record an additional .5 seconds to buffer out filter distortion.
                yield return new WaitForSeconds(.5f);

                if (trackersEnabled) EndTrackerRecording();

                // If this is the last trial, set up the next and remove the reaching task stimulus.
                if(session.CurrentTrial == session.CurrentBlock.lastTrial)
                {
                    _instructionsDisplay.DisplayCanvas();
                    _taskOngoing = false;
                    isDuringBlock = false;
                    _reachingPanel.enabled = false;
                    _trialText.text = "";
                    GameObject.Find("liftTarget").GetComponent<MeshRenderer>().enabled = false;
                    _instructionAudio.PlayAudioSequentially(postTaskAudio);
                    yield return new WaitUntil(()=> _instructionAudio.IsFinished());
                }


                session.EndCurrentTrial();
            }

            _stimuliSetup.RemoveStimuli();
            yield return new WaitForSeconds(5f);
            _trackerControlHub.DisableTrackers();
            yield break;
        }

    private void AssignTaskColliders()
    {
        if (isTool)
        {
            AddFingerTipColliders(participantAvatarRoot);
            UXF.Utilities.UXFDebugLog("Detected as tool reach.");
            _targetCollider = "leftClamp";
            _targetColliderTwo = "rightClamp";
            _taskStart = GameObject.Find("toolStartPoint");
            _taskStartLeft = GameObject.Find("leftStartPoint");
        }
        else
        {
            AddFingerTipColliders(participantAvatarRoot);
            UXF.Utilities.UXFDebugLog("Detected as manual reach.");
            _targetCollider = "Bip01 R Finger0 Tip";
            _targetColliderTwo = "Bip01 R Finger1 Tip";
            _taskStart = GameObject.Find("thumbStartPoint");
            _taskStartLeft = GameObject.Find("leftStartPoint");
        }
    }

    private void EndTrackerRecording()
    {
        if(session.settings.GetBool("record_data") == false) return;
        if (isTool)
        {
            pptKinematicsTool.StopRecording();
            steamVRKinematicsTool.StopRecording();
            _manusKinematics.StopRecording();
        }
        else
        {
            pptKinematics.StopRecording();
            steamVRKinematics.StopRecording();
            _manusKinematics.StopRecording();
        }
        if(session.settings.GetBool("eye_tracking_enabled"))
        {
            eyeTracker.StopRecording();
        }
    }

    private bool InitTrackerRecording()
    {
        if(session.settings.GetBool("record_data") == false) return true;
        DateTime startTime = DateTime.UtcNow;

            if (isTool)
            {
                try{
                    pptKinematicsTool.StartRecording(startTime);
                }
                catch(Exception e)
                {
                    UXF.Utilities.UXFDebugLog("Error starting pptKinematicsTool: " + e.Message);
                }
                try{
                    steamVRKinematicsTool.StartRecording(startTime);
                } catch(Exception e)
                {
                    UXF.Utilities.UXFDebugLog("Error starting steamVRKinematicsTool: " + e.Message);
                }
            }
            else
            {
                try{
                    pptKinematics.StartRecording(startTime);
                                }
                catch(Exception e)
                {
                    UXF.Utilities.UXFDebugLog("Error starting pptKinematics: " + e.Message);
                }
                try{
                    steamVRKinematics.StartRecording(startTime);
                }
                catch(Exception e)
                {
                    UXF.Utilities.UXFDebugLog("Error starting steamVRKinematics: " + e.Message);
                }
            }

        try{
            var skeleton = BoneUtilities.SearchHierarchyForBone(participantAvatarRoot, "Bip01 R Hand").GetComponent<Manus.Skeletons.Skeleton>();
            _manusKinematics.manusSkeletons = new List<Manus.Skeletons.Skeleton>(){skeleton};
            _manusKinematics.StartRecording(startTime);
        }
        catch(Exception e)
        {
            UXF.Utilities.UXFDebugLog("Error starting manusKinematics: " + e.Message);
        }
        try{
        if(session.settings.GetBool("eye_tracking_enabled"))
        {
            eyeTracker.StartRecording(startTime);
        }
        }
        catch(Exception e)
        {
            UXF.Utilities.UXFDebugLog("Error starting Eye Tracker recording: " + e.Message);
            return false;
        }
        return true;
    }

    private int CalculateTotalTrials(int startTrial)
    {
        int totalTrialNum;
        var isDebug = session.ppid == "test";
        if (isDebug)
        {
            totalTrialNum = 2;
        }
        else if (isTool)
        {
            if (session.currentBlockNum == 0)
            {
                totalTrialNum = 48 - (startTrial - 1);
            }
            else
            {
                totalTrialNum = 48;
            }
        }
        else
        {
            if (session.currentBlockNum == 0)
            {
                totalTrialNum = 18 - (startTrial - 1);
            }
            else
            {
                totalTrialNum = 18;
            }
        }

        return totalTrialNum;
    }

    /// <summary>
    /// Adds trigger colliders to the tips of the thumb and index finger of the hands for the reaching task, if these colliders are not already present.
    /// </summary>
    /// <param name="avatar">The Avatar root</param>
    private static void AddFingerTipColliders(Transform avatar){

        List<Transform> bones = new List<Transform>(){
            BoneUtilities.SearchHierarchyForBone(avatar, "Bip01 R Finger0 Tip"),
            BoneUtilities.SearchHierarchyForBone(avatar, "Bip01 L Finger0 Tip"),
            BoneUtilities.SearchHierarchyForBone(avatar, "Bip01 R Finger1 Tip"),
            BoneUtilities.SearchHierarchyForBone(avatar, "Bip01 L Finger1 Tip"),
        };

        foreach (Transform bone in bones)
        {
            if (bone.GetComponent<Collider>() != null) continue;
            var sphereCollider = bone.gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.01f;
            sphereCollider.isTrigger = true;
        }
    }

    /// <summary>
    /// Toggles the anti-scale option on the Manus Skeletons, as sometimes the Manus Skeletons become scaled incorrectly.
    /// </summary>
    /// <returns></returns>
    private IEnumerator ToggleManusScaleBones()
    {
        var manusSkeletonR = BoneUtilities.SearchHierarchyForBone(participantAvatarRoot, "Bip01 R Hand").GetComponent<Manus.Skeletons.Skeleton>();
        var manusSkeletonL = BoneUtilities.SearchHierarchyForBone(participantAvatarRoot, "Bip01 L Hand").GetComponent<Manus.Skeletons.Skeleton>();
        manusSkeletonR.skeletonData.settings.scaleToTarget = false;
        manusSkeletonL.skeletonData.settings.scaleToTarget = false;

        yield return new WaitForSeconds(0.5f);

        manusSkeletonR.skeletonData.settings.scaleToTarget = true;
        manusSkeletonL.skeletonData.settings.scaleToTarget = true;

        yield return null;
    }

    private void Start()
        {
            _reachingCanvas = GameObject.Find("reachCanvas");
            _reachingPanel = _reachingCanvas.GetComponentInChildren<Image>();
            _trialText = _reachingCanvas.GetComponentInChildren<TextMeshProUGUI>();
        }
}
