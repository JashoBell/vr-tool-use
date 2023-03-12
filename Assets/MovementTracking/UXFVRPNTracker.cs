using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UXF;


namespace MovementTracking
{
    /// <summary>
	/// Records the position of a VRPN-based positional tracker at 200hz using a separate thread, and saves it via the Unity Experiment Framework.
	/// </summary>
    public class UXFVRPNTracker : Tracker
    {


        /// <summary>
        /// Rate at which you wish to record position.
        /// </summary>
        public int recordRate = 200;

        /// <summary>
        /// Name of VRPN tracker you are sampling from (default for Precision Point Tracking is PPT0)
        /// </summary>
        public string trackerName = "PPT0";

        /// <summary>
        /// Address of the VRPN tracker you are sampling from
        /// </summary>
        public string vrpnAddress = "localhost";
        private string _address;

        /// <summary>
        /// Should reflect your desired sample rate in the inspector window.
        /// </summary>
        public int updatesPerSecond;

        [Tooltip("If true, will print sampling rate and additional procedural updates.")]
        public bool verbose = false;

        private DateTime _startTime;

        /// <summary>
        /// Indicates the number of samples taken during a particular trial.
        /// </summary>
        public int sampleCount;
        public int[] perTrackerSamples;
        public int[] numRepeats;

        /// <summary>
        /// Zero-indexed Sensor ID of VRPN tracker you are sampling from (IR light ID - 1 for PPT)
        /// </summary>
        public int[] vrpnChannel;
        /// <summary>
        /// The name of the object you are tracking.
        /// </summary>
        public string[] tracked;
        [SerializeField] private bool recordOrientation = false;

        /// <summary>
        /// The UXF session that this tracker is sampling for.
        /// </summary>
        public Session session;
        /// <summary>
        /// Contains the UXFDataRows from the fastUpdate loop.
        /// </summary>
        private UXFDataRow[] _trialDataArray;

        private List<UXFDataRow[]> _trialDataArrayList;

        [Tooltip("Thread object that handles collection/saving of data.")]
        private Thread _collectData, _saveData;

        /// <summary>
        /// Creates the data name.
        /// </summary>
        /// <value></value>
        private string DataName
        {
            get
            {
                Debug.AssertFormat(measurementDescriptor.Length > 0, "No measurement descriptor has been specified for this Tracker!");
                return string.Join("_", new string[] { session.CurrentBlock.settings.GetString("task"), objectName, measurementDescriptor });
            }
        }

        /// <summary>
        /// Combines name and address.
        /// </summary>
        /// <param name="trackerIdentifier"></param>
        /// <returns></returns>
        private string GetTrackerAddress(string trackerIdentifier)
        {
            _address = "@" + vrpnAddress;
            var fulladdress = trackerIdentifier + _address;
            return fulladdress;
        }

        /// <summary>
        /// Establishes header for the collected data.
        /// </summary>

        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "movement";
            if(recordOrientation)
            {
                customHeader = new string[]
                {
                    "participant",
                    "block",
                    "trial",
                    "tracked",
                    "time_ms",
                    "phase",
                    "pos_x",
                    "pos_y",
                    "pos_z",
                    "rot_x",
                    "rot_y",
                    "rot_z",
                    "rot_w"
                };
            } else
            {
                customHeader = new string[]
                {
                    "participant",
                    "block",
                    "trial",
                    "tracked",
                    "time_ms",
                    "phase",
                    "pos_x",
                    "pos_y",
                    "pos_z"
                };
            }
        }

