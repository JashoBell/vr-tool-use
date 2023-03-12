using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using ViveSR.anipal.Eye;
using ViveSR.anipal;
using ViveSR;
using UnityEngine;
using Valve.VR;
using BOLL7708;
using UnityEngine.Serialization;


namespace UXF
{

    public class UXFEyeTrackerFixations : Tracker
    {
        /// <summary>
        /// The name of the fixation object tracker. This should be attached to the same game object if recordFixationObjects is true.
        /// </summary>
        public Transform _hmdTransform;

        /// <summary>
        /// Should reflect your desired sample rate in the inspector window.
        /// </summary>
        [FormerlySerializedAs("ThreadedUpdatesPerSecond")]
        public int threadedUpdatesPerSecond;

        /// <summary>
        /// Indicates the number of samples taken during a particular trial.
        /// </summary>
        public int sampleCount;

        /// <summary>
        /// UXF Session object with block and trial settings
        /// </summary>
        public Session session;
        private bool _trialOngoing = false;
        public bool verbose = false;
        const int oneSecond = 10000000;
        const string format = "F4";
        private DateTime _startTime;

        ///
        /// Variables to capture eye-tracking data.
        /// 
        private static VerboseData _eyeData = new();
        public GazeRayParameter Gaze = new();
        [FormerlySerializedAs("cal_need")] public bool calNeed; // Calibration assessment.
        [FormerlySerializedAs("result_cal")] public bool resultCal; // Result of calibration attempt.

        private string DataName
        {
            get
            {
                Debug.AssertFormat(measurementDescriptor.Length > 0,
                    "No measurement descriptor has been specified for this Tracker!");
                return string.Join("_",
                    new string[] { session.CurrentBlock.settings.GetString("task"), measurementDescriptor });
            }
        }

        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "gaze_data";
            if (session.CurrentTrial.settings.ContainsKey("phase"))
            {
                customHeader = new string[]
                {
                    "ppid",
                    "block",
                    "trial",
                    "trial_time",
                    "gaze_ray_origin.x",
                    "gaze_ray_origin.y",
                    "gaze_ray_origin.z",
                    "gaze_ray_direction.x",
                    "gaze_ray_direction.y",
                    "gaze_ray_direction.z",
                    "distance_valid_combined",
                    "gaze_hit_object_name",
                    "gaze_hit_point.x",
                    "gaze_hit_point.y",
                    "gaze_hit_point.z",
                    "pupil_diameter_left",
                    "pupil_diameter_right",
                    "phase"
                };

            }
            else
            {
                customHeader = new string[]
                {
                    "ppid",
                    "block",
                    "trial",
                    "trial_time",
                    "gaze_ray_origin.x",
                    "gaze_ray_origin.y",
                    "gaze_ray_origin.z",
                    "gaze_ray_direction.x",
                    "gaze_ray_direction.y",
                    "gaze_ray_direction.z",
                    "distance_valid_combined",
                    "gaze_hit_object_name",
                    "gaze_hit_point.x",
                    "gaze_hit_point.y",
                    "gaze_hit_point.z",
                    "pupil_diameter_left",
                    "pupil_diameter_right"
                };
            }
        }

        /// <summary>
        /// Coroutine for recording eye tracking data. Must run synchronously with the main thread
        /// to capture the raycast data.
        /// </summary>
        private IEnumerator EyeTrackingDataSampling()
        {
            //Utilities.UXFDebugLog("Entered callback function.");
            // List of UXF Data Rows, with rows added after each sample.
            List<UXFDataRow> dataList = new List<UXFDataRow>();
            // Sets number of ticks for one second, for displaying samples/second.
            // Time in ticks at start of recording.
            var time2 = _startTime;

            // Time in ticks for updating samples/second.
            var timeSampleRate = time2;

            // Count the number of samples
            int loopCount = 0;
            int numFailed = 0;

            _trialOngoing = true;

            string ppid = session.ppid;
            int startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
            int startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            int block = session.currentBlockNum + (startBlock - 1);
            int trial = (startTrial > 1 & startBlock == block)
                ? session.currentTrialNum + (startTrial - 1)
                : session.currentTrialNum;
            Ray gazeRay = new();
            RaycastHit gazeHit = new();

            bool gazeHitValid;

            // Loop while the trial is ongoing- record data if recording, notify that trial has stopped if not recording.
            while (_trialOngoing)
            {
                switch (Recording)
                {
                    case true:

                        try
                        {
                            if(SRanipal_Eye_v2.GetVerboseData(out _eyeData) && Focus(ref gazeRay, ref gazeHit, out gazeHitValid))
                            {
                                UXFDataRow newRow = new()
                                    {
                                        ("ppid", ppid),
                                        ("block", block.ToString()),
                                        ("trial", trial.ToString()),
                                        ("trial_time", (System.DateTime.UtcNow - time2).TotalMilliseconds.ToString(format)),
                                        ("gaze_ray_origin.x", gazeRay.origin.x.ToString(format)),
                                        ("gaze_ray_origin.y", gazeRay.origin.y.ToString(format)),
                                        ("gaze_ray_origin.z", gazeRay.origin.z.ToString(format)),
                                        ("gaze_ray_direction.x", gazeRay.direction.x.ToString(format)),
                                        ("gaze_ray_direction.y", gazeRay.direction.y.ToString(format)),
                                        ("gaze_ray_direction.z", gazeRay.direction.z.ToString(format)),
                                        ("distance_valid_combined", _eyeData.combined.convergence_distance_validity.ToString()),
                                        ("gaze_hit_object_name", gazeHit.collider.name),
                                        ("gaze_hit_point.x", gazeHit.point.x.ToString(format)),
                                        ("gaze_hit_point.y", gazeHit.point.y.ToString(format)),
                                        ("gaze_hit_point.z", gazeHit.point.z.ToString(format)),
                                        ("pupil_diameter_left", _eyeData.left.pupil_diameter_mm.ToString(format)),
                                        ("pupil_diameter_right", _eyeData.right.pupil_diameter_mm.ToString(format))
                                    };
                                    if(session.CurrentTrial.settings.ContainsKey("phase"))
                                    {
                                        newRow.Add(("phase", session.CurrentTrial.settings.GetString("phase")));
                                    }
                                    // Update samples per second in the inspector.
                                    if ((System.DateTime.UtcNow - timeSampleRate).TotalMilliseconds >= oneSecond)
                                    {
                                        threadedUpdatesPerSecond = loopCount;
                                        loopCount = 0;
                                        timeSampleRate = System.DateTime.UtcNow;
                                    }

                                    dataList.Add(newRow);
                            }
                            else
                            {
                                numFailed++;
                            }
                        }
                        catch(Exception e)
                        {
                            Utilities.UXFDebugLog("[Eye Tracker]: Error during recording: " + e.Message);
                        }
                        loopCount++;
                        yield return new WaitForSeconds(0);
                        break;


                        //When recording ends, if data has been collected, report the size and log that the thread has ended.
                        case false when dataList.Count > 0:
                        if (verbose) UXF.Utilities.UXFDebugLog("[Eye Tracker]: Recording ended. Data collected: " + dataList.Count);
                        if (verbose) UXF.Utilities.UXFDebugLog("[Eye Tracker]: Number of invalid samples: " + numFailed);                        
                        sampleCount = 0;
                        _trialOngoing = false;
                        break;

                        default:
                        _trialOngoing = false;
                        Utilities.UXFDebugLog("[Eye Tracker]: No data collected.");
                        break;
                    }

                    sampleCount = dataList.Count;
                }
            // Once the trial has ended, save the data.

            SaveGazeData(dataList);
            }


        protected override UXFDataRow GetCurrentValues()
        {
            // Put here to prevent errors, but this tracker uses something different from GetCurrentValues. Should not run.
            return new UXFDataRow();
        }
        public void StartRecording(DateTime startTime)
        {
            if (recording) return;
            _startTime = startTime;
            _hmdTransform = gameObject.transform;
            SRanipal_Eye_v2.cameraTransform = _hmdTransform;
            if(session == null)
            {
                session = Session.instance;
            }

            // Replaces top-level StartRecording().
            try{
            if(verbose) Utilities.UXFDebugLog("[Eye Tracker]: Recording Initiating. Connecting to eye tracker.");
            SetupDescriptorAndHeader();
            data = new UXFDataTable(header);
            recording = true;
            StartCoroutine(EyeTrackingDataSampling());
            } catch (Exception e)
            {
                Utilities.UXFDebugLog("[Eye Tracker]: Error during recording initiation: " + e.Message);
            }
        }

        /// <summary>
        /// Stops the recording of the eye tracker.
        /// </summary>

        public override void StopRecording()
        {
            if (verbose) UXF.Utilities.UXFDebugLog("[Eye Tracker]: Recording stopping.");
            recording = false;
        }


        private void SaveGazeData(IEnumerable<UXFDataRow> dataList)
        {
            
            if (verbose) Utilities.UXFDebugLog("[Eye Tracker]: Saving data.");
            // Wait for the EyeTrackingDataSampling() function to finish.

            // For each data row sampled, add to the trial's data.
            try
            {
                foreach (UXFDataRow i in dataList)
                {
                    data.AddCompleteRow(i);
                }
                session.CurrentTrial.SaveDataTable(data, DataName, UXFDataType.Trackers);
                if (verbose) Utilities.UXFDebugLog("[Eye Tracker]: Data saved.");
            }
            catch (Exception e)
            {
                Utilities.UXFDebugLog("[Eye Tracker]: Error during saving: " + e.Message);
            }
            finally
            {
                StopCoroutine(EyeTrackingDataSampling());
            }

        }
        
        
        private bool Focus(ref Ray rayGlobal, ref RaycastHit hit, out bool hitValid)
        {
            bool valid = SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out Ray ray);

            if (valid)
            {
                rayGlobal = new Ray(_hmdTransform.position, _hmdTransform.TransformDirection(ray.direction));
                hitValid = Physics.Raycast(rayGlobal, out hit, 1.5f);;
            }
            else
            {
                hitValid = false;
            }

            return valid;
        }

    }

    }