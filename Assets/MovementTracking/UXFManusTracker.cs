using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Manus;
using UnityEngine;
using UXF;
using static Manus.CoreSDK;
using Skeleton = Manus.Skeletons.Skeleton;
using SDK_Skeleton = Manus.CoreSDK.Skeleton;
//
namespace MovementTracking
{
    /// <summary>
    /// Provides a Manus SDK tracker for use with the UXF framework. Records through the Manus Core SDK.
    /// To use, requires slight modification of Manus Core SDK code.
    /// </summary>
    public class UXFManusTracker : Tracker
    {
        private static UXFManusTracker _instance;
        public static UXFManusTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<UXFManusTracker>();
                }
                return _instance;
            }
        }

        public bool coreConnected = false;
        public List<Manus.Skeletons.Skeleton> manusSkeletons;
        public Session session;
        private static int _block, _trial, _startBlock, _startTrial; 
        private DateTime _startTime, _timeSampleRate;
        private static string _ppid, _phase;
        private static List<List<UXFDataRow>> _dataTables;
    
    
        private Thread _collectData, _saveData;

        public bool verbose = false;

        [SerializeField] public int threadedUpdatesPerSecond = 0;
        [SerializeField] private static int _samplesTaken = 0;
        [SerializeField] private static int _preSamplesTaken = 0;
        private const string Format = "F4";
        private const int OneSecond = 1000;

    
        /// <summary>
        /// A struct of the key information for recording via the Manus SDK.
        /// </summary>

        private struct SkeletonRecordingStruct
        {  
            // The name of the object the skeleton is attached to.
            public string Name;
            // The index ID of the skeleton.
            public uint SkeletonID;
            // The number of nodes in the skeleton.
            public uint NodeCount;
        
            public Dictionary<uint, string> NodeNames;
            // A dictionary of the tip nodes of each chain in the skeleton.
            public Dictionary<uint, string> TipNodes;

        
            // A list of UXFDatarows for the recorded data.
            public List<UXFDataRow> DataTable;
        
        }

        private List<SkeletonRecordingStruct> _skeletonRecordingStructs;

        /// <summary>
        /// An enum specifying which nodes to record, with options for all nodes, tip nodes, index and thumb tips only, or no nodes.
        /// At the moment, the nodes record in local space, so all nodes should be recorded.
        /// </summary>
        public enum NodeRecordingOption
        {
            AllNodes,
            TipNodes,
            IndexAndThumbtip,
            NoNodes
        }

        /// <summary>
        /// Serialized field specifying which nodes to record, with options for all nodes, tip nodes, or no nodes.
        /// All nodes records all of the bones in the skeletons specified in the ManusSkeletons field (e.g. the full hand of a glove-only skeleton).
        /// Tip nodes records only the tip nodes of each chain in the skeletons specified in the ManusSkeletons field (e.g. the fingertips of a glove-only skeleton).
        /// No nodes records no nodes.
        /// </summary>
        public NodeRecordingOption nodeRecordingOption;
    
    
        protected override void SetupDescriptorAndHeader()
        {
            measurementDescriptor = "movement_manus";
            // The header for the data file. Each entry corresponds to a column in the data file.
            // These are used by UXF as the keys for the data row.
            customHeader = new[]
            {
                "participant",
                "block",
                "trial",
                "phase",
                "skeleton_name",
                "publish_time",
                "time_ms",
                "node_id",
                "pos_x",
                "pos_y",
                "pos_z",
                "rot_w",
                "rot_x",
                "rot_y",
                "rot_z",
                "scale_x",
                "scale_y",
                "scale_z"
            };
        }
    
        private string DataName
        {
            get
            {
                Debug.AssertFormat(measurementDescriptor.Length > 0, "No measurement descriptor has been specified for this Tracker!");
                return string.Join("_", new[]{session.CurrentBlock.settings.GetString("task"), objectName, measurementDescriptor});
            }
        }        
    
        // This is unused here but required by the Tracker class.
        protected override UXFDataRow GetCurrentValues()
        {
            return new UXFDataRow();
        }


        /// <summary>
        /// Creates structs for each skeleton marked for recording.
        /// Fills the structs with info from the skeleton objects.
        /// Adds the structs to a list.
        /// </summary>
        /// <param name="skeletons">The skeletons to record</param>
        /// <returns>A list of SkeletonRecordingStructs</returns>
        private static List<SkeletonRecordingStruct> InitRecordingStructs(IEnumerable<Skeleton> skeletons)
        {
            var recordingStructs = new List<SkeletonRecordingStruct>();
            try
            {
                foreach (var skeleton in skeletons)
                {
                    var skeletonData = skeleton.skeletonData;
                    var skeletonRecordingStruct = new SkeletonRecordingStruct
                    {
                        Name = skeleton.gameObject.name,
                        SkeletonID = skeletonData.id,
                        NodeCount = (uint)skeletonData.nodes.Count,
                        NodeNames = new Dictionary<uint, string>(),
                        TipNodes = new Dictionary<uint, string>(),
                        DataTable = new List<UXFDataRow>()
                    };
                    foreach (var node in skeletonData.nodes)
                    {
                        skeletonRecordingStruct.NodeNames.Add(node.id, node.name);
                    }
                    foreach (var chain in skeletonData.chains)
                    {
                        var tipNodeID = chain.nodeIds.Last();
                        skeletonRecordingStruct.TipNodes.Add(tipNodeID, chain.name);
                    }
                    recordingStructs.Add(skeletonRecordingStruct);
                }
            } catch (Exception e) {
                Debug.Log(e);
            }

            return recordingStructs;
        }

    

        /// <summary>
        /// Called from Manus.CommunicationHub.OnSkeletonUpdate() when the skeleton data is updated from Manus Core.
        /// Parses the data and adds it to tables.
        /// </summary>
        /// <param name="streamedSkeleton"></param>
        public void OnSkeletonStreamUpdate(SDK_Skeleton streamedSkeleton, Timestamp timestamp)
        {
            _preSamplesTaken++;
            // If the recording is not ongoing, return.
            if (!Recording) return;
            if(verbose)
            {
                TimeSampleRate(ref _samplesTaken);
                _samplesTaken++;
            }
            try{
                // For each skeleton in _skeletonRecordingStructs, find the matching skeleton in the skeleton stream using the id.
                // If the skeleton is found, add the data to the data rows.
                int n = 0;
                // Find the skeletonRecordingStruct that matches the streamed skeleton by using the id.

                uint skeletonID = streamedSkeleton.id;
        
                foreach (var skeleton in _skeletonRecordingStructs)
                {
                    if (skeleton.SkeletonID != skeletonID) continue;

                    var nodeData = streamedSkeleton.nodes;
                    var dataRow = new string[]
                    {
                        _ppid.ToString(),
                        _block.ToString(),
                        _trial.ToString(),
                        session.CurrentTrial.settings.GetString("phase"),
                        skeleton.Name,
                        timestamp.time.ToString(),
                        (DateTime.UtcNow - _startTime).TotalMilliseconds.ToString()
                    };
                    var dataRows = RecordNodes(nodeData, dataRow, skeleton.NodeNames, skeleton.TipNodes);
                    skeleton.DataTable.AddRange(dataRows);
                    n++;
                }
            }
            catch (Exception e)
            {
                UXF.Utilities.UXFDebugLog(
                    "[Manus]: Error on sampling skeleton data: " 
                    + e 
                    + ". Node count: " 
                    + streamedSkeleton.nodes.Count().ToString() 
                    + ".");
            }
        }
    
        /// <summary>
        /// Depending on the node recording option, records the transform of all nodes or only the tip nodes,
        /// or just the thumb and index finger (or records nothing).
        /// </summary>
        /// <param name="nodeData">The array of node data received from Manus Core</param>
        /// <param name="dataRow">A UXFDataRow of the trial-stable variables</param>
        /// <param name="skeleton">The skeleton associated with the nodes.</param>
        /// <returns></returns>
        private List<UXFDataRow> RecordNodes(SkeletonNode[] nodeData, string[] dataRow, Dictionary<uint, string> skeletonNodeNames, Dictionary<uint, string> skeletonTipNodes)
        {
            List<UXFDataRow> nodeRows = new List<UXFDataRow>();
            switch (nodeRecordingOption)
            {
                case NodeRecordingOption.AllNodes:
                    nodeRows.AddRange(nodeData.Select(node => RecordSingleNodeTransform(dataRow, node, skeletonNodeNames[node.id])));
                    break;
                case NodeRecordingOption.TipNodes:
                    //Only records the data from nodes which correspond to the keys of the TipNodes dictionary.
                    nodeRows.AddRange(
                        from node in nodeData 
                        where skeletonTipNodes.Keys.Contains(node.id) 
                        select RecordTipNodeTransform(dataRow, node, skeletonTipNodes[node.id]));
                    break;
                case NodeRecordingOption.IndexAndThumbtip:
                    //Only records the nodes wchich correspond to the keys of the tipNodes dictionary values "Index" and "Thumb".
                    nodeRows.AddRange(
                        from node in nodeData 
                        where skeletonTipNodes.Keys.Contains(node.id) 
                              && (skeletonTipNodes.Values.Contains("Index") || skeletonTipNodes.Values.Contains("Thumb")) 
                        select RecordTipNodeTransform(dataRow, node, skeletonTipNodes[node.id]));
                    break;
                case NodeRecordingOption.NoNodes:
                    break;
                default:
                    break;
            }

            return nodeRows;
        }

        /// <summary>
        /// Records the transforms of a node.
        /// </summary>
        /// <param name="dataRow">A UXFDataRow of variables that will remain stable across nodes for this execution.</param>
        /// <param name="node">The node being recorded</param>
        /// <returns>A UXFDataRow of the recorded transforms.</returns>
        private static UXFDataRow RecordSingleNodeTransform(string[] dataRow, SkeletonNode node, string nodeName)
        {
            try
            {
                var nodePosition = node.transform.position;
                var nodeRotation = node.transform.rotation;
                var nodeScale = node.transform.scale;
                UXFDataRow nodeData = new UXFDataRow
                {
                    ("participant", dataRow[0]),
                    ("block", dataRow[1]),
                    ("trial", dataRow[2]),
                    ("phase", dataRow[3]),
                    ("skeleton_name", dataRow[4]),
                    ("publish_time", dataRow[5]),
                    ("time_ms", dataRow[6]),
                    ("node_id", nodeName),
                    ("pos_x", nodePosition.x.ToString(Format)),
                    ("pos_y", nodePosition.y.ToString(Format)),
                    ("pos_z", nodePosition.z.ToString(Format)),
                    ("rot_w", nodeRotation.w.ToString(Format)),
                    ("rot_x", nodeRotation.x.ToString(Format)),
                    ("rot_y", nodeRotation.y.ToString(Format)),
                    ("rot_z", nodeRotation.z.ToString(Format)),
                    ("scale_x", nodeScale.x.ToString(Format)),
                    ("scale_y", nodeScale.y.ToString(Format)),
                    ("scale_z", nodeScale.z.ToString(Format))
                };
                return nodeData;
            }
            catch (Exception e)
            {
                UXF.Utilities.UXFDebugLog("[Manus]: Error on recording node " + nodeName + " data: " + e + ".");
                return null;
            }
        }

        private static UXFDataRow RecordTipNodeTransform(string[] dataRow, SkeletonNode node, string tipNodeName)
        {
            var nodePosition = node.transform.position;
            var nodeRotation = node.transform.rotation;
            var nodeScale = node.transform.scale;
            UXFDataRow nodeData = new UXFDataRow
            {
                ("participant", dataRow[0]),
                ("block", dataRow[1]),
                ("trial", dataRow[2]),
                ("phase", dataRow[3]),
                ("skeleton_name", dataRow[4]),
                ("publish_time", dataRow[5]),
                ("time_ms", dataRow[6]),
                ("node_id", tipNodeName),
                ("pos_x", nodePosition.x.ToString(Format)),
                ("pos_y", nodePosition.y.ToString(Format)),
                ("pos_z", nodePosition.z.ToString(Format)),
                ("rot_w", nodeRotation.w.ToString(Format)),
                ("rot_x", nodeRotation.x.ToString(Format)),
                ("rot_y", nodeRotation.y.ToString(Format)),
                ("rot_z", nodeRotation.z.ToString(Format)),
                ("scale_x", nodeScale.x.ToString(Format)),
                ("scale_y", nodeScale.y.ToString(Format)),
                ("scale_z", nodeScale.z.ToString(Format))
            };
            return nodeData;
        }

        private void TimeSampleRate(ref int numSamples)
        {
            // Sets number of ms for one second, for displaying samples/second.
            DateTime currentTime = DateTime.UtcNow;
            if ((currentTime - _timeSampleRate).TotalMilliseconds < OneSecond) return;
            threadedUpdatesPerSecond = numSamples;
            numSamples = 0;
            _timeSampleRate = currentTime;
            UXF.Utilities.UXFDebugLog("[Manus]: Samples per second: " + threadedUpdatesPerSecond);
        }

        public void StartRecording(DateTime startTime)
        {
            if(recording) return;
            try
            {SetupDescriptorAndHeader();
                data = new UXFDataTable(header);
                CoreSDK.GetNumberOfAvailableUsers(out var numberOfUsers);
                if (numberOfUsers == 0)
                {
                    Debug.LogError("[Manus]: Manus Core not connected");
                    return;
                }
                CoreSDK.m_UXFManusTracker = this;
                if (verbose) Utilities.UXFDebugLog("[Manus]: Recording Start. Connecting to Manus Core");
            } catch (Exception e)
            {
                Debug.LogError("[Manus]: Error on StartRecording: " + e);
            }

            // Get the stable details of the current trial.
            _ppid = session.ppid;
            
            _startBlock = Convert.ToInt32(session.participantDetails["startblock"]);
            _block = session.currentBlockNum + (_startBlock - 1);

            _startTrial = Convert.ToInt32(session.participantDetails["starttrial"]);
            _trial = session.currentTrialNum + (_startTrial - 1);

            _startTime = startTime;
            _timeSampleRate = startTime;
            
            // Create the structs for the Manus data.
            _skeletonRecordingStructs = InitRecordingStructs(manusSkeletons);

            recording = true;

            Utilities.UXFDebugLog("[Manus]: Thread Started.");
        }
    
        public override void StopRecording()
        {
            recording = false;
            if (verbose) Utilities.UXFDebugLog("[Manus]: Recording End. Disconnected from Manus SDK.");
            if (verbose) Utilities.UXFDebugLog("[Manus]: Samples recorded at " 
                                  + (
                                      _skeletonRecordingStructs[0].DataTable.Count
                                      /
                                      _skeletonRecordingStructs[0].NodeCount
                                  )
                                  /
                                  (
                                      (DateTime.UtcNow - _startTime).TotalMilliseconds/1000
                                  ) 
                                  + " Hz on average.");
            InitSavingThread();
        }

        private void InitSavingThread()
        {
            try
            {
                _saveData = new Thread(SaveMovementData)
                {
                    IsBackground = true
                };
                _saveData.Start();
            }
            catch (Exception e)
            {
                Debug.Log("Error on data saving thread:" + e);
            }
        }

        private void SaveMovementData()
        {
            try
            {
                int n = 0;
                foreach (var recordedSkeleton in _skeletonRecordingStructs)
                {
                    var table = recordedSkeleton.DataTable;
                    var compiledTable = new UXFDataTable(header);
                    foreach (var row in table)
                    {
                        compiledTable.AddCompleteRow(row);
                    }
                    session.CurrentTrial.SaveDataTable(
                        compiledTable, 
                        DataName + "_" + _skeletonRecordingStructs[n].Name, 
                        UXFDataType.Trackers
                    );
                    n++;
                }
            }
            catch (Exception e)
            {
                UXF.Utilities.UXFDebugLog("Error saving data: " + e);
            }
        
        }
   
        // When the gameobject is disabled (e.g. at the end of a block) stop the tracking threads.
        private void OnDisable()
        {
            _collectData?.Abort();
            _saveData?.Abort();
        }

        // When the application ends (e.g. at the end of the experiment) stop the tracking threads.
        private void OnApplicationQuit()
        {
            _collectData?.Abort();
            _saveData?.Abort();
        }

    
    }
}