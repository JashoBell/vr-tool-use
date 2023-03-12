using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
using MovementTracking;
using taskAssets.Instructions;
using Valve.VR;

namespace UXF
{
    /// <summary>
    /// Attach this component to a gameobject and assign it in the trackedObjects field in an ExperimentSession to automatically record position/rotation of the object at each frame.
    /// </summary>
    public class LandmarkEstimateTracker : Tracker
    {
        public UXFDataRow[] TrialDataArray;
        public GameObject stimuli, experimentControl, armPlacement;
        [SerializeField] private AvatarHandler avatarHandler;
        private List<Transform> _landmarkEstimates, _landmarkTargets;
        public List<AudioClip> preTaskAudio, postTaskAudio;
        public UXFEyeTrackerFixations eyeTracker;
        public Session session;
        public Material estimateMaterial, lapseMaterial;
        [SerializeField] private Camera _researcherCamera;
        private InstructionsDisplay _instructionsDisplay;
        private TrackerControlHub _trackerControlHub;
        private StimuliSetup _stimuliSetup;
        private LandmarkStartTrigger _startTrigger;
        private OscillateStimulus _oscillateStimulus;
        private bool _trialUnderway;
        private float _error;
        private double _trialTime;
        private float _startPosition;
        private int _totalTrialNum, _currentTrialNum, _startTrial;
        public SteamVR_Action_Boolean clickWatcher;
        public SteamVR_Input_Sources handType;
        private bool _isClicked;
        private bool _isRunning;
        public Camera participantCamera;
        private List<Vector3> _wristEstimates;
        private List<Vector3> _elbowEstimates;


        /// <summary>
        /// Sets measurementDescriptor and customHeader to appropriate values
        /// </summary>
        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "estimates";

            customHeader = new string[]
            {
                //Trial and block information
                "participant",
                "trial",
                "block",
                //Tracker positions: w = wrist, e = elbow, f = forearm midpoint, s = stimuli
                "wpos_x",
                "wpos_y",
                "wpos_z",
                "epos_x",
                "epos_y",
                "epos_z",
                "fpos_x",
                "fpos_y",
                "fpos_z",
                "spos_x",
                "spos_y",
                "spos_z",
                "target",
                "error",
                "forearmlength",
                "perceivedforearmlength",
                "perceivedforearmlengthratio",
                "noisewrist",
                "noiseelbow",
                "startposition",
                "direction",
                "speed",
                "trialtime"
            };
        }

