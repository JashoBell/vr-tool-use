using System.Collections;
using System.Collections.Generic;
using MovementTracking;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class ButtonScript : MonoBehaviour
{
    private Button _button;
    private StimuliSetup _stimuliSetup;
    private InstructionsDisplay _instructionsDisplay;
    private TrackerControlHub _trackerControlHub;
    private Keyboard keyboard;
    public bool takingInputs = false;
    // On awake, assign components and add a listener to parent button.
    private void Awake() 
    {
        _button = gameObject.GetComponent<Button>();
        GameObject experiment = GameObject.Find("experimentObjects");
        _stimuliSetup = experiment.GetComponent<StimuliSetup>();
        _instructionsDisplay = experiment.GetComponent<InstructionsDisplay>();
        _trackerControlHub = experiment.GetComponent<TrackerControlHub>();
        keyboard = Keyboard.current;
        // Create an input action that is triggered when the numpad enter key is released.
    }

    public void EnableInputs()
    {
        takingInputs = true;
    }
    // When button is clicked, check if find trackers is clicked. If so, set up the next block's virtual objects. If not, don't do anything.
    void BeginBlock()
    {
        _trackerControlHub.EnableTrackers();
        takingInputs = false;
        if(_trackerControlHub.trackerFindClicked)
        {
            _stimuliSetup.BeginFirstTrial();
            _stimuliSetup.ArrangeStimuli();
            _instructionsDisplay.DisplayCanvas();
        }

    }

    void ListenForInputs()
    {
        if (keyboard.numpadEnterKey.wasReleasedThisFrame && takingInputs)
        {
            UXF.Utilities.UXFDebugLog("Numpad enter key was pressed.");
            BeginBlock();
        }
    }

    private void Update()
    {
        ListenForInputs();
    }
}
