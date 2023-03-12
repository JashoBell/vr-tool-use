using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UXF;

/// <summary>
/// For the index finger's start location.
/// </summary>
public class StartTriggerIndex : MonoBehaviour
{
    public bool rightIsReturned = false;
    public StartTriggers thumbTrigger;


    void OnTriggerEnter(Collider other) {
        // Determine what needs to collide with the starting position for the countdown to initiate.


        if(thumbTrigger.GetTask() == "manual_reach")
        {
            if(other.name is "indexFingerTracker" or "Bip01 R Finger1 Tip" & !rightIsReturned)
            {
                rightIsReturned = true;
                // UXF.Utilities.UXFDebugLog("rightIsReturned to true.");
            }  
            if(thumbTrigger.leftIsReturned & rightIsReturned & !thumbTrigger.isReturned) 
            {
                thumbTrigger.isReturned = true;
                // UXF.Utilities.UXFDebugLog("isReturned to true.");
            }
        }
    }
    void OnTriggerExit(Collider other) {
        if(thumbTrigger.GetTask() == "manual_reach")
        {
            if(other.name is "indexFingerTracker" or "Bip01 R Finger1 Tip" & rightIsReturned)
            {
                rightIsReturned = false;
            } 
            if((!thumbTrigger.leftIsReturned || !rightIsReturned) & thumbTrigger.isReturned)
            {
                thumbTrigger.isReturned = false;
                // UXF.Utilities.UXFDebugLog("isReturned to false.");
            }
        }
    }
}
