using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UXF;

/// <summary>
/// Handles the collision triggers that determine whether the participant has returned to the starting location or not.
/// </summary>
public class StartTriggers : MonoBehaviour
{
    public bool isReturned = false;
    public bool leftIsReturned = false;
    public bool rightIsReturned = false;
    public bool isDuringBlock = false;
    public enum Side {left, right};
    public Side side = Side.right;
    public ReachingTask reachingTask;
    public string _task = "";
    public Session session;


    public string GetTask() {
        //For the first trial of the block, the "current task" is the previous one, or nothing if it's the first block.
        //In these situations, point to the "next" block, which is starting.
        string task;
        isDuringBlock = reachingTask.isDuringBlock;
        if(session.currentBlockNum == 0 || session.CurrentBlock.settings.GetString("task") == "landmark" || ((session.CurrentTrial == session.CurrentBlock.lastTrial) & !isDuringBlock))
        {
            task = session.blocks[session.currentBlockNum].settings.GetString("task");
        } 
        else 
        {
            task = session.blocks[session.currentBlockNum - 1].settings.GetString("task");
        }
        _task = task;
        return task;
    }

    void OnTriggerEnter(Collider other) {
        // Determine what needs to collide with the starting position for the countdown to initiate.
        switch (_task){
            case "tool_reach":
                switch (other.name)
                {
                    case "Bip01 L Finger1 Tip" or "rightClamp":
                        rightIsReturned = true;
                        // UXF.Utilities.UXFDebugLog("rightIsReturned to true.");
                        break;
                    case "Bip01 L Finger0 Tip" or "leftClamp":
                        leftIsReturned = true;
                        // UXF.Utilities.UXFDebugLog("leftIsReturned to true.");
                        break;
                } 
                if(leftIsReturned && rightIsReturned) 
                {
                    isReturned = true;
                    // UXF.Utilities.UXFDebugLog("isReturned to true.");
                }
                break;

            case "manual_reach":
                if(other.name is "Bip01 L Finger1 Tip" or "Bip01 R Finger1 Tip" & !rightIsReturned)
                {
                    rightIsReturned = true;
                    // UXF.Utilities.UXFDebugLog("rightIsReturned to true.");
                } 
                else if(other.name is "Bip01 L Finger0 Tip" or "Bip01 R Finger0 Tip" & !leftIsReturned)
                {
                    leftIsReturned = true;
                    // UXF.Utilities.UXFDebugLog("leftIsReturned to true.");
                } 

                if(leftIsReturned && rightIsReturned && !isReturned) 
                {
                    isReturned = true;
                    // UXF.Utilities.UXFDebugLog("isReturned to true.");
                }
                break;
        }
    }
    void OnTriggerExit(Collider other) {
        if(session.hasInitialised)
        {
            GetTask();
        }
        switch (_task)
        {
            case "tool_reach":
                if(other.name is "Bip01 L Finger1 Tip" or "rightClamp" & rightIsReturned)
                {
                    rightIsReturned = false;
                    // UXF.Utilities.UXFDebugLog("rightIsReturned to false.");
                } 
                else if(other.name is "Bip01 L Finger0 Tip" or "leftClamp" & leftIsReturned)
                {
                    leftIsReturned = false;
                    // UXF.Utilities.UXFDebugLog("leftIsReturned to false.");
                } 

                if((!leftIsReturned || !rightIsReturned) & isReturned) 
                {
                    isReturned = false;
                    // UXF.Utilities.UXFDebugLog("isReturned to false.");
                }
                break;

            case "manual_reach":
                if(other.name is "Bip01 L Finger1 Tip" or "Bip01 R Finger1 Tip" & rightIsReturned)
                {
                    rightIsReturned = false;
                    // UXF.Utilities.UXFDebugLog("rightIsReturned to false.");
                } 
                else if(other.name is "Bip01 L Finger0 Tip" or "Bip01 R Finger0 Tip" & leftIsReturned) 
                {
                    leftIsReturned = false;
                    // UXF.Utilities.UXFDebugLog("leftIsReturned to false.");
                } 

                if((!leftIsReturned || !rightIsReturned) & isReturned)
                {
                    isReturned = false;
                    // UXF.Utilities.UXFDebugLog("isReturned to false.");
                }
                break;
        }
    }
}
