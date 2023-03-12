using UnityEngine;
using UXF;

namespace MovementTracking
{
    public class TrackerControlHub : MonoBehaviour
    {
        public GameObject viveToolTracker;
        public GameObject openVRCalibrationTracker;
        public GameObject pptToolTracker;
        public GameObject stimulusTracker;
        public GameObject handRecorders, toolRecorders;
        public Session session;
        private TaskIndicators _indicatorPanel;
        public ButtonScript buttonScript;
        public bool trackerFindClicked;

        private void Start()
        {
            if(GameObject.Find("openVROriginTracker") == null)
            {
                openVRCalibrationTracker.SetActive(true);
            }
        }

        /// <summary>
        /// Enable the trackers appropriate for the upcoming task.
        /// </summary>
        public void EnableTrackers()
        {
            var nextTask = session.blocks[session.currentBlockNum].settings.GetString("task");
        
            switch(nextTask)
            {
                case "manual_reach":
                    stimulusTracker.SetActive(true);
                    handRecorders.SetActive(true);
                    break;
                case "tool_reach":
                    pptToolTracker.SetActive(true);
                    viveToolTracker.SetActive(true);
                    if(!viveToolTracker.GetComponent<SteamVRTrackedObjectPlus>().assigned)
                    {
                        viveToolTracker.GetComponent<SteamVRTrackedObjectPlus>().FindTracker();
                    }
                    stimulusTracker.SetActive(true);
                    toolRecorders.SetActive(true);
                    break;
                case "landmark":
                    break;
                default:
                    break;
            }
            trackerFindClicked = true;
        }

        /// <summary>
        /// Disable trackers from the previous task in transitioning to the new one.
        /// </summary>
        public void DisableTrackers()
        {
            if (session.currentBlockNum == 0) return;
            trackerFindClicked = false;
            var previousTask = session.blocks[session.currentBlockNum-1].settings.GetString("task");
            switch(previousTask)
            {
                case "manual_reach":
                    stimulusTracker.SetActive(false);
                    handRecorders.SetActive(false);
                    break;
                case "tool_reach":
                    pptToolTracker.SetActive(false);
                    viveToolTracker.SetActive(false);
                    stimulusTracker.SetActive(false);
                    toolRecorders.SetActive(false);
                    break;
                default:
                    break;
            }
            buttonScript.takingInputs = true;
        }
    
    
    }
}