        /// <summary>
        /// Grabs the current positions of each landmark and the stimuli, as well as the error between the target and the selected position, when the participant clicks the touchpad.
        /// 
        /// </summary>
        /// <returns></returns>
        protected override UXFDataRow GetCurrentValues()
        {
            // get position of landmarks and stimulus
            var s = _oscillateStimulus.transform.position;
            _landmarkEstimates[_currentTrialNum - 1].position = s;
            var w = _oscillateStimulus.wristTransform.position;
            _landmarkTargets[0].position = w;
            var e = _oscillateStimulus.elbowTransform.position;
            _landmarkTargets[1].position = e;
            var f = (w+e)/2;
            _landmarkTargets[2].position = f;



            // pull target of current trial
            var target = session.CurrentTrial.settings.GetString("landmarkTarget");

            // get distance between elbow and wrist, and if in pre-test create a rolling average of size
            // (due to possibility of noise) to use as basis for post-test comparison.
            var forearmSize = Vector3.Distance(w, e);

            _error = target switch
            {
                // get distance between stimulus and target (error) along horizontal axes (task axes)
                // 
                "Wrist" => Vector2.Distance(new Vector2(s.x, s.z), new Vector2(w.x, w.z)),
                "Elbow" => Vector2.Distance(new Vector2(s.x, s.z), new Vector2(e.x, e.z)),
                "Forearm" => Vector2.Distance(new Vector2(s.x, s.z), new Vector2(f.x, f.z)),
                _ => _error
            };

            if (_error > forearmSize/2)
            {
                _landmarkEstimates[_currentTrialNum - 1].gameObject.GetComponent<Renderer>().material = lapseMaterial;
            } else if(_error < forearmSize/2)
            {
                _landmarkEstimates[_currentTrialNum - 1].gameObject.GetComponent<Renderer>().material = estimateMaterial;
            }
            switch (target)
            {
                case "Wrist":
                    _wristEstimates.Add(s);
                    break;
                case "Elbow":
                    _elbowEstimates.Add(s);
                    break;
            }
            // Averages the wrist and elbow estimates to create a rolling "perceived arm length"
            var perceivedArmLength = (_wristEstimates.Count >= 1 && _elbowEstimates.Count >= 1) ? 
                Vector3.Distance(_wristEstimates.Average(), _elbowEstimates.Average()) : 0;
            var perceivedArmRatio = perceivedArmLength / forearmSize;




            if(session.currentBlockNum < 3)
            {
                if(_oscillateStimulus.forearmLengthPre == 0)
                {
                    _oscillateStimulus.forearmLengthPre = forearmSize;
                }
                else
                {
                    _oscillateStimulus.forearmLengthPre = (_oscillateStimulus.forearmLengthPre + forearmSize) / 2;
                }
            }


            // get speed of stimulus and noise applied to finger and elbow
            var speed = _oscillateStimulus.speed;
            var noiseWrist = _oscillateStimulus.noiseOne;
            var noiseElbow = _oscillateStimulus.noiseTwo;
            var currentBlock = session.currentBlockNum + (Convert.ToInt32(session.participantDetails["startblock"]) - 1);
            const string format = "0.####";

            // If first trial of experiment, add the start trial number to the current trial number
            if(session.currentBlockNum == 0)
            {
                _currentTrialNum += (_startTrial - 1);
            }

            // add data to a new row, return as output
            var values = new UXFDataRow()
            {
                ("participant", session.ppid),
                ("trial", _currentTrialNum.ToString()),
                ("block", currentBlock.ToString()),
                ("wpos_x", w.x.ToString(format)),
                ("wpos_y", w.y.ToString(format)),
                ("wpos_z", w.z.ToString(format)),
                ("epos_x", e.x.ToString(format)),
                ("epos_y", e.y.ToString(format)),
                ("epos_z", e.z.ToString(format)),
                ("fpos_x", f.x.ToString(format)),
                ("fpos_y", f.y.ToString(format)),
                ("fpos_z", f.z.ToString(format)),
                ("spos_x", s.x.ToString(format)),
                ("spos_y", s.y.ToString(format)),
                ("spos_z", s.z.ToString(format)),
                ("target", target),
                ("error", _error.ToString(format)),
                ("forearmlength", forearmSize.ToString(format)),
                ("perceivedforearmlength", perceivedArmLength.ToString(format)),
                ("perceivedforearmlengthratio", perceivedArmRatio.ToString(format)),
                ("noisewrist", noiseWrist.ToString(format)),
                ("noiseelbow", noiseElbow.ToString(format)),
                ("startposition", _startPosition.ToString(format)),
                ("direction", _oscillateStimulus.direction),
                ("speed", speed),
                ("trialtime", _trialTime.ToString(format))
            };



            Utilities.UXFDebugLog("Landmark response recorded.");
            return values;
        }

        public override void StartRecording()
        {
            // Make sure current task is indeed landmark estimation, and that the coroutine isn't already running 
            // (because I'm recording the full block in a single .csv)
            _startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            SetupDescriptorAndHeader();
            
            if (session.blocks[session.currentBlockNum].settings.GetString("task") != "landmark" || _isRunning) return;
            
            Utilities.UXFDebugLog("Landmark coroutine starting.");
            //If debugging, shorten block.
            var isDebug = session.ppid == "test";
            if(isDebug)
            {
                _totalTrialNum = 2;
            } 
            else if(session.currentBlockNum == 0 & _startTrial > 1)
            {
                _totalTrialNum = 18 - (_startTrial - 1);
            }
            else 
            {
                _totalTrialNum = 18;
            }
                
            _oscillateStimulus = stimuli.GetComponent<OscillateStimulus>();
            StartCoroutine(LandmarkTaskHandler());
        }

        private void ClickDown (SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
            Utilities.UXFDebugLog("Click registered.");
            if (!_trialUnderway) return;
            Utilities.UXFDebugLog("Response registered.");
            _isClicked = true;
        }

        public override void StopRecording()
        {
            //Ensure current task is landmark estimation and that this is the final trial.
            if(session.CurrentBlock.settings.GetString("task") == "landmark" && _currentTrialNum == _totalTrialNum)
            {
                StopAllCoroutines();
            }
        }


