using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UXF;


/// <summary>
/// Class that handles verifying whether the arm is placed within the starting location or not.
/// </summary>
public class LandmarkStartTrigger : MonoBehaviour
{
    public bool armIsPlaced;
    public bool elbowIsPlaced;
    public bool wristIsPlaced;
    // Start is called before the first frame update
    private void OnTriggerEnter(Collider other) {
        if(other.name == "right_hand_tracker"& !wristIsPlaced){
                wristIsPlaced = true;
                UXF.Utilities.UXFDebugLog("wristIsPlaced to true.");
        } else if(other.name == "right_elbow_tracker" & !elbowIsPlaced) {
            elbowIsPlaced = true;
            UXF.Utilities.UXFDebugLog("elbowIsPlaced to true.");
        }

        if (!elbowIsPlaced || !wristIsPlaced || armIsPlaced) return;
        armIsPlaced = true;
        UXF.Utilities.UXFDebugLog("armIsPlaced to true.");
    }

    public void ResetTriggers()
    {
        armIsPlaced = false;
        elbowIsPlaced = false;
        wristIsPlaced = false;
    }

    private void OnTriggerExit(Collider other) {
        if(other.name == "right_hand_tracker"& wristIsPlaced){
                wristIsPlaced = false;
                UXF.Utilities.UXFDebugLog("wristIsPlaced to false.");
        } else if(other.name == "right_elbow_tracker" & elbowIsPlaced) {
            elbowIsPlaced = false;
            UXF.Utilities.UXFDebugLog("elbowIsPlaced to false.");
        }

        if ((other.name != "wristViveTracker" && other.name != "elbowViveTracker") || !elbowIsPlaced ||
            !wristIsPlaced || !armIsPlaced) return;
        armIsPlaced = false;
        UXF.Utilities.UXFDebugLog("armIsPlaced to false.");
    }
}
