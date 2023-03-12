using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BOLL7708;
using UnityEngine;
using UnityEngine.Serialization;
using UXF;
using Valve.VR;

namespace MovementTracking
{
    /// <summary>
	/// Records the position, orientation and velocity of a SteamVR-based positional tracker at 200hz using the OpenVR api, and saves it via the Unity Experiment Framework.
	/// </summary>
    public class UXFOpenVRTracker : Tracker
    {


        /// <summary>
        /// Rate at which you wish to record data. At some point, you will be limited by
        /// the rate at which the OpenVR api can provide data, or your hardware.
        /// </summary>
        public int recordRate = 240;

        /// <summary>
        /// Serial number(s) you want to record data from. Used to identify the index.
        /// </summary>
        public string[] serialNumbers;
        
        /// <summary>
        /// The tracker index(es) associated with your serial number(s).
        /// </summary>
        public List<int> _openVRTrackerIndex;
        
        /// <summary>
        /// The names you would like to associate with the trackers you are recording from.
        /// These will be recorded in the data file.
        /// </summary>
        public string[] trackerNames;

        private Dictionary<string, string> _trackerSerialNameDictionary;
        private Dictionary<int, string> _trackerNameDictionary;

        private DateTime _startTime;
        /// <summary>
        /// Should reflect your desired sample rate in the inspector window.
        /// </summary>
        [FormerlySerializedAs("ThreadedUpdatesPerSecond")] public int threadedUpdatesPerSecond;
        public bool verbose = false;

        /// <summary>
        /// Indicates the number of samples taken during a particular trial.
        /// </summary>
        public int sampleCount;
        private const int oneSecond = 1000;
        private const string format = "F4";
        /// <summary>
        /// Indicates the number of consecutive repeated/identical measurements taken during a particular trial.
        /// </summary>
        public int[] numRepeats;
        private UXFDataRow _lastRow;
        private UXFDataRow _newRow;

        public Session session;
        /// <summary>
        /// Contains the UXFDataRows from the fastUpdate loop.
        /// </summary>
        private List<UXFDataRow> dataRows;

        // Trial context
        private int _trial;
        private int _block;
        private string _participant;
        
        /// <summary>
        /// The threads that handle sampling and saving.
        /// </summary>
        private Thread _collectData;
        private readonly object _lock = new object();

        private int _indexOfTracker;

        private EasyOpenVRSingleton _trackingInstance;
 
         /// <summary>
        /// Find the tracker index which matches the _serialNumber variable.
        /// </summary>
        private int GetTrackerIndex(string serialNumber)
         {
            ETrackedPropertyError error = new();
            StringBuilder sb = new();
            
            for (var i = 0; i < SteamVR.connected.Length; ++i)
            {
                string trackerName = _trackerSerialNameDictionary[serialNumber];
                OpenVR.System.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
                var serialNum = sb.ToString();
                if (serialNum == serialNumber)
                {
                    Debug.Log("[OpenVR]: Assigning device " + i + " to " + trackerName + " (" + serialNumber +")");
                    _trackerNameDictionary.Add(i, trackerName);
                    return i;
                }
            }
            Utilities.UXFDebugLogError("No tracker found with serial number " + serialNumber);
            return -1;
        }

         /// <summary>
         /// Removes the trackers which weren't found from the indices list.
         /// </summary>
         /// <returns></returns>
         private List<int> ConfirmValidTrackerIndices()
         {
            try
            {
                List<int> validTrackerIndex = new List<int>();
                // Initialises the list of valid trackers
                for (var i = 0; i < _openVRTrackerIndex.Count; i++)
                {
                    if (_openVRTrackerIndex[i] == -1)
                    {
                        continue;
                    }
                    validTrackerIndex.Add(_openVRTrackerIndex[i]);
                    if (verbose) UXF.Utilities.UXFDebugLog("Tracker " + _openVRTrackerIndex[i] + " is valid.");
                }
                return validTrackerIndex;
            } catch (Exception e)
            {
                UXF.Utilities.UXFDebugLogError("Error in ConfirmValidTrackerIndices: " + e);
            }

            return null;
         }

         private string DataName
        {
            get
            {
                Debug.AssertFormat(measurementDescriptor.Length > 0, "No measurement descriptor has been specified for this Tracker!");
                return string.Join("_", new[]{session.CurrentBlock.settings.GetString("task"), objectName, measurementDescriptor});
            }
        }        


  
        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "movement_steamvr";
            // Quaternions, not euler angles. Time should be measured outside of the Unity API.
            customHeader = new[]
            {
                "participant",
                "block",
                "trial",
                "phase",
                "time_ms",
                "tracker",
                "pos_x",
                "pos_y",
                "pos_z",
                "rot_w",
                "rot_x",
                "rot_y",
                "rot_z",
                "velocity_x",
                "velocity_y",
                "velocity_z"
            };
        }
        