        /// <summary>
        /// Landmark task coroutine. Handles the display/removal of stimuli, the initiation and end of trials, and recording the participant's response.
        /// </summary>
        IEnumerator LandmarkTaskHandler()
        {
            Utilities.UXFDebugLog("Entered landmark Coroutine.");
            _isRunning = true;
            bool isTestingMode = session.settings.GetBool("testing_mode");
            participantCamera = GameObject.FindGameObjectWithTag("participantCamera").GetComponent<Camera>();
            eyeTracker = participantCamera.gameObject.GetComponent<UXFEyeTrackerFixations>();
            data = new UXFDataTable(header);
            var dataList = new List<UXFDataRow>();
            var instructionAudio = experimentControl.GetComponent<InstructionAudio>();
            SkinnedMeshRenderer avatarRenderer = avatarHandler.GetAvatar().GetComponentInChildren<SkinnedMeshRenderer>();
            _wristEstimates = new List<Vector3>();
            _elbowEstimates = new List<Vector3>();
            _landmarkEstimates = new List<Transform>();
            _landmarkTargets = new List<Transform>();
            var armPlacementCollider = armPlacement.GetComponent<Collider>();
            var armPlacementRenderer = armPlacement.GetComponent<Renderer>();
            LandmarkSetup(avatarRenderer);            

            //Loop which runs for n trials, recording a row of data when participants make an estimate.
            for(var i = 1; i <= _totalTrialNum; i++)
            {
                _currentTrialNum = i;
                if(_currentTrialNum == 1)
                {
                    if (!isTestingMode)
                    {
                        instructionAudio.PlayAudioSequentially(preTaskAudio);
                        yield return new WaitUntil(() => instructionAudio.IsFinished());
                        yield return StartCoroutine(_instructionsDisplay.InstructionsCoroutine("landmark"));
                    }
                    armPlacementRenderer.enabled = false;
                    _oscillateStimulus.ToggleLandmarkTable();
                    _oscillateStimulus.RandomizeParameters();
                }

                yield return new WaitForSeconds(1);
                Utilities.UXFDebugLog("Trial number " + i + " beginning.");
                _instructionsDisplay.LandmarkTargetUpdate();
                yield return new WaitForSeconds(1);


                // Make sure participants have aligned their arm along the z axis.
                yield return new WaitUntil(() => _startTrigger.armIsPlaced & _oscillateStimulus.isAligned);


                // Loops for duration of countdown, playing a tone each iteration.
                for (var k = 0; k < session.settings.GetInt("start_delay"); k++)
                {
                    _oscillateStimulus.PlaySound("countdown");
                    yield return new WaitForSeconds(1);
                }


                //Begin trial, note time, make stimulus visible and randomize its parameters.
                if(session.currentTrialNum == 0 || session.CurrentTrial.status != TrialStatus.InProgress)
                {
                    session.BeginNextTrial();
                    _startPosition = _oscillateStimulus.transform.position.z;
                }

                armPlacementCollider.enabled = false;
                var timeBegin = Time.time;

                //Start tracking gaze if it is enabled.
                if(session.settings.GetBool("eye_tracking_enabled") & session.settings.GetBool("record_data"))
                {
                    eyeTracker.StartRecording(
                        DateTime.UtcNow
                    );
                }

                _trialUnderway = true;

                //Make stimuli appear and note its position
                if(!stimuli.GetComponent<Renderer>().enabled)
                {
                    _oscillateStimulus.ToggleVisibility();
                    _startPosition = _oscillateStimulus.transform.position.z;
                }  

                _oscillateStimulus.PlaySound(session.CurrentTrial.settings.GetString("landmarkTarget").ToLower());

                //Pause execution until button is pressed (participant makes judgment).
                yield return new WaitUntil(() => _isClicked);
                //Set clicked to false, and trialUnderway to false so it can't be used in the interval.
                _isClicked = false;
                _trialUnderway = false;
                //Take the time in milliseconds it took to make the estimation
                _trialTime = Time.time - timeBegin;
                //Record the response
                data.AddCompleteRow(GetCurrentValues());

                //Play a selection sound
                _oscillateStimulus.PlaySound(
                    session.CurrentTrial != session.CurrentBlock.lastTrial ? "select" : "final");
                Utilities.UXFDebugLog("Response detected.");

                //Disable vision of the stimuli
                _oscillateStimulus.ToggleVisibility();
                UXF.Utilities.UXFDebugLog("Trial number " + i + " ending.");
                
                //Randomize the parameters of the stimulus for the next trial.
                _oscillateStimulus.RandomizeParameters();

                //Save the data every trial, in case the experiment is interrupted.
                session.CurrentTrial.SaveDataTable(data, dataName, dataType: UXFDataType.Trackers);

                Utilities.UXFDebugLog("Landmark data saved.");
                //Clear the data file for the next trial.
                data = new UXFDataTable(header);

                //End the trial
                session.EndCurrentTrial();
                Utilities.UXFDebugLog("Trial ended.");
                if(session.settings.GetBool("eye_tracking_enabled") & session.settings.GetBool("record_data"))
                {
                    eyeTracker.StopRecording();
                }
                armPlacementCollider.enabled = true;
                //If last trial, display intermission instructions and turn everything off.
                if (session.CurrentTrial != session.CurrentBlock.lastTrial) continue;

                LandmarkCleanup(avatarRenderer);
                if(!isTestingMode)
                {
                    instructionAudio.PlayAudioSequentially(postTaskAudio);
                    yield return new WaitUntil(() => instructionAudio.IsFinished());
                    yield return new WaitForSeconds(5);
                }
                if (session.settings.GetString("format") == "non-VR"){
                    participantCamera.enabled = false;
                }
                _stimuliSetup.RemoveStimuli();  
                StopRecording();
            }      
        }

        /// <summary>
        /// Hides/removes landmark task-relevant objects when the task is complete.
        /// </summary>
        private void LandmarkCleanup(SkinnedMeshRenderer avatarRenderer)
        {
            foreach(Transform t in _landmarkEstimates)
            {
                t.position = new Vector3(0, 0, 0);
            }
            foreach(Transform t in _landmarkTargets)
            {
                if (t.name == "landmarkForearm")
                {
                    t.GetComponent<SphereCollider>().enabled = false;
                }
                t.gameObject.SetActive(false);
            }
            armPlacement.GetComponent<BoxCollider>().enabled = false;
            _researcherCamera.transform.position = new Vector3(0,2.15f,-0.5f);
            _instructionsDisplay.DisplayCanvas();
            _oscillateStimulus.enabled = false;
            _isRunning = false;
            _startTrigger.ResetTriggers();
            clickWatcher.RemoveOnStateDownListener(ClickDown, handType);
            _oscillateStimulus.ToggleLandmarkTable();
            _trackerControlHub.DisableTrackers();
            avatarRenderer.enabled = true;
        }

        /// <summary>
        /// Adds necessary objects and variables to the scene for the landmark task.
        /// </summary>
        private void LandmarkSetup(SkinnedMeshRenderer avatarRenderer)
        {
            if(_landmarkEstimates.Count == 0)
            {
                var landmarkEstimates = GameObject.Find("landmarkEstimates");
                var landmarkTargets = GameObject.Find("landmarkTargets");
                
                _landmarkEstimates.AddRange(GetChildTransforms(landmarkEstimates.transform));
                _landmarkTargets.AddRange(GetChildTransforms(landmarkTargets.transform));
                foreach(Transform t in _landmarkTargets)
                {
                    t.gameObject.SetActive(true);
                    if (t.name == "landmarkForearm")
                    {
                        t.GetComponent<SphereCollider>().enabled = false;
                    }
                }
                UXF.Utilities.UXFDebugLog("Landmark researcher objects added to lists.");
            }
            armPlacement.GetComponent<BoxCollider>().enabled = true;
            _researcherCamera.transform.position = new Vector3(0.35f,1.25f,-0.5f);
            avatarRenderer.enabled = false;
            _startTrigger ??= armPlacement.GetComponent<LandmarkStartTrigger>();
            clickWatcher.AddOnStateDownListener(ClickDown, handType);
            _instructionsDisplay ??= experimentControl.GetComponent<InstructionsDisplay>();
            _trackerControlHub ??= experimentControl.GetComponent<TrackerControlHub>();
            _stimuliSetup ??= experimentControl.GetComponent<StimuliSetup>();
            if (session.settings.GetString("format") == "non-VR")
            {
                participantCamera.enabled = true;
            }
        }

        private List<Transform> GetChildTransforms(Transform parent)
        {
            var path = new List<Transform>();
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                path.Add(parent.transform.GetChild(i).GetComponent<Transform>());
            }
            return path;
        }
    }
}