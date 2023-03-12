//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: For controlling in-game objects with tracked devices.
// - Edited by jeffcrouse @ github.com/jeffcrouse
// - Added ability to specify a serial number to assign to the TrackedObject
// - I (jashobell @ github.com/jashobell) made minor modifications to fit my specific use-case
//=============================================================================

using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR;
using Valve.VR;

namespace MovementTracking
{
    [System.Serializable]
    public class TimedPose : System.Object
    {
        public HmdMatrix34_t Mat;
        public long time;

        public TimedPose(HmdMatrix34_t mat, long time)
        {
            this.Mat = mat;
            this.time = time; 
        }
    }

    public class SteamVRTrackedObjectPlus : MonoBehaviour
    {
        public enum EIndex
        {
            None = -1,
            Hmd = (int)OpenVR.k_unTrackedDeviceIndex_Hmd,
            Device1,
            Device2,
            Device3,
            Device4,
            Device5,
            Device6,
            Device7,
            Device8,
            Device9,
            Device10,
            Device11,
            Device12,
            Device13,
            Device14,
            Device15,
            Device16
        }

        public EIndex index;

        public enum DeviceType
        {
            HMD,
            ViveController,
            ViveTrackerThree,
            ViveTrackerTwo,
            Unspecified
        }

        public DeviceType deviceType = DeviceType.Unspecified;

        // The height of the device origin when placed on a flat surface
        private Dictionary<DeviceType, float> deviceHeights = new Dictionary<DeviceType, float>()
        {
            {DeviceType.HMD, 0.0f},
            {DeviceType.ViveController, 0.013f}, // When upside down!
            {DeviceType.ViveTrackerThree, 0.00907f},
            {DeviceType.ViveTrackerTwo, 0.0086f},
            {DeviceType.Unspecified, 0.0f}
        };

        [Tooltip("If not set, relative to parent")]
        [SerializeField] private Transform origin;

        [Tooltip("If true, the offset and rotationOffset will be applied to the tracker's local position and rotation.")]
        [SerializeField] private bool individualCalibration = false;
        public Vector3 offset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;
        public bool _originAssigned = false;
        private bool IsValid { get; set; }
        [Tooltip("Whether the tracker has been successfully assigned to an index.")]
        public bool assigned = false;

        private List<TimedPose> _timedPoses = new ();
        [FormerlySerializedAs("DesiredSerialNumber")] [Tooltip("The serial number you wish to be associated with this tracker object")]
        public string desiredSerialNumber = "";
        public int indexOfTracker;
        private bool _coroutineStarted = false;

        /// <summary>
        /// Called when SteamVR provides new pose (i.e. position, orientation) data.
        /// </summary>
        /// <param name="poses">An array of poses from the SteamVR devices.</param>
        private void OnNewPoses(TrackedDevicePose_t[] poses)
        {
            if (index == EIndex.None)
                return;


            var i = (int)index;

            IsValid = false;
            if (poses.Length <= i)
                return;

            if (!poses[i].bDeviceIsConnected)
                return;

            if (!poses[i].bPoseIsValid)
                return;

            IsValid = true;

            var pose = new SteamVR_Utils.RigidTransform(poses[i].mDeviceToAbsoluteTracking);
            var objtransform = transform;
            if (_originAssigned)
            {
                if(individualCalibration)
                {
                    objtransform.localPosition = offset != Vector3.zero? offset + origin.transform.TransformPoint(pose.pos) : origin.transform.TransformPoint(pose.pos);
                    objtransform.localRotation = rotationOffset != Vector3.zero? Quaternion.Euler(rotationOffset) * origin.transform.rotation * pose.rot : origin.transform.rotation * pose.rot;
                }
                else
                {
                    objtransform.position = origin.transform.TransformPoint(pose.pos);
                    objtransform.rotation = origin.rotation * pose.rot;
                }
            }
            else
            {
                if(individualCalibration)
                {
                    objtransform.localPosition = offset != Vector3.zero? offset + pose.pos : pose.pos;
                    objtransform.localRotation = rotationOffset != Vector3.zero? Quaternion.Euler(rotationOffset) * pose.rot : pose.rot;
                }
                else
                {
                    objtransform.localPosition = pose.pos;
                    objtransform.localRotation = pose.rot;
                }
            }
        }
        


        private SteamVR_Events.Action _newPosesAction;

        private SteamVRTrackedObjectPlus()
        {
            _newPosesAction = SteamVR_Events.NewPosesAction(OnNewPoses);
        }


        public void CalibrateHeightOffset(float expectedHeight)
        {
            var height = transform.position.y;
            var heightOffset = expectedHeight - height;
            var newOffset = heightOffset + deviceHeights[deviceType];
            Debug.Log("Calibrated height offset for " + gameObject.name + " to " + newOffset);
            this.offset = new Vector3(offset.x, newOffset, offset.z);
        }

        /// <summary>
        /// Iterates through the list of active SteamVR objects, comparing their SN to the desired one and attaching the one that matches to this object.
        /// </summary>
        public void FindTracker()
        {
            if (assigned) return;
            ETrackedPropertyError error = new();
            StringBuilder sb = new();
            for (var i = 0; i < SteamVR.connected.Length; ++i)
            {

                OpenVR.System.GetStringTrackedDeviceProperty((uint)i, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
                var serialNumber = sb.ToString();
                if (serialNumber == desiredSerialNumber)
                {
                    UnityEngine.Debug.Log("Assigning device " + i + " to " + gameObject.name + " (" + desiredSerialNumber +")");
                    SetDeviceIndex(i);
                    indexOfTracker = i;
                    assigned = true;
                    _originAssigned = origin != null;
                }
                // If there is nothing connected, SN is blank. Listing SNs may help in identifying the ones you want to assign.
                // SN for vive trackers can be found in SteamVR under "Manage Trackers"
                // else if (serialNumber != "")
                // {
                //     print("Serial number " + SerialNumber + "found at index " + i);
                // }
            }

            // if(!assigned)
            // {
            //     UnityEngine.Debug.Log("Couldn't find a device with Serial Number \"" + desiredSerialNumber + "\"");
            // }
        }


        private IEnumerator FindTrackerCoroutine()
        {
            _coroutineStarted = true;
            if (SteamVR.initializedState == SteamVR.InitializedStates.None)
            {
                SteamVR.Initialize();
            }

            while (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
                yield return null;

            while (!assigned)
            {
                FindTracker();

                if (!assigned)
                {
                    yield return new WaitForSeconds(10f);
                    continue;
                }
            }

            var render = SteamVR_Render.instance;
            if (render == null)
            {
                enabled = false;
                yield break;
            }

            _newPosesAction.enabled = true;
            _coroutineStarted = false;
        }

        public void OnEnable()
        {
            if(!assigned & !_coroutineStarted) StartCoroutine(FindTrackerCoroutine());
        }

        public void Start()
        {    
            if(!assigned & !_coroutineStarted) StartCoroutine(FindTrackerCoroutine());
        }

        private void OnDisable()
        {
            assigned = false;
            _newPosesAction.enabled = false;
            _coroutineStarted = false;
            IsValid = false;
        }

        private void SetDeviceIndex(int index)
        {
            if (System.Enum.IsDefined(typeof(EIndex), index))
                this.index = (EIndex)index;
        }

        private void OnApplicationQuit() {
            if (XRSettings.loadedDeviceName != "None") {
                XRSettings.LoadDeviceByName("None");
            }
        }
    }
}