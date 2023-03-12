using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using UXF;
using Valve.VR;

public class InstructionsDisplay : MonoBehaviour
{
    #region participanttext
    public Canvas instructionsCanvas;
    public Session session;   
    public SteamVR_Action_Boolean clickWatcher;
    public SteamVR_Action_Boolean triggerWatcher;
    public SteamVR_Input_Sources handType;
    private VideoPlayer _instructionVideoPlayer;
    private TextMeshProUGUI _preStartText, _endBlockText, _replayText, _continueText, _endStudyText;
    private TextMeshProUGUI _landmarkText, _manualReachingText, _toolReachingText;
    [Tooltip("Text that indicates current landmark target..")]
    public TextMeshProUGUI landmarkTarget;
    [Tooltip("Appears during landmark task to display target of current trial.")]
    public GameObject landmarkCanvas;
    [Tooltip("Reference to the oscillating landmark sphere.")]
    private GameObject _landmarkStimulus;
    public Camera hmdCamera, monitorCamera;
    private string _currentTask;
    private bool _isClicked, _isPulled, _isReady, _isAwaitingInput;

    private void ClickDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            Utilities.UXFDebugLog("Click registered.");
            if(_isAwaitingInput)
            {
                    _isClicked = true;
            }
        }

    private void TriggerDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Utilities.UXFDebugLog("Trigger click registered.");
            if(_isAwaitingInput)
            {
                    _isPulled = true;
            }
    }

    /// <summary>
    /// Displays instructions to the participant, yielding progression until the participant confirms through clicking the trackpad. Participants press the trigger to replay instructions.
    /// </summary>
    public IEnumerator InstructionsCoroutine(string context)
    {
        //Check for controller inputs.
        clickWatcher.AddOnStateDownListener(ClickDown, handType);
        triggerWatcher.AddOnStateDownListener(TriggerDown, handType);
        yield return new WaitForSeconds(1f);

        //Remove pre-block text.
        _preStartText.enabled = false;
        _endBlockText.enabled = false;


        _isReady = false;
        _isAwaitingInput = false;

        var again = false;
        //While loop repeats if participants request instructions be replayed.
        while(!_isReady)
        {
            PresentInstructionText(context);

            PlayInstructionVideo(context);
            
            //If this is the first time through, ensure they watch the full video.
            if(!again)
            {
                // Wait for video to finish. In the second block, participants could skip the video with a correct recounting
                // of the instructions and a verbally consistent summary from the researcher.
                yield return new WaitUntil(() => !_instructionVideoPlayer.isPlaying || Keyboard.current.spaceKey.isPressed);
                if(Keyboard.current.spaceKey.isPressed)
                {
                    _instructionVideoPlayer.Stop();
                }

                _videoDisplay.enabled = false;
            }

            //Display choice of continuing or replaying instructions to participant.
            _continueText.enabled = true;
            _replayText.enabled = true;
            _isAwaitingInput = true;
            
            yield return new WaitUntil(() => _isClicked || _isPulled);
            
            EndInstructions(context);
            
            _isClicked = false;
            _isPulled = false;
            _isAwaitingInput = false;
            again = true;
        }
        instructionsCanvas.enabled = false;
        
        clickWatcher.RemoveOnStateDownListener(ClickDown, handType);
        triggerWatcher.RemoveOnStateDownListener(TriggerDown, handType);
        yield break;
    }

    private void PresentInstructionText(string context)
    {
        switch (context)
        {
            case "manual_reach":
                _manualReachingText.enabled = true;
                break;
            case "tool_reach":
                _toolReachingText.enabled = true;
                break;
            default:
                _landmarkText.enabled = true;
                break;
        }
    }

    private void EndInstructions(string context)
    {
        //If participants advance, clean text and enable necessary gameobjects.
        if (_isClicked)
        {
            switch (context)
            {
                case "landmark":
                    _landmarkText.enabled = false;
                    landmarkCanvas.SetActive(true);
                    break;
                case "manual_reach":
                    _manualReachingText.enabled = false;
                    break;
                default:
                    _toolReachingText.enabled = false;
                    break;
            }

            //If the video is still playing (i.e., if participant watched it again and ended early), stop it.
            if (_instructionVideoPlayer.isPlaying)
            {
                _instructionVideoPlayer.Stop();
            }

            audioSource.PlayOneShot(advance);
            _isReady = true;
        }
        //If the trigger is pulled, do nothing, allowing the loop to repeat.
        else if (_isPulled)
        {
            _isPulled = false;
        }

        _videoDisplay.enabled = false;
        _continueText.enabled = false;
        _replayText.enabled = false;
    }

    /// <summary>
    /// Displays the "projector screen" instruction canvas to participants.
    /// </summary>
    public void DisplayCanvas()
    {  
        // If the block number equals or exceeds the amount specified by the experiment, the next task is debriefing.
        _currentTask = session.currentBlockNum < session.blocks.Count ? session.blocks[session.currentBlockNum].settings.GetString("task") : "debrief";

        instructionsCanvas.enabled = true;
       if(session.currentTrialNum == 0 || (session.CurrentTrial == session.CurrentBlock.lastTrial & session.CurrentTrial != session.LastTrial))
       {
           if(session.currentBlockNum > 0)
           {
               _endBlockText.enabled = true;
           } else
           {
               _preStartText.enabled = true;
           }
       } else if(session.CurrentTrial == session.LastTrial)
       {
            gameObject.GetComponent<taskAssets.Instructions.InstructionAudio>().PlayAudioSequentially(endingAudio);
           _endStudyText.enabled = true;
       }
    }
    /// <summary>
    /// Updates the targeted landmark during the landmark task.
    /// </summary>
    public void LandmarkTargetUpdate()
    {
        var firsttrialtarget = session.blocks[0].firstTrial.settings.GetString("landmarkTarget");
        var nexttrialtarget = session.NextTrial.settings.GetString("landmarkTarget");
        landmarkTarget.text = session.currentTrialNum == 0 ? firsttrialtarget : nexttrialtarget;
    }

    #endregion

    #region participantaudio
    public AudioSource audioSource;
    public AudioClip advance;
    public VideoClip landmarkVideo, manualGraspVideo, toolGraspVideo;
    private RawImage _videoDisplay;

    /// <summary>
    /// Plays the correct instruction video for the upcoming task.
    /// </summary>
    private void PlayInstructionVideo(string context)
    {
        switch (context)
        {
            case "landmark":
                _videoDisplay.enabled = true;
                _instructionVideoPlayer.clip = landmarkVideo;
                _instructionVideoPlayer.Play();
                break;
            case "manual_reach":
                _videoDisplay.enabled = true;
                _instructionVideoPlayer.clip = manualGraspVideo;
                _instructionVideoPlayer.Play();
                break;
            default:
                _videoDisplay.enabled = true;
                _instructionVideoPlayer.clip = toolGraspVideo;
                _instructionVideoPlayer.Play();
                break;
        }
    }

    #endregion

    #region researchertext

    #if UNITY_STANDALONE
    public GameObject researcherCanvas, trackerPanelHub, researcherCamera;
    private Image _researcherPanel, _landmarkAlignment, _landmarkLength, _landmarkStart;
    private TextMeshProUGUI _researcherCurrentTask, _researcherNextTask, _researcherBlockNumber, _researcherTrialNumber, _researcherReachingPhase, _landmarkLengthText;
    private TextMeshProUGUI _researcherIntroInstructions, _researcherLandmarkInstructions, _researcherLandmarkIndicators, _researcherToolInstructions, _researcherManualReachInstructions, _trackerTurnoffChecklist;
    private GameObject _introCompleted, _findTracker, _beginBlock, _endSession, _researcherTrialStartButton;

    //Tracker activity indicators
    private TrackerConnected _wristShaftPpt, _leftProngPpt, _indexRightProngPpt, _elbowSteamVR, _wristSteamVR, _toolSteamVR, _originSteamVR, _backSteamVR, _stimulusRectangle; 


    /// <summary>
    /// Shows the current and next tasksm as well as updating trial number each time it is run.
    /// </summary>
    private void ResearcherUpdateTasksandNumbers() {
        if(researcherCamera.activeSelf == false) return;
        if(session.currentBlockNum != 0)
        {
            _researcherBlockNumber.text = "Block: " + session.currentBlockNum.ToString() + "/5";
            _researcherTrialNumber.text = "Trial: " + session.CurrentTrial.numberInBlock.ToString() + "/" + session.CurrentBlock.trials.Count.ToString();
            _researcherCurrentTask.text = "Current Task: " + session.CurrentBlock.settings.GetString("task");
            if(session.CurrentBlock.settings.GetString("task") != "landmark")
            {
                _researcherReachingPhase.text = "Current Reaching Phase: " + session.CurrentTrial.settings.GetString("phase");
            }
        }
        var startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
        if(session.currentBlockNum < session.blocks.Count)
        {
            _researcherNextTask.text = "Next Task: " + session.blocks[session.currentBlockNum].settings.GetString("task");
        } else 
        {
            _researcherNextTask.text = "Next Task: Survey and Debrief";
        }
    }

    public void ResearcherFinishIntro() {
        if(researcherCamera.activeSelf == false) return;
        _introCompleted.SetActive(false);
        ResearcherUpdateTasksandNumbers();
        ResearcherIntermissionInstructions();
    }

    /// <summary>
    /// Shows the upcoming task and which trackers to deactivate/activate for it.
    /// </summary>
    private void ResearcherIntermissionInstructions() {
        if(researcherCamera.activeSelf == false) return;

        _researcherPanel.enabled = true;
        
        var nextTask = session.currentBlockNum < session.blocks.Count ? session.blocks[session.currentBlockNum].settings.GetString("task") : "debrief";
        
        if(session.currentBlockNum > 0)
        {
            var currentTask = session.CurrentBlock.settings.GetString("task");
            
            if(currentTask == "landmark" & nextTask == "tool_reach")
            {
                _trackerTurnoffChecklist.SetText("");
            } else if(currentTask == "manual_reach" && nextTask == "tool_reach")
            {
                _trackerTurnoffChecklist.SetText("Turn off: \n" +
                                                "<indent=3em>\u2022 Wrist (PPT)\n" +
                                                "\u2022 Fingers (PPT)</indent>");
            } else if(currentTask == "manual_reach" && nextTask == "landmark")
            {
                _trackerTurnoffChecklist.SetText("Turn off: \n" +
                                                "<indent=3em>\u2022 Wrist (PPT)</indent>\n" +
                                                "Turn off: \n" +
                                                "<indent=3em>\u2022 Finger (PPT)</indent>");
            } else if(currentTask == "landmark" && nextTask == "manual_reach")
            {
                _trackerTurnoffChecklist.SetText("");
            } else if(currentTask == "tool_reach")
            {
                _trackerTurnoffChecklist.SetText("Turn off: \n" +
                                                "<indent=3em>\u2022 Tool (PPT)\n" +
                                                "\u2022 Tool (SteamVR)</indent>");
            } else
            {
                _trackerTurnoffChecklist.SetText("");
            }
        }

        _researcherIntroInstructions.enabled = false;
        
        UXF.Utilities.UXFDebugLog("Next Task: " + nextTask);

        if(nextTask != "debrief")
        {
            _beginBlock.SetActive(true);
            _findTracker.SetActive(true);
        }
        switch (nextTask)
        {
            case "manual_reach":
                _researcherManualReachInstructions.enabled = true;
                _wristSteamVR.BeginSearching();
                _leftProngPpt.BeginSearching();
                _wristShaftPpt.BeginSearching();
                _indexRightProngPpt.BeginSearching();
                _stimulusRectangle.BeginSearching();
                break;
            case "tool_reach":
                _researcherToolInstructions.enabled = true;
                _toolSteamVR.BeginSearching();
                _leftProngPpt.BeginSearching();
                _wristShaftPpt.BeginSearching();
                _indexRightProngPpt.BeginSearching();
                _stimulusRectangle.BeginSearching();
                break;
            case "landmark":
            {
                _researcherLandmarkInstructions.enabled = true;
                _researcherLandmarkIndicators.enabled = true;
                _landmarkAlignment.enabled = true;
            
                _landmarkStart.enabled = true;
                if(session.currentBlockNum + ((int)session.participantDetails["startblock"] - 1) > 2)
                {
                    _landmarkLengthText.enabled = true;
                    _landmarkLength.enabled = true;
                }
                _wristSteamVR.BeginSearching();
                _elbowSteamVR.BeginSearching();
                break;
            }
            case "debrief":
                _researcherIntroInstructions.enabled = true;
                ResearcherEndingInstructions();
                _endSession.SetActive(true);
                break;
        }
    }

    public AudioClip[] endingAudio;

    public void ResearcherEndingInstructions() {
        _researcherIntroInstructions.text = "The Virtual Reality portion of the study has completed. Please help the participant remove the trackers and HMD, and give them the paper survey. Click \"End Session\" when the headset has been removed.";

    }

    /// <summary>
    /// When trials begin, hides researcher canvas and stops indicators.
    /// </summary>
    private void ResearcherTrialsUnderway() {
        if(researcherCamera.activeSelf == false) return;

        _researcherPanel.enabled = false;
        _trackerTurnoffChecklist.SetText("");
        
        if(_researcherManualReachInstructions.enabled)
        {
            _researcherManualReachInstructions.enabled = false;
            _wristSteamVR.EndSearching();
            _leftProngPpt.EndSearching();
            _wristShaftPpt.EndSearching();
            _indexRightProngPpt.EndSearching();
            _stimulusRectangle.EndSearching();
        }
        if(_researcherToolInstructions.enabled)
        {
            _researcherToolInstructions.enabled = false;
            _toolSteamVR.EndSearching();
            _leftProngPpt.EndSearching();
            _wristShaftPpt.EndSearching();
            _indexRightProngPpt.EndSearching();
            _stimulusRectangle.EndSearching();
        }
        if(_researcherLandmarkInstructions.enabled)
        {
            _researcherLandmarkInstructions.enabled = false;
            _researcherLandmarkIndicators.enabled = false;
            
            _landmarkAlignment.enabled = false;
            _landmarkStart.enabled = false;
            _landmarkLength.enabled = false;
            _landmarkLengthText.enabled = false;
            _wristSteamVR.EndSearching();
            _elbowSteamVR.EndSearching();
        } 
        _researcherIntroInstructions.enabled = true;
        _researcherIntroInstructions.text = "Please watch the participant, ensuring they adhere to instructions.";
    }
    /// <summary>
    /// On each trial, update the task and trial number.
    /// </summary>
    public void researcherInstructionsUpdateOnTrialBegin() {
        if(researcherCamera.activeSelf == false) return;
        ResearcherUpdateTasksandNumbers();
        ResearcherTrialsUnderway();
    }

    /// <summary>
    /// When the trial ends, check if it is the last in the block. If so, show researcher appropriate instructions.
    /// </summary>
    public void researcherInstructionsUpdateOnTrialEnd() {
        if(researcherCamera.activeSelf == false) return;
        
        if(session.CurrentTrial != session.CurrentBlock.lastTrial){
            return;
        } else if(session.CurrentTrial == session.CurrentBlock.lastTrial)
        {
            ResearcherIntermissionInstructions();
        }
    }

    public AudioClip openingAudio;

    /// <summary>
    /// Enable the researcher-facing canvas that displays active trackers and instructions.
    /// </summary>
    public void EnableResearcherCanvas()
    {
        if(researcherCamera.activeSelf == false) return;
        if(session.number == 1) gameObject.GetComponent<taskAssets.Instructions.InstructionAudio>().PlayAudio(openingAudio);
        researcherCanvas.GetComponent<Canvas>().enabled = true;
        AssignComponents();
    }
    #endif
    #endregion

    #region  Startup
    #if UNITY_STANDALONE
    /// <summary>
    /// Populate the private instruction variables with scene components.
    /// </summary>
    private void AssignComponents() {
        _instructionVideoPlayer = instructionsCanvas.GetComponentInChildren<VideoPlayer>();
        _videoDisplay = _instructionVideoPlayer.GetComponent<RawImage>();        

        var researcherPanelArray = researcherCanvas.GetComponentsInChildren<Image>(true);
        AssignResearcherPanels(researcherPanelArray);
        
        _researcherPanel = researcherCanvas.GetComponentInChildren<Image>(true);
        _researcherTrialStartButton = researcherCanvas.GetComponentInChildren<Button>(true).gameObject;
        
        var researcherTextArray = researcherCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        AssignResearcherText(researcherTextArray);
        
        var researcherButtons = researcherCanvas.GetComponentsInChildren<Button>(true);
        AssignResearcherButtons(researcherButtons);

        var participantInstructions = instructionsCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        AssignParticipantInstructions(participantInstructions);

        var trackerIndicators = trackerPanelHub.GetComponentsInChildren<TrackerConnected>(true);
        AssignTrackerIndicators(trackerIndicators);



    }

    private void AssignResearcherPanels(Image[] researcherPanelArray)
    {
        if(researcherCamera.activeSelf == false) return;

        foreach (var i in researcherPanelArray)
        {
            switch (i.name)
            {
                case "Panel":
                    _researcherPanel = i;
                    break;
                case "landmarkAlignmentPanel":
                    _landmarkAlignment = i;
                    break;
                case "landmarkLengthPanel":
                    _landmarkLength = i;
                    break;
                case "landmarkStartPanel":
                    _landmarkStart = i;
                    break;
                default:
                    UXF.Utilities.UXFDebugLog("No matching researcher panel in instructionsDisplay. Looking for: " +
                                              i.name);
                    break;
            }
        }
    }

    private void AssignTrackerIndicators(TrackerConnected[] trackerIndicators)
    {
        if(researcherCamera.activeSelf == false) return;

        foreach (var t in trackerIndicators)
        {
            switch (t.name)
            {
                case "wristShaftPPT":
                    _wristShaftPpt = t;
                    break;
                case "leftProngPPT":
                    _leftProngPpt = t;
                    break;
                case "indexRightProngPPT":
                    _indexRightProngPpt = t;
                    break;
                case "elbowSteamVR":
                    _elbowSteamVR = t;
                    break;
                case "wristSteamVR":
                    _wristSteamVR = t;
                    break;
                case "toolSteamVR":
                    _toolSteamVR = t;
                    break;
                case "originSteamVR":
                    _originSteamVR = t;
                    break;
                case "backSteamVR":
                    _backSteamVR = t;
                    break;
                case "stimulusRectangle":
                    _stimulusRectangle = t;
                    break;
                default:
                    break;
            }
        }
    }

    private void AssignParticipantInstructions(TextMeshProUGUI[] participantInstructions)
    {
        foreach (var t in participantInstructions)
        {
            switch (t.name)
            {
                case "reachingText":
                    _manualReachingText = t;
                    break;
                case "reachingTextTool":
                    _toolReachingText = t;
                    break;
                case "landmarkText":
                    _landmarkText = t;
                    break;
                case "preStartText":
                    _preStartText = t;
                    break;
                case "endBlockText":
                    _endBlockText = t;
                    break;
                case "continueText":
                    _continueText = t;
                    break;
                case "replayText":
                    _replayText = t;
                    break;
                case "endStudyText":
                    _endStudyText = t;
                    break;
            }
        }
    }

    private void AssignResearcherButtons(Button[] researcherButtons)
    {
        if(researcherCamera.activeSelf == false) return;

        foreach (var b in researcherButtons)
        {
            switch (b.name)
            {
                case "introCompletedButton":
                    _introCompleted = b.gameObject;
                    break;
                case "findTrackerButton":
                    _findTracker = b.gameObject;
                    break;
                case "beginBlockButton":
                    _beginBlock = b.gameObject;
                    break;
                case "endSessionButton":
                    _endSession = b.gameObject;
                    break;
                default:
                    UXF.Utilities.UXFDebugLog("No matching button in instructionsDisplay. Looking for: " + b.name);
                    break;
            }
        }
    }

    private void AssignResearcherText(TextMeshProUGUI[] researcherTextArray)
    {
        if(researcherCamera.activeSelf == false) return;

        foreach (var t in researcherTextArray)
        {
            switch (t.name)
            {
                case "researcherCurrentTask":
                    _researcherCurrentTask = t;
                    break;
                case "researcherNextTask":
                    _researcherNextTask = t;
                    break;
                case "researcherBlockNumber":
                    _researcherBlockNumber = t;
                    break;
                case "researcherTrialNumber":
                    _researcherTrialNumber = t;
                    break;
                case "researcherIntroInstructions":
                    _researcherIntroInstructions = t;
                    break;
                case "researcherLandmarkInstructions":
                    _researcherLandmarkInstructions = t;
                    break;
                case "researcherToolInstructions":
                    _researcherToolInstructions = t;
                    break;
                case "researcherManualReachInstructions":
                    _researcherManualReachInstructions = t;
                    break;
                case "researcherReachingPhase":
                    _researcherReachingPhase = t;
                    break;
                case "trackerTurnoffChecklist":
                    _trackerTurnoffChecklist = t;
                    break;
                case "researcherLandmarkIndicators":
                    _researcherLandmarkIndicators = t;
                    break;
                case "landmarkLengthText":
                    _landmarkLengthText = t;
                    break;
                default:
                    UXF.Utilities.UXFDebugLog("No matching researcher text in instructionsDisplay. Looking for: " + t.name);
                    break;
            }
        }
    }
    #endif
    #endregion


    public void EndStudy()
    {
        Application.Quit();
    }
}
