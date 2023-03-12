using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UXF;
public class StimuliSetup : MonoBehaviour
{
    // Reference the session and GameObjects that need to be spawned at start of blocks/trials.
    public Session session;
    public GameObject findTracker, beginBlock;
    public GameObject tableTop;
    public GameObject startPoint, toolStartPoint, fingerStartPoint, leftStartPoint, armPlacement;
    private GameObject _trackerConnectionHub;
    public GameObject goalLocation;
    public GameObject landmarkInstructions, landmarkStimulus;
    public GameObject reachingStimulus;
    public TaskIndicators landmarkAlignment, landmarkStartIndicator, landmarkLengthIndicator, landmarkLengthText;

    /// <summary>
    /// Handles the activation and presentation of the objects relevant to the current task.
    /// </summary>
    public void ArrangeStimuli()
    {
        //Only introduces and positions stimuli if it is the first trial in the block.
        if (session.currentTrialNum != 0 && session.CurrentTrial != session.CurrentBlock.lastTrial) return;
        // Pull settings from experiment settings.
        var experimentalTask = session.blocks[session.currentBlockNum].settings.GetString("task");

        switch (experimentalTask)
        {
            case "manual_reach":
            case "tool_reach":
            {
                ReachingSetup(experimentalTask);
                break;
            }
            case "landmark":
            {
                LandmarkSetup();
                break;
            }
        }

    }

    private void ReachingSetup(string experimentalTask)
    {
        startPoint.SetActive(true);
        goalLocation.SetActive(true);
        leftStartPoint.SetActive(true);
        leftStartPoint.GetComponent<MeshRenderer>().enabled = true;
        goalLocation.GetComponent<MeshRenderer>().enabled = true;
        foreach (var r in reachingStimulus.GetComponentsInChildren<Renderer>())
        {
            r.enabled = true;
        }

        switch (experimentalTask)
        {
            case "manual_reach":
                fingerStartPoint.SetActive(true);
                break;
            case "tool_reach":
                toolStartPoint.SetActive(true);
                break;
        }
    }

    private void LandmarkSetup()
    {
        armPlacement.SetActive(true);
        armPlacement.GetComponent<Renderer>().enabled = true;
        goalLocation.SetActive(false);
        startPoint.SetActive(false);
        landmarkStimulus.SetActive(true);
        landmarkStimulus.GetComponent<OscillateStimulus>().enabled = true;
        landmarkStartIndicator.EnableIndicator();
        landmarkAlignment.EnableIndicator();
        if (session.currentBlockNum + ((int)session.participantDetails["startblock"] - 1) > 2)
        {
            landmarkLengthIndicator.EnableIndicator();
            landmarkLengthText.EnableIndicator();
        }
    }


    /// <summary>
    /// Handles the deactivation and removal of stimuli following the completion of an experimental block.
    /// </summary>
    public void RemoveStimuli()
    {
        //Only removes stimuli if the block is over.
        if (!(session.currentBlockNum > 0 & session.CurrentTrial == session.CurrentBlock.lastTrial)) return;
        //Remove stimuli/objects from participants' view at the end of the block
            var experimentalTask = session.CurrentBlock.settings.GetString("task");

            switch (experimentalTask)
            {
                case "manual_reach":
                case "tool_reach":
                {
                    ReachingCleanup(experimentalTask);

                    break;
                }
                case "landmark":
                    LandmarkCleanup();
                    break;
            }
    }



    private void ReachingCleanup(string experimentalTask)
    {
        startPoint.SetActive(false);
        leftStartPoint.SetActive(false);
        goalLocation.SetActive(false);
        foreach (var r in reachingStimulus.GetComponentsInChildren<Renderer>())
        {
            r.enabled = false;
        }

        switch (experimentalTask)
        {
            case "manual_reach":
                fingerStartPoint.SetActive(false);
                break;
            case "tool_reach":
                toolStartPoint.SetActive(false);
                break;
        }
    }
    
    private void LandmarkCleanup()
    {
        landmarkStartIndicator.DisableIndicator();
        landmarkAlignment.DisableIndicator();
        armPlacement.SetActive(false);
        landmarkInstructions.SetActive(false);
        landmarkStimulus.SetActive(false);
    }

    /// <summary>
    /// Determines whether the correct trackers have been connected and initiates the block if so.
    /// </summary>
    public void BeginFirstTrial() {

        var nextTask = session.blocks[session.currentBlockNum].settings.GetString("task");
        // if(!session.settings.GetBool("testing_mode"))
        // {
        //     _trackerConnectionHub = GameObject.Find("trackerPanels");
        //     var connectionArray = _trackerConnectionHub.GetComponentsInChildren<TrackerConnected>();
        //     var numFound = 0;
        //     var numRequired = 0;
        //     foreach (var t in connectionArray)
        //     {
        //         switch (nextTask)
        //         {
        //             case "landmark":
        //             {
        //                 numRequired = 2;
        //                 if (t.tracker is TrackerConnected.TrackerEnum.ElbowSteamVR
        //                     or TrackerConnected.TrackerEnum.WristSteamVR)
        //                 {
        //                     numFound++;
        //                 }

        //                 break;
        //             }
        //             case "manual_reach":
        //             {
        //                 numRequired = 5;
        //                 if (t.tracker is TrackerConnected.TrackerEnum.WristShaftPpt
        //                     or TrackerConnected.TrackerEnum.WristSteamVR
        //                     or TrackerConnected.TrackerEnum.LeftProngPpt
        //                     or TrackerConnected.TrackerEnum.IndexRightProngPpt
        //                     or TrackerConnected.TrackerEnum.StimulusRectangle)
        //                 {
        //                     numFound++;
        //                 }

        //                 break;
        //             }
        //             default:
        //             {
        //                 numRequired = 5;
        //                 if (t.tracker is TrackerConnected.TrackerEnum.WristShaftPpt
        //                     or TrackerConnected.TrackerEnum.LeftProngPpt
        //                     or TrackerConnected.TrackerEnum.IndexRightProngPpt
        //                     or TrackerConnected.TrackerEnum.StimulusRectangle
        //                     or TrackerConnected.TrackerEnum.ToolSteamVR)
        //                 {
        //                     numFound++;
        //                 }

        //                 break;
        //             }
        //         }
        //     }

        //     var allFound = numFound == numRequired;
        //     if (session.InTrial || !allFound) return;
        // }
        ArrangeStimuli();
        switch(nextTask)
        {
            case "landmark":
                landmarkStimulus.GetComponent<LandmarkEstimateTracker>().StartRecording();
                break;
            case "manual_reach":
                reachingStimulus.GetComponent<ReachingTask>().isTool = false;
                reachingStimulus.GetComponent<ReachingTask>().StartCoroutine("ReachingTaskHandler");
                break;
            case "tool_reach":
                reachingStimulus.GetComponent<ReachingTask>().isTool = true;
                reachingStimulus.GetComponent<ReachingTask>().StartCoroutine("ReachingTaskHandler");
                break;
            default:
                break;
        }
        
        findTracker.SetActive(false);
        beginBlock.SetActive(false);
    }
}