        /// <summary>
        /// Sampling of position and orientation, to be used in a parallel thread.
        /// </summary>
        private void RecordMovementData()
        {
            // List of UXF Data Rows to hold the data from each tracker/serial number.
            List<UXFDataRow> dataList = new List<UXFDataRow>();
            // List of trackers with valid index
            List<int> validTrackerIndex = ConfirmValidTrackerIndices();
            TrackedDevicePose_t[] trackerPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            
            // Time in ms for updating samples/second.
            var timeSampleRate = _startTime;

            // Count the number of samples
            var loopCount = 0;

            // Bool, allowing for repetition and ending of while loop.
            var trialOngoing = true;

            // Loop while the trial is ongoing- record data if recording, notify that trial has stopped if not recording.
            while(trialOngoing)
            {
                switch (Recording)
                {
                    case true:
                    try{
                        var time = (int)(DateTime.UtcNow - _startTime).TotalMilliseconds;
                        trackerPoses = _trackingInstance.GetDeviceToAbsoluteTrackingPose(ref trackerPoses);
                        foreach (var index in validTrackerIndex)
                        {
                            if (index == -1)
                            {
                                continue;
                            }
                            
                            // Sample data with OpenVR API, transforming via EasyOpenVR UnityUtils functions
                            // to Unity coordinates.
                            dataList.Add(RecordOpenVRPoses(ref trackerPoses, index, time));
                        }

                        //Check sample rate, update if necessary.
                        if (verbose) {
                            timeSampleRate = TimeSampleRate(timeSampleRate, ref loopCount);
                            // Iterate counters and sleep for 1000/recording rate (200(hz) = 5ms)
                            loopCount++;
                        }
                        sampleCount++;
                        Thread.Sleep(1000/recordRate);
                    } catch (Exception e)
                    {
                        Utilities.UXFDebugLogError("[OpenVR]: Error in RecordMovementData: " + e);
                    }
                        break;

                    //When recording ends, if data has been collected, report the size and log that the thread has ended.
                    case false when dataList.Count > 0:
                        sampleCount = 0;

                        // Assign a copy of the data to the dataRows variable.
                        lock (_lock)
                        {
                            dataRows = dataList;
                        }

                        // Change bool to break while loop.
                        trialOngoing = false;
                        break;
                    
                    default:
                        Utilities.UXFDebugLogWarning("Data from thread contains no rows.");
                        trialOngoing = false;
                        break;
                }
            }
        }

        private UXFDataRow RecordOpenVRPoses(ref TrackedDevicePose_t[] trackerPoses, int index, int time)
        {
            try
            {
                var velocity = trackerPoses[index].vVelocity;

                var trackerPosition = EasyOpenVRSingleton.UnityUtils.MatrixToPosition(ref trackerPoses[index].mDeviceToAbsoluteTracking);
                var trackerRotation = EasyOpenVRSingleton.UnityUtils.MatrixToRotation(ref trackerPoses[index].mDeviceToAbsoluteTracking);

                // Calculate the velocity of the tracker using the absolute value of the velocity vector.
                // This is drawn from the IMU data.
                return new UXFDataRow()
                {
                    ("participant", _participant),
                    ("block", _block.ToString()),
                    ("trial", _trial.ToString()),
                    ("phase", session.CurrentTrial.settings.GetString("phase")),
                    ("time_ms", time.ToString(format)),
                    ("tracker", _trackerNameDictionary[index]),
                    ("pos_x", trackerPosition.v0.ToString(format)),
                    ("pos_y", trackerPosition.v1.ToString(format)),
                    ("pos_z", trackerPosition.v2.ToString(format)),
                    ("rot_w", trackerRotation.w.ToString(format)),
                    ("rot_x", trackerRotation.x.ToString(format)),
                    ("rot_y", trackerRotation.y.ToString(format)),
                    ("rot_z", trackerRotation.z.ToString(format)),
                    ("velocity_x", velocity.v0.ToString(format)),
                    ("velocity_y", velocity.v1.ToString(format)),
                    ("velocity_z", velocity.v2.ToString(format))
                    
                };
            }
            catch (Exception e)
            {
                UXF.Utilities.UXFDebugLogError("[OpenVR] Error while recording: " + e.ToString() +
                                               ", Recording a blank row for " + _trackerNameDictionary[index] + "tracker");
                return new UXFDataRow()
                {
                    ("participant", session.ppid),
                    ("block", _block.ToString()),
                    ("trial", _trial.ToString()),
                    ("phase", session.CurrentTrial.settings.GetString("phase")),
                    ("time_ms", time.ToString(format)),
                    ("tracker", _trackerNameDictionary[index]),
                    ("pos_x", "NA"),
                    ("pos_y", "NA"),
                    ("pos_z", "NA"),
                    ("rot_w", "NA"),
                    ("rot_x", "NA"),
                    ("rot_y", "NA"),
                    ("rot_z", "NA"),
                    ("velocity_x", "NA"),
                    ("velocity_y", "NA"),
                    ("velocity_z", "NA")
                };
            }
        }

        private DateTime TimeSampleRate(DateTime timeSampleRate, ref int loopCount)
        {
            // Sets number of ms for one second, for displaying samples/second.

            if ((DateTime.UtcNow - timeSampleRate).TotalMilliseconds < oneSecond) return timeSampleRate;
            threadedUpdatesPerSecond = loopCount;
            loopCount = 0;
            timeSampleRate = DateTime.UtcNow;
            UXF.Utilities.UXFDebugLog("[OpenVR]: Samples per second: " + threadedUpdatesPerSecond);
            return timeSampleRate;
        }
        
        protected override UXFDataRow GetCurrentValues()
        {
            // Put here to prevent errors, but this tracker uses something different from GetCurrentValues. Should not run.
            Utilities.UXFDebugLogWarning("GetCurrentValues() is running, but shouldn't be.");
            return new UXFDataRow();

        }


        public void StartRecording(DateTime startTime)
        {
            if(recording) return;
            Utilities.UXFDebugLog("[OpenVR]: Recording Started for OpenVR Trackers");
            _trackingInstance = EasyOpenVRSingleton.Instance;
            try
            {
                if (!_trackingInstance.IsInitialized()) _trackingInstance.Init();
            }
            catch(NullReferenceException err) {
                Utilities.UXFDebugLog(err + " OpenVR singleton not present.");
            }

            SetupDescriptorAndHeader();
            data = new UXFDataTable(header);
            _startTime = startTime;
            UpdateExperimentalContext();
            
            // Combine the tracker names and serial numbers into a dictionary.
            try
            {
                _trackerNameDictionary = new Dictionary<int, string>();
                _trackerSerialNameDictionary = new Dictionary<string, string>();
                for (var i = 0; i < trackerNames.Length; i++)
                {
                    _trackerSerialNameDictionary.Add(serialNumbers[i], trackerNames[i]);
                }
            
            
                // Find the current indices of the trackers.
                _openVRTrackerIndex = new List<int>();
                foreach (string sn in serialNumbers)
                {
                    _openVRTrackerIndex.Add(GetTrackerIndex(sn));
                }
                // If there are no trackers, don't start recording.
                if (!(updateType == TrackerUpdateType.fastUpdate & serialNumbers.Length > 0)) return;
                if (verbose) Utilities.UXFDebugLog(_openVRTrackerIndex.Count + " OpenVR trackers found. Initializing thread.");
            } catch (Exception e)
            {
                Utilities.UXFDebugLogError("[OpenVR]: Error while initializing OpenVR tracker indices: " + e);
            }

            recording = true;

            _collectData = new Thread(RecordMovementData);
            _collectData.Start();
            
            Utilities.UXFDebugLog("[OpenVR]: Thread Started.");
        }


        private void UpdateExperimentalContext()
        {
            try
            {
                var startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
                _block = session.currentBlockNum + (startBlock - 1);
                _participant = session.ppid;
                var startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
                _trial = session.currentTrialNum;

                // If the participant is resuming a session, some adjustment is needed to put the trial at the correct number.
                if(startTrial > 1 && startBlock == _block)
                {
                    _trial += startTrial-1;
                }
            } catch (Exception e)
            {
                Utilities.UXFDebugLogError("[OpenVR]: Error while updating experimental context: " + e);
            }
        }
        

        public override void StopRecording()
        {
            recording = false;
            try
            {
                _collectData.Join();
            }
            catch (NullReferenceException e)
            {
                Utilities.UXFDebugLog("[OpenVR]: No thread to Join." + e);
            }
            Utilities.UXFDebugLog("[OpenVR]: Recording Ended");
            InitSavingThread();
        }
        
        private void InitSavingThread()
        {
            Task.Run(SaveMovementData);
        }

        /// <summary>
        /// Save data from trial.
        /// </summary>
        private void SaveMovementData()
        {
            try
            {
                lock (_lock)
                {
                    foreach (var i in dataRows)
                    {
                        data.AddCompleteRow(i);
                    }
                }

                session.CurrentTrial.SaveDataTable(data, DataName, UXFDataType.Trackers);
            }
            catch (Exception e)
            {
                Utilities.UXFDebugLogError("[OpenVR]: Error while saving data: " + e);
            }
        }


        //Make sure to abort threads if possible.

        private void OnDisable()
        {
            _collectData?.Abort();
        }

        private void OnApplicationQuit()
        {
            _collectData?.Abort();
        }

        }

    }