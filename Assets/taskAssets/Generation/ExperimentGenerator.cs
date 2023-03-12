using System;
using System.Linq;
using System.Security.Cryptography;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UXF;
using UnityEngine.SceneManagement;
using ViveSR.anipal.Eye;


public class ExperimentGenerator : MonoBehaviour
{
    private readonly System.Random _rng = new System.Random();
    private bool _shuffleFailed;
    public GameObject eyeTracker;


        /// <summary>
        /// Shuffles the landmark targets with the constraint that no two consecutive trials has the same target.
        /// </summary>
        private string[] ShuffleTarget(string[] targetArray)
        {
            string[] baseArray = (string[])targetArray.Clone();

            bool success = false;
            while(!success)
            {
                string[] loopArray = (string[])baseArray.Clone();
                bool failure = false;
                for(int i = 0; i < loopArray.Length; i++)
                {
                    int j = _rng.Next(i, loopArray.Length);
                    var tries = 0;
                    if(i > 0)
                    {
                        while(loopArray[i-1] == loopArray[j])
                        {
                            j = _rng.Next(i, loopArray.Length);
                            tries++;
                            
                            if (tries <= 5000) continue;
                            //UXF.Utilities.UXFDebugLogWarning("Shuffle failed at index" + i.ToString());
                            failure = true;
                            break;
                        }
                    }
                    (loopArray[i], loopArray[j]) = (loopArray[j], loopArray[i]);
                }

                if (failure) continue;
                success = true;
                baseArray = (string[])loopArray.Clone();
            }

            UXF.Utilities.UXFDebugLog(string.Join(", ", baseArray));
            return (string[])baseArray.Clone();
        }
        
        /// <summary>
        /// Shuffles the landmark targets with two constraints:
        /// 1. No two consecutive trials has the same target.
        /// 2. Each target is presented at least once before the set is repeated.
        /// </summary>
        /// <param name="targetArray">A string array of targets</param>
        /// <returns>Shuffled targets</returns>
        private static string[] ShuffleTargetSequences(string[] targetArray)
        {
            // create a list of landmarks
            var targets = new List<string> { "Wrist", "Elbow", "Forearm" };
            // shuffle the list
            targets.Shuffle();
            // iterate through the list and create the order
            string lastTarget = "";
            List<string> targetList = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                targets.Shuffle();
                // If the last target in targetList is the same as the first in targets, shuffle targets again
                while (lastTarget == targets[0])
                {
                    targets.Shuffle();
                }
                targetList.AddRange(targets);
                lastTarget = targetList[^1];
            }

            return targetList.ToArray();
        }

 
        /// <summary>
        /// Handles the generation and order of the experiment's conditions and trials, as well as shuffling the targets in the landmark task.
        /// </summary>
        /// <param name="session"></param>
        public void Generate(Session session)
        {
            if(SceneManager.GetSceneByName("participantCalibration").isLoaded)
            {
                SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("participantCalibration"));
            }
            
            // r code for generating the condition order
            //set.seed(1337)
            //conditionvector <- c(rep(1, 6), rep(2, 6), rep(3, 6), rep(4, 6))
            //random_assignment <- sample(conditionvector, 24)
            //paste(random_assignment, collapse = ",")
            
            int[] order = {2,4,3,3,3,4,1,1,3,2,3,1,1,1,2,2,4,1,4,4,4,2,2,3};
            
            string[] landmarkTargetPre = ShuffleTargetSequences(new string[18]);
            UXF.Utilities.UXFDebugLog("Landmark target pre: " + string.Join(", ", landmarkTargetPre));
            
            string[] landmarkTargetPost = ShuffleTargetSequences(new string[18]);
            UXF.Utilities.UXFDebugLog("Landmark target post: " + string.Join(", ", landmarkTargetPost));

            List<int> numTrials = session.settings.GetIntList("num_trials");
            List<string> expTask = session.settings.GetStringList("task");
            int pNumber = Convert.ToInt32(session.participantDetails["id"]);
            if(session.ppid == "test")
            {
                session.settings.SetValue("start_delay", 1);
                session.settings.SetValue("instruction_delay", 0);
                numTrials = new List<int>{2, 2, 2};
            }
            if(session.settings.GetBool("eye_tracking_enabled") & GameObject.Find("SRanipal Eye Framework") == null) 
            {
                eyeTracker = Instantiate(eyeTracker, Vector3.zero, Quaternion.identity);
                eyeTracker.GetComponent<SRanipal_Eye_Framework>().EnableEyeVersion = SRanipal_Eye_Framework.SupportedEyeVersion.version2;
            }


            session.settings.SetValue("participantAvatar", PlayerPrefs.GetString("Chosen Avatar"));
            session.participantDetails.Add("avatar_sex", PlayerPrefs.GetString("AvatarSex", "None"));
            session.participantDetails.Add("avatar_number", PlayerPrefs.GetString("AvatarNumber", "None"));
            session.participantDetails.Add("participant_height", PlayerPrefs.GetFloat("ParticipantHeight"));
            session.participantDetails.Add("participant_wrist_to_wrist", PlayerPrefs.GetFloat("ParticipantWristToWrist"));
            session.participantDetails.Add("right_wrist_target_x",  PlayerPrefs.GetFloat("wristTargetRightX"));
            session.participantDetails.Add("right_wrist_target_y",  PlayerPrefs.GetFloat("wristTargetRightY"));
            session.participantDetails.Add("right_wrist_target_z",  PlayerPrefs.GetFloat("wristTargetRightZ"));
            session.participantDetails.Add("right_elbow_target_x",  PlayerPrefs.GetFloat("elbowTargetRightX"));
            session.participantDetails.Add("right_elbow_target_y",  PlayerPrefs.GetFloat("elbowTargetRightY"));
            session.participantDetails.Add("right_elbow_target_z",  PlayerPrefs.GetFloat("elbowTargetRightZ"));
            session.participantDetails.Add("right_upperarm_target_x",  PlayerPrefs.GetFloat("upperArmTargetRightX"));
            session.participantDetails.Add("right_upperarm_target_y",  PlayerPrefs.GetFloat("upperArmTargetRightY"));
            session.participantDetails.Add("right_upperarm_target_z",  PlayerPrefs.GetFloat("upperArmTargetRightZ"));

            //Generate blocks: 5 for this experiment, given two separate measurement paradigms to be
            //applied before and after tool-use.
            Block pre1 = session.CreateBlock();
            Block pre2 = session.CreateBlock();
            Block expBlock = session.CreateBlock();
            Block post1 = session.CreateBlock();
            Block post2 = session.CreateBlock();


            switch (order[pNumber-1])
            {
                // Establish four possible condition orders.
                case 1:
                    pre1.settings.SetValue("task", "manual_reach");
                    pre2.settings.SetValue("task", "landmark");
                    post1.settings.SetValue("task", "landmark");
                    post2.settings.SetValue("task", "manual_reach");
                    break;
                case 2:
                    pre1.settings.SetValue("task", "landmark");
                    pre2.settings.SetValue("task", "manual_reach");
                    post1.settings.SetValue("task", "manual_reach");
                    post2.settings.SetValue("task", "landmark");
                    break;
                case 3:
                    pre1.settings.SetValue("task", "manual_reach");
                    pre2.settings.SetValue("task", "landmark");
                    post1.settings.SetValue("task", "manual_reach");
                    post2.settings.SetValue("task", "landmark");
                    break;
                case 4:
                    pre1.settings.SetValue("task", "landmark");
                    pre2.settings.SetValue("task", "manual_reach");
                    post1.settings.SetValue("task", "landmark");
                    post2.settings.SetValue("task", "manual_reach");
                    break;
            }

            expBlock.settings.SetValue("task", "tool_reach");

            // Trial Generation. First two blocks are baseline measurements of the estimation and reaching paradigms.
            // Next block is the experimental block, tool-use.
            // Experimental block followed by the post-test measurements.
            foreach(Block block in session.blocks)
            {
                if(block == pre1 || block == pre2){
                    block.settings.SetValue("stage", "baseline");
                    for(int n = 0; n < numTrials[0]; n++)
                    {
                        Trial newTrial = block.CreateTrial();
                        if(n == 0){
                         newTrial.settings.SetValue("show_intro", "true");   
                        } else{newTrial.settings.SetValue("show_intro", "false");}
                        if(block.settings.GetString("task") == "landmark"){
                            newTrial.settings.SetValue("landmarkTarget", landmarkTargetPre[n]);
                        }
                    }
                }
                else if(block == expBlock)
                {
                    block.settings.SetValue("stage", "experimental");
                    for(int n = 0; n < numTrials[1]; n++)
                    {
                        Trial newTrial = block.CreateTrial();
                        if(n == 0){
                         newTrial.settings.SetValue("show_intro", "true");   
                        } else{newTrial.settings.SetValue("show_intro", "false");}
                    }
                }
                else if(block == post1 || block == post2)
                {
                    block.settings.SetValue("stage", "post-test");
                    for(int n = 0; n < numTrials[2]; n++)
                    {
                        Trial newTrial = block.CreateTrial();
                        if(n == 0){
                         newTrial.settings.SetValue("show_intro", "true");   
                        } else{newTrial.settings.SetValue("show_intro", "false");}
                        if(block.settings.GetString("task") == "landmark"){
                            newTrial.settings.SetValue("landmarkTarget", landmarkTargetPost[n]);
                        }
                    }
                }
            }


        }

    /// <summary>
    /// Handles the adjustment of the starting trial, for sessions following a crash.
    /// </summary>
    /// <param name="session">The experiment session object.</param>
    public void ChangeStartingPoint(Session session) 
    {
        int startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
        int startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
        session.blocks.RemoveAll(x => x.number < startBlock);
        
        foreach(Block i in session.blocks)
        {
            UXF.Utilities.UXFDebugLog("block " + i.number + " is " + i.settings.GetString("task"));
            
            if (!(i.number == 1 & startTrial > 1)) continue;
            i.trials.RemoveAll(t => t.numberInBlock < startTrial);
            UXF.Utilities.UXFDebugLog("block " + i.number + " contains " + i.trials.Count);
        }
    }
}
