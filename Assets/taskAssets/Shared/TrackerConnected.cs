using System;
using System.Collections;
using System.Numerics;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using TMPro;
using BOLL7708;
using MovementTracking;
using UXF;


public class TrackerConnected : MonoBehaviour
{
    private Image _panel;
    public enum TrackerEnum {WristShaftPpt, LeftProngPpt, IndexRightProngPpt, ElbowSteamVR, WristSteamVR, ToolSteamVR, OriginSteamVR, BackSteamVR, StimulusRectangle, Controller};
    public TrackerEnum tracker;
    public TextMeshProUGUI text;
    public Session session;
    private SteamVRTrackedObjectPlus _steamVRTracker;
    private System.Numerics.Vector3 _previousFramePosition = System.Numerics.Vector3.Zero;
    public bool found = false;
    public bool updating = false;
    private BOLL7708.EasyOpenVRSingleton _trackingInstance;
    
    private void Start() {
        if(GameObject.Find("researcherCamera") == null) return;
        //On start, grab the image component associated with the gameobject this script is attached to
        _panel = gameObject.GetComponent<Image>();
        // If one of the calibration trackers or the controller, activate immediately.
        if((tracker == TrackerEnum.OriginSteamVR || tracker == TrackerEnum.BackSteamVR || tracker == TrackerEnum.Controller))
        {
            StartCoroutine(TrackerActivityCheck());
        }
    }

    /// <summary>
    /// Coroutine that reads activity of the tracker associated with the enum chosen for this object and changes the indicator's color based on the result.
    /// </summary>
    /// <returns></returns>
    public IEnumerator TrackerActivityCheck()
    {
        while(_panel.isActiveAndEnabled)
        {
            switch (tracker)
            {
                case TrackerEnum.WristShaftPpt:
                    if(VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 0) != _previousFramePosition)
                    {
                        _panel.color = Color.green;
                        found = true;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    _previousFramePosition = VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 0);
                    break;
                case TrackerEnum.LeftProngPpt:
                    if(VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 2) != _previousFramePosition)
                    {
                        _panel.color = Color.green;
                        found = true;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    _previousFramePosition = VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 2);
                    break;
                case TrackerEnum.IndexRightProngPpt:
                    if(VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 1) != _previousFramePosition)
                    {
                        _panel.color = Color.green;
                        found = true;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    _previousFramePosition = VRPNUpdate.VrpnTrackerVector3("PPT0@192.168.0.12", 1);
                    break;
                case TrackerEnum.ElbowSteamVR:
                    FindTracker("LHR-41DC9F4C");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    break;
                case TrackerEnum.WristSteamVR:
                    FindTracker("LHR-50E26EDE");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    break;
                case TrackerEnum.ToolSteamVR:
                    FindTracker("LHR-14AC034A");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    break;
                case TrackerEnum.OriginSteamVR:
                    FindTracker("LHR-98C0C3E8");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                    }
                    break;
                case TrackerEnum.Controller:
                    FindTracker("LHR-1979A41D");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                    }
                    break;
                case TrackerEnum.StimulusRectangle:
                    FindTracker("LHR-47D7AE42");
                    if(found && updating)
                    {
                        _panel.color = Color.green;
                    } else
                    {
                        _panel.color = Color.red;
                        found = false;
                    }
                    break;
            }
            yield return new WaitForSeconds(1f);
        }
        yield break;
    }

    /// <summary>
    ///  For SteamVR trackers, checks whether the desired serial number is represented in the steamvr object indices.
    /// </summary>
    /// <param name="desiredSerialNumber">The serial number of the tracker.</param>
    public void FindTracker(string desiredSerialNumber)
        {
            _trackingInstance ??= EasyOpenVRSingleton.Instance;
            if(!_trackingInstance.IsInitialized()) _trackingInstance.Init();
            TrackedDevicePose_t[] trackerPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            trackerPoses = _trackingInstance.GetDeviceToAbsoluteTrackingPose(ref trackerPoses);

            ETrackedPropertyError error = new ETrackedPropertyError();
            StringBuilder sb = new StringBuilder();
            int steamVRTrackerIndex = 0;
            for (int i = 0; i < SteamVR.connected.Length; ++i)
            {
                try
                {
                    OpenVR.System.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
                    var serialNumber = sb.ToString();
                    if (serialNumber == desiredSerialNumber)
                    {
                        found = true;
                        steamVRTrackerIndex = i;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log("[Tracker Connectivity]: " + e.Message);
                }
            }



            if(found)
            {
                updating = trackerPoses[steamVRTrackerIndex].bDeviceIsConnected;
            } 
            else
            {
                UnityEngine.Debug.Log("[Tracker Connectivity]: Couldn't find a device with SN \"" + desiredSerialNumber + "\"");
            }
        }

        /// <summary>
        /// Indicates the activation status of a tracker when it is relevant for the upcoming task.
        /// </summary>
        public void BeginSearching()
        {
            //If this is before trials have started or is the last trial of the block
            if((session.currentBlockNum == 0 ||
                session.NextTrial.block != session.CurrentTrial.block) & !session.InTrial)
            {
                //Make sure only relevant tracker panels are activated.
                if(
                    //Landmark task
                    ((session.NextTrial.block.settings.GetString("task") == "landmark") & (tracker == TrackerEnum.WristSteamVR || tracker == TrackerEnum.ElbowSteamVR)) ||
                    //Manual reaching task
                    ((session.NextTrial.block.settings.GetString("task") == "manual_reach") & (tracker == TrackerEnum.WristSteamVR || tracker == TrackerEnum.WristShaftPpt || tracker == TrackerEnum.StimulusRectangle)) ||
                    //Tool-use task
                    ((session.NextTrial.block.settings.GetString("task") == "tool_reach") & (tracker == TrackerEnum.ToolSteamVR || tracker == TrackerEnum.WristShaftPpt || tracker == TrackerEnum.LeftProngPpt || tracker == TrackerEnum.IndexRightProngPpt || tracker == TrackerEnum.StimulusRectangle)) ||
                    //Calibration trackers and controller, always on
                    tracker == TrackerEnum.OriginSteamVR)
                {
                    _panel.enabled = true;
                    try{text.enabled = true;}
                    catch(NullReferenceException e)
                    {
                        UXF.Utilities.UXFDebugLogWarning("No text assigned to" + gameObject.name + e);
                    }

                    StartCoroutine(TrackerActivityCheck());
                }
            }
        }

        public void EndSearching()
        {
            StopCoroutine(TrackerActivityCheck());
            _panel.enabled = false;
            try{text.enabled = false;}
            catch(NullReferenceException e)
            {
                UXF.Utilities.UXFDebugLogWarning("No text assigned to" + gameObject.name + e);
            }
            found = false;
        }
}