        /// <summary>
        /// Sampling of position, to be used in a parallel thread.
        /// </summary>
        private void RecordMovementData()
        {
            // List of UXF Data Rows, with rows added after each sample.
            List<List<UXFDataRow>> dataList = new();
            _trialDataArrayList = new List<UXFDataRow[]>();


            for (var i = 0; i < vrpnChannel.Length; i++)
            {
                dataList.Add(new List<UXFDataRow>());
            }

            const string format = "F4";

            // Sets number of Millisecond for one second, for displaying samples/second.
            const int oneSecond = 1000;


            // Time in Millisecond at start of recording.
            var time2 = System.DateTime.UtcNow;

            // Time in Millisecond for updating samples/second.
            var timeSampleRate = System.DateTime.UtcNow.Millisecond;

            // Count the number of samples
            var loopCount = 0;

            // Bool, allowing for repetition and ending of while loop.
            var trialOngoing = true;

            //vector3 variables for storing position and calculating velocity
            var previous = new System.Numerics.Vector3[vrpnChannel.Length];
            var current = new System.Numerics.Vector3[vrpnChannel.Length];
            numRepeats = new int[vrpnChannel.Length];
            perTrackerSamples = new int[vrpnChannel.Length];

            var startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
            var block = session.currentBlockNum + (startBlock - 1);

            var startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            var trial = session.currentTrialNum;
            if (startTrial > 1 & startBlock == block)
            {
                trial += startTrial - 1;
            }

            //Input 0 for each of these values, as they can't be calculated on the first round.
            for (var i = 0; i < vrpnChannel.Length; i++)
            {
                previous[i] = new System.Numerics.Vector3(0, 0, 0);
                numRepeats[i] = 0;
                perTrackerSamples[i] = 0;
            }


            // Loop while the trial is ongoing- record data if recording, notify that trial has stopped if not recording.
            while (trialOngoing)
            {
                switch (Recording)
                {
                    case true:
                    {
                        // Update samples per second in the inspector.
                        if (verbose) timeSampleRate = UpdateSampleRate(timeSampleRate, oneSecond, ref loopCount);


                        // Sample data
                        var time = System.DateTime.UtcNow - _startTime;
                        var n = 0;
                        var trackerPosition = new System.Numerics.Vector3[vrpnChannel.Length];
                        var trackerOrientation = recordOrientation ? new System.Numerics.Quaternion[vrpnChannel.Length] : null;

                        foreach (var i in vrpnChannel)
                        {
                            var p = VRPNUpdate.VrpnTrackerPos(GetTrackerAddress(trackerName), i);
                            trackerPosition[n] = new System.Numerics.Vector3(p[0], p[1], p[2]);
                            if(trackerOrientation != null)
                            {
                                var o = VRPNUpdate.VrpnTrackerQuat(GetTrackerAddress(trackerName), i);
                                trackerOrientation[n] = new System.Numerics.Quaternion(o[0], o[1], o[2], o[3]);
                            }
                            n++;
                        }


                        n = 0;
                        foreach (var t in dataList)
                        {
                            //Only record samples that differ from the previous frame.
                            if (sampleCount == 0 || (current[n] != previous[n]))
                            {
                                var row = new UXFDataRow()
                                {
                                    ("participant", session.ppid),
                                    ("block", block.ToString()),
                                    ("trial", trial.ToString()),
                                    ("tracked", tracked[n]),
                                    ("time_ms", time.TotalMilliseconds.ToString(format)),
                                    ("phase", session.CurrentTrial.settings.GetString("phase")),
                                    ("pos_x", trackerPosition[n].X.ToString(format)),
                                    ("pos_y", trackerPosition[n].Y.ToString(format)),
                                    ("pos_z", trackerPosition[n].Z.ToString(format))
                                };
                                if (trackerOrientation != null)
                                {
                                    var orientation = new UXFDataRow()
                                    {
                                        ("quat_x", trackerOrientation[n].X.ToString(format)),
                                        ("quat_y", trackerOrientation[n].Y.ToString(format)),
                                        ("quat_z", trackerOrientation[n].Z.ToString(format)),
                                        ("quat_w", trackerOrientation[n].W.ToString(format))
                                    };
                                    row.AddRange(orientation);
                                }
                                t.Add(row);
                                perTrackerSamples[n]++;
                            }
                            else
                            {
                                numRepeats[n]++;
                            }
                            n++;
                        }

                        trackerPosition.CopyTo(previous, 0);

                        loopCount++;
                        sampleCount++;

                        // Wait for 1000/recording rate ms (200hz = 5ms)
                        Thread.Sleep(1000 / recordRate);
                        break;
                    }
                    //When recording ends, if data has been collected, report the size and log that the thread has ended.
                    case false when dataList.Count > 0:
                        sampleCount = 0;

                        // Send dataList clone to a public Array, report size and clear the thread's list.
                        AssembleData(dataList);

                        dataList.Clear();

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

        private void AssembleData(List<List<UXFDataRow>> dataList)
        {
            _trialDataArrayList = new List<UXFDataRow[]>();
            var n = 0;
            foreach (var t in dataList)
            {
                _trialDataArrayList.Add(t.ToArray());
                Utilities.UXFDebugLog(tracked[n] + " tracker took " + t.Count.ToString() + " samples with " +
                                      numRepeats[n].ToString() + "repeated samples.");
                n++;
            }
        }

        private int UpdateSampleRate(int timeSampleRate, int oneSecond, ref int loopCount)
        {
            if (System.DateTime.UtcNow.Millisecond - timeSampleRate < oneSecond) return timeSampleRate;
            updatesPerSecond = loopCount;
            loopCount = 0;
            timeSampleRate = System.DateTime.UtcNow.Millisecond;
            UXF.Utilities.UXFDebugLog("[VRPN]: Samples per second: " + timeSampleRate);
            return timeSampleRate;
        }


        protected override UXFDataRow GetCurrentValues()
        {
            // Put here to prevent errors, but this tracker uses something different from GetCurrentValues. Should not run.
            Utilities.UXFDebugLogWarning("[VRPN]: GetCurrentValues() was called, but shouldn't be.");
            return new UXFDataRow();
        }

        public void StartRecording(DateTime startTime)
        {
            if(recording) return;
            _startTime = startTime;
            SetupDescriptorAndHeader();
            InitTrackingThread();
        }

        private void InitTrackingThread()
        {
            if (verbose) Utilities.UXFDebugLog("[VRPN]: Recording Start. Connecting to " + GetTrackerAddress(trackerName) + " on channel " + vrpnChannel.ToString());
            data = new UXFDataTable(header);
            if (!(updateType == TrackerUpdateType.fastUpdate & vrpnChannel.Length > 0)) return;
            recording = true;
            _collectData = new Thread(RecordMovementData);
            _collectData.Start();
            if (verbose) Utilities.UXFDebugLog("[VRPN]: Thread Started.");
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
                UXF.Utilities.UXFDebugLog("[VRPN]: No thread to Join" + e);
            }
            if (verbose) Utilities.UXFDebugLog("[VRPN]: Recording End. Disconnected from " + GetTrackerAddress(trackerName) + " on channel " + vrpnChannel.ToString());
            InitSavingThread();
        }

        private void InitSavingThread()
        {
            _saveData = new Thread(SaveMovementData);
            _saveData.Start();
        }

        private void SaveMovementData()
        {
            foreach (var i in _trialDataArrayList)
            {
                foreach (var r in i)
                {
                    data.AddCompleteRow(r);
                }
            }
            session.CurrentTrial.SaveDataTable(data, DataName, UXFDataType.Trackers);
        }

        // Abort threads when no longer needed, otherwise Unity seems to keep them open.

        private void OnDisable()
        {
            _collectData?.Abort();
            _saveData?.Abort();
        }

        private void OnApplicationQuit()
        {
            _collectData?.Abort();
            _saveData?.Abort();
        }

    }
}