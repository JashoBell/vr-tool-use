using System.Threading;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
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
    
    public class UXFEyeTracker : Tracker
    {
        /// <summary>
        /// Rate of recording through SRanipal (max 120hz).
        /// </summary>
        public int recordRate = 120;

        /// <summary>
        /// Whether to record the colliders that intersect with the gaze ray. Done with a separate tracking script that should be attached to the same game object.
        /// </summary>
        public bool recordFixationObjects = true;
        
        /// <summary>
        /// The name of the fixation object tracker. This should be attached to the same game object if recordFixationObjects is true.
        /// </summary>
        private UXFEyeTrackerFixations fixationTracker;

        /// <summary>
        /// Should reflect your desired sample rate in the inspector window.
        /// </summary>
        [FormerlySerializedAs("ThreadedUpdatesPerSecond")] public int threadedUpdatesPerSecond;

        /// <summary>
        /// Indicates the number of samples taken during a particular trial.
        /// </summary>
        public int sampleCount;
    
        /// <summary>
        /// UXF Session object with block and trial settings
        /// </summary>
        public Session session;

        /// <summary>
        /// Contains the UXFDataRows from the callback function.
        /// </summary>
        private UXFDataRow[] _trialDataArray;

        private bool _safeToRelease;
        private bool _trialOngoing;
        private Thread _collectData, _saveData;
        ///
        /// Variables to capture eye-tracking data.
        /// 
        private static EyeData_v2 _eyeData = new EyeData_v2();
        private EyeData_v2 _eyeDataTemp = new EyeData_v2();
        private EyeParameter _eyeParameter = new EyeParameter();
        public GazeRayParameter Gaze = new GazeRayParameter();
        private static int _frame;
        private bool _gazeRaySampling;
        private static float _timeStamp;
        private static UInt64 _eyeValidL, _eyeValidR;                 // The bits explaining the validity of eye data.
        private static float _opennessL, _opennessR;                    // The level of eye openness.
        private static float _pupilDiameterL, _pupilDiameterR;        // Diameter of pupil dilation.
        private static Vector2 _posSensorL, _posSensorR;              // 2d pupil positions.
        private static Vector3 _gazeOriginL, _gazeOriginR;            // World position of gaze origin.
        private static Vector3 _gazeDirectL, _gazeDirectR;            // Normalized gaze direction.
        private static float _frownL, _frownR;                          // The extent to which the sensor detects frowning in the eyes.
        private static float _squeezeL, _squeezeR;                      // The extent to which the sensor detects the eyes as being squeezed shut.
        private static float _wideL, _wideR;                            // The extent to which the sensor detects the eyes as being wide open.
        private static double _gazeSensitive;                           // The sensitivity of the gaze ray from [0, 1].
        private static float _distanceC;                                // Distance from the central point of right and left eyes.
        private static bool _distanceValidC;                           // Validity of combined data of right and left eyes.
        [FormerlySerializedAs("cal_need")] public bool calNeed;       // Calibration assessment.
        [FormerlySerializedAs("result_cal")] public bool resultCal;    // Result of calibration attempt.
        private static int _trackImpCnt = 0;
        private static TrackingImprovement[] _trackImpItem;
        private EasyOpenVRSingleton _trackingInstance;


        private static Ray _testRay;
        private static FocusInfo _focusInfo;
        private string _focusObject;

        private string DataName
        {
            get
            {
                Debug.AssertFormat(measurementDescriptor.Length > 0, "No measurement descriptor has been specified for this Tracker!");
                return string.Join("_", new string[]{session.CurrentBlock.settings.GetString("task"), measurementDescriptor});
            }
        }

        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "gaze_data";
            if(session.CurrentTrial.settings.ContainsKey("phase"))
            {
                customHeader = new string[]
                {
                    "participant",
                    "block",
                    "trial",
                    "trial_time",
                    "timestamp",
                    "eye_valid_L" ,
                    "eye_valid_R" ,
                    "openness_L" ,
                    "openness_R" ,
                    "pupil_diameter_L" ,
                    "pupil_diameter_R" ,
                    "pos_sensor_L.x" ,
                    "pos_sensor_L.y" ,
                    "pos_sensor_R.x" ,
                    "pos_sensor_R.y" ,
                    "gaze_origin_L.x" ,
                    "gaze_origin_L.y" ,
                    "gaze_origin_L.z" ,
                    "gaze_origin_R.x" ,
                    "gaze_origin_R.y" ,
                    "gaze_origin_R.z" ,
                    "gaze_direct_L.x" ,
                    "gaze_direct_L.y" ,
                    "gaze_direct_L.z" ,
                    "gaze_direct_R.x" ,
                    "gaze_direct_R.y" ,
                    "gaze_direct_R.z" ,
                    "gaze_sensitive" ,
                    "frown_L" ,
                    "frown_R" ,
                    "squeeze_L" ,
                    "squeeze_R" ,
                    "wide_L" ,
                    "wide_R" ,
                    "distance_valid_C" ,
                    "distance_C" ,
                    "track_imp_cnt",
                    "phase"
                };

            } else
            {
                customHeader = new string[]
                {
                    "participant",
                    "block",
                    "trial",
                    "trial_time",
                    "eye_valid_L" ,
                    "eye_valid_R" ,
                    "openness_L" ,
                    "openness_R" ,
                    "pupil_diameter_L" ,
                    "pupil_diameter_R" ,
                    "pos_sensor_L.x" ,
                    "pos_sensor_L.y" ,
                    "pos_sensor_R.x" ,
                    "pos_sensor_R.y" ,
                    "gaze_origin_L.x" ,
                    "gaze_origin_L.y" ,
                    "gaze_origin_L.z" ,
                    "gaze_origin_R.x" ,
                    "gaze_origin_R.y" ,
                    "gaze_origin_R.z" ,
                    "gaze_direct_L.x" ,
                    "gaze_direct_L.y" ,
                    "gaze_direct_L.z" ,
                    "gaze_direct_R.x" ,
                    "gaze_direct_R.y" ,
                    "gaze_direct_R.z" ,
                    "gaze_sensitive" ,
                    "frown_L" ,
                    "frown_R" ,
                    "squeeze_L" ,
                    "squeeze_R" ,
                    "wide_L" ,
                    "wide_R" ,
                    "distance_valid_C" ,
                    "distance_C" ,
                    "track_imp_cnt"
                };
            }
        }
        
        /// <summary>
        /// Function which records eye tracking data in a separate thread.
        /// </summary>
        /// <param name="_eyeData"></param>
        private void EyeTrackingDataSampling()
        {
            //Utilities.UXFDebugLog("Entered callback function.");
            // List of UXF Data Rows, with rows added after each sample.
            List<UXFDataRow> dataList = new List<UXFDataRow>();

            // Sets number of ticks for one second, for displaying samples/second.
            const int oneSecond = 10000000;
            const string format = "F4";
            // Time in ticks at start of recording.
            var time2 = System.DateTime.UtcNow;
            TimeSpan deltatime;

            // Time in ticks for updating samples/second.
            var timeSampleRate = System.DateTime.UtcNow.Ticks;

            // Count the number of samples
            int loopCount = 0;

            bool trialOngoing = true;

            SRanipal_Eye_API.GetEyeParameter(ref _eyeParameter);

            int startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
            int block = session.currentBlockNum + (startBlock - 1);

            int startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            int trial = session.currentTrialNum;

            if(startTrial > 1 & startBlock == block)
            {
                trial += startTrial-1;
            }

            // Loop while the trial is ongoing- record data if recording, notify that trial has stopped if not recording.
            while(trialOngoing)
            {
                if(recording & trialOngoing)
                {
                    ViveSR.Error error = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
                    if(!_gazeRaySampling)
                    {
                        _eyeDataTemp = _eyeData;
                    }

                    if (error == ViveSR.Error.WORK)
                    {
                        var verboseData = _eyeData.verbose_data;
                        var expressionData = _eyeData.expression_data;
                        //Store verbose data in appropriate variables
                        deltatime = System.DateTime.UtcNow - time2;
                        _timeStamp = _eyeData.timestamp;
                        _frame = _eyeData.frame_sequence;
                        _eyeValidL = verboseData.left.eye_data_validata_bit_mask;
                        _eyeValidR = verboseData.right.eye_data_validata_bit_mask;
                        _opennessL = verboseData.left.eye_openness;
                        _opennessR = verboseData.right.eye_openness;
                        _pupilDiameterL = verboseData.left.pupil_diameter_mm;
                        _pupilDiameterR = verboseData.right.pupil_diameter_mm;
                        _posSensorL = verboseData.left.pupil_position_in_sensor_area;
                        _posSensorR = verboseData.right.pupil_position_in_sensor_area;
                        _gazeOriginL = verboseData.left.gaze_origin_mm;
                        _gazeOriginR = verboseData.right.gaze_origin_mm;
                        _gazeDirectL = verboseData.left.gaze_direction_normalized;
                        _gazeDirectR = verboseData.right.gaze_direction_normalized;
                        _gazeSensitive = _eyeParameter.gaze_ray_parameter.sensitive_factor;
                        _frownL = expressionData.left.eye_frown;
                        _frownR = expressionData.right.eye_frown;
                        _squeezeL = expressionData.left.eye_squeeze;
                        _squeezeR = expressionData.right.eye_squeeze;
                        _wideL = expressionData.left.eye_wide;
                        _wideR = expressionData.right.eye_wide;
                        _distanceValidC = verboseData.combined.convergence_distance_validity;
                        _distanceC = verboseData.combined.convergence_distance_mm;
                        _trackImpCnt = verboseData.tracking_improvements.count;   

                    // Update samples per second in the inspector.
                        if (System.DateTime.UtcNow.Ticks - timeSampleRate >= oneSecond)
                        {
                            threadedUpdatesPerSecond = loopCount;
                            loopCount = 0;
                            timeSampleRate = System.DateTime.UtcNow.Ticks;
                        }


                        //Add sample to list.
                        if(session.settings.ContainsKey("phase"))
                        {
                            dataList.Add(new UXFDataRow()
                            {
                                ("participant", session.ppid),
                                ("block", block.ToString()),
                                ("trial", trial.ToString()),
                                ("trial_time", deltatime.TotalMilliseconds.ToString(format)),
                                ("timestamp", _timeStamp.ToString(format)),
                                ("eye_valid_L", _eyeValidL.ToString(format)),
                                ("eye_valid_R", _eyeValidR.ToString(format)),
                                ("openness_L", _opennessL.ToString(format)),
                                ("openness_R", _opennessR.ToString(format)),
                                ("pupil_diameter_L", _pupilDiameterL.ToString(format)),
                                ("pupil_diameter_R", _pupilDiameterR.ToString(format)),
                                ("pos_sensor_L.x", _posSensorL.x.ToString(format)),
                                ("pos_sensor_L.y", _posSensorL.y.ToString(format)),
                                ("pos_sensor_R.x", _posSensorR.x.ToString(format)),
                                ("pos_sensor_R.y" , _posSensorR.y.ToString(format)),
                                ("gaze_origin_L.x" , _gazeOriginL.x.ToString(format)),
                                ("gaze_origin_L.y" , _gazeOriginL.y.ToString(format)),
                                ("gaze_origin_L.z" , _gazeOriginL.z.ToString(format)),
                                ("gaze_origin_R.x" , _gazeOriginR.x.ToString(format)),
                                ("gaze_origin_R.y" , _gazeOriginR.y.ToString(format)),
                                ("gaze_origin_R.z" , _gazeOriginR.z.ToString(format)),
                                ("gaze_direct_L.x" , _gazeDirectL.x.ToString(format)),
                                ("gaze_direct_L.y" , _gazeDirectL.y.ToString(format)),
                                ("gaze_direct_L.z" , _gazeDirectL.z.ToString(format)),
                                ("gaze_direct_R.x" , _gazeDirectR.x.ToString(format)),
                                ("gaze_direct_R.y" , _gazeDirectR.y.ToString(format)),
                                ("gaze_direct_R.z" , _gazeDirectR.z.ToString(format)),
                                ("gaze_sensitive" , _gazeSensitive.ToString(format)),
                                ("frown_L" , _frownL.ToString(format)),
                                ("frown_R" , _frownR.ToString(format)),
                                ("squeeze_L" , _squeezeL.ToString(format)),
                                ("squeeze_R" , _squeezeR.ToString(format)),
                                ("wide_L" , _wideL.ToString(format)),
                                ("wide_R" , _wideR.ToString(format)),
                                ("distance_valid_C" , _distanceValidC.ToString()),
                                ("distance_C" , _distanceC.ToString(format)),
                                ("track_imp_cnt", _trackImpCnt.ToString(format)),
                                ("phase", session.CurrentBlock.settings.GetString("phase"))
                            });
                        }
                        else
                        {
                            dataList.Add(new UXFDataRow()
                            {
                                ("participant", session.ppid),
                                ("block", block.ToString()),
                                ("trial", trial.ToString()),
                                ("trial_time", deltatime.TotalMilliseconds.ToString(format)),
                                ("timestamp", _timeStamp.ToString(format)),
                                ("eye_valid_L", _eyeValidL.ToString(format)),
                                ("eye_valid_R", _eyeValidR.ToString(format)),
                                ("openness_L", _opennessL.ToString(format)),
                                ("openness_R", _opennessR.ToString(format)),
                                ("pupil_diameter_L", _pupilDiameterL.ToString(format)),
                                ("pupil_diameter_R", _pupilDiameterR.ToString(format)),
                                ("pos_sensor_L.x", _posSensorL.x.ToString(format)),
                                ("pos_sensor_L.y", _posSensorL.y.ToString(format)),
                                ("pos_sensor_R.x", _posSensorR.x.ToString(format)),
                                ("pos_sensor_R.y" , _posSensorR.y.ToString(format)),
                                ("gaze_origin_L.x" , _gazeOriginL.x.ToString(format)),
                                ("gaze_origin_L.y" , _gazeOriginL.y.ToString(format)),
                                ("gaze_origin_L.z" , _gazeOriginL.z.ToString(format)),
                                ("gaze_origin_R.x" , _gazeOriginR.x.ToString(format)),
                                ("gaze_origin_R.y" , _gazeOriginR.y.ToString(format)),
                                ("gaze_origin_R.z" , _gazeOriginR.z.ToString(format)),
                                ("gaze_direct_L.x" , _gazeDirectL.x.ToString(format)),
                                ("gaze_direct_L.y" , _gazeDirectL.y.ToString(format)),
                                ("gaze_direct_L.z" , _gazeDirectL.z.ToString(format)),
                                ("gaze_direct_R.x" , _gazeDirectR.x.ToString(format)),
                                ("gaze_direct_R.y" , _gazeDirectR.y.ToString(format)),
                                ("gaze_direct_R.z" , _gazeDirectR.z.ToString(format)),
                                ("gaze_sensitive" , _gazeSensitive.ToString(format)),
                                ("frown_L" , _frownL.ToString(format)),
                                ("frown_R" , _frownR.ToString(format)),
                                ("squeeze_L" , _squeezeL.ToString(format)),
                                ("squeeze_R" , _squeezeR.ToString(format)),
                                ("wide_L" , _wideL.ToString(format)),
                                ("wide_R" , _wideR.ToString(format)),
                                ("distance_valid_C" , _distanceValidC.ToString()),
                                ("distance_C" , _distanceC.ToString(format)),
                                ("track_imp_cnt", _trackImpCnt.ToString(format))
                            });
                        }
                    } 
                    else 
                    {
                        Utilities.UXFDebugLog("SRanipal not functioning correctly.");
                        Thread.Sleep(1000);
                    }
                    // Sleep for 1000/recording rate (120hz = 8.33ms)
                    
                    loopCount++;
                    sampleCount++;
                    Thread.Sleep(1000/200);
                } 

                //When recording ends, if data has been collected, report the size and log that the thread has ended.
                else if(!Recording & dataList.Count > 0)
                {
                    sampleCount = 0;
                // Send dataList clone to a public Array, report size and clear the thread's list.
                    _trialDataArray = dataList.ToArray();
                    dataList.Clear();
                    trialOngoing = false;
                }
                else
                {
                    Thread.Sleep(100);
                    trialOngoing = false;
                }
            }
        }


        protected override UXFDataRow GetCurrentValues()
        {
            // Put here to prevent errors, but this tracker uses something different from GetCurrentValues. Should not run.
            return new UXFDataRow();
        }
        public override void StartRecording()
        {
            // Replaces top-level StartRecording().
            Utilities.UXFDebugLog("Recording Start. Connecting to eye tracker.");
            SetupDescriptorAndHeader();
            data = new UXFDataTable(header);
            recording = true;
            
            //Only initialize a trackingInstance if it doesn't exist yet.
            if (updateType != TrackerUpdateType.eyeTracker) return;
            _collectData = new Thread(new ThreadStart(EyeTrackingDataSampling));
            _collectData.Start();
            Utilities.UXFDebugLog("Thread Started.");
        }
        public override void StopRecording()
        {
            recording = false;
            // Wait for thread to join, to ensure no combined writing of files occurs.
            _collectData.Join();

            // For each data row sampled, add to the trial's data. Notify entry into the for loop.
            _saveData = new Thread(SaveMovementData);
            _saveData.Start();            
        }

        private void SaveMovementData()
        {
            // For each data row sampled, add to the trial's data.
            foreach (UXFDataRow i in _trialDataArray)
            {
             data.AddCompleteRow(i);    
            }
            
            session.CurrentTrial.SaveDataTable(data, DataName, UXFDataType.Trackers);
        }

        private void OnDisable()
        {
            try
            {
                _collectData.Abort();
            }
            catch(NullReferenceException e)
            {
                UXF.Utilities.UXFDebugLog("UXF Eye Tracker: No thread to abort" + e);
            }
        }

        void OnApplicationQuit()
        {
            try
            {
                _collectData.Abort();
            }
            catch(NullReferenceException e)
            {
                UXF.Utilities.UXFDebugLog("UXF Eye Tracker: No thread to abort" + e);
            }
        }

        public bool Focus(out Ray ray, out FocusInfo focusInfo)
            {
                _gazeRaySampling = true;
                bool valid = SRanipal_Eye_v2.GetGazeRay(GazeIndex.COMBINE, out ray, _eyeDataTemp);
                _gazeRaySampling = false;
                if (valid)
                {
                    var cameraTransform = Camera.main.transform;
                    Ray rayGlobal = new Ray(cameraTransform.position, cameraTransform.TransformDirection(ray.direction));
                    valid = Physics.Raycast(rayGlobal, out RaycastHit hit, 10);
                    focusInfo = new FocusInfo
                    {
                        point = hit.point,
                        normal = hit.normal,
                        distance = hit.distance,
                        collider = hit.collider,
                        rigidbody = hit.rigidbody,
                        transform = hit.transform
                    };
                }
                else
                {
                    focusInfo = new FocusInfo() {
                        point = Vector3.zero,
                        normal = Vector3.zero,
                        distance = 0,
                        collider = null,
                        rigidbody = null,
                        transform = null
                    };
                }
                return valid;
            }

    }

    }