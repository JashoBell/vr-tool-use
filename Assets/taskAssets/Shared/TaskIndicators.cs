using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class TaskIndicators : MonoBehaviour
{
    private Image _panel;
    private TextMeshProUGUI _text;
    public bool isEnabled;
    public enum TaskIndicatorEnum {LandmarkAlignment, LandmarkStart, LandmarkLength, LandmarkLengthText, ReachingStart, ReachingStimuli};
    public TaskIndicatorEnum taskIndicator;
    private GameObject _taskObject;
    private OscillateStimulus _oscillateStimulus;
    private LandmarkStartTrigger _landmarkStartTrigger;
    private StartTriggers _startTriggers;
    private StimuliTriggers _stimuliTriggers;

    // Start is called before the first frame update
    private void Start() {
        if(taskIndicator != TaskIndicatorEnum.LandmarkLengthText)
        {
            _panel = gameObject.GetComponent<Image>();
        }
        else
        {
            _text = gameObject.GetComponent<TextMeshProUGUI>();
        }

    }
    /// <summary>
    /// Activates task-dependent indicators that appear on the researcher-facing canvas. These display whether certain criteria for the task have been satisfied, 
    /// such as whether the trackers on the participant's arm are the same distance apart in the post-test as the pre-test.
    /// </summary>
    public void EnableIndicator()
    {
        switch (taskIndicator)
        {
            case TaskIndicatorEnum.LandmarkAlignment:
                _taskObject = GameObject.Find("landmarkStimulus");
                UXF.Utilities.UXFDebugLog("Landmark taskObject found");
                _oscillateStimulus = _taskObject.GetComponent<OscillateStimulus>();
                break;

            case TaskIndicatorEnum.LandmarkLength:
                _taskObject = GameObject.Find("landmarkStimulus");
                UXF.Utilities.UXFDebugLog("Landmark taskObject found");
                _oscillateStimulus = _taskObject.GetComponent<OscillateStimulus>();
                break;

            case TaskIndicatorEnum.LandmarkLengthText:
                _taskObject = GameObject.Find("landmarkStimulus");
                UXF.Utilities.UXFDebugLog("Landmark taskObject found");
                _oscillateStimulus = _taskObject.GetComponent<OscillateStimulus>();
                break;

            case TaskIndicatorEnum.LandmarkStart:
                _taskObject = GameObject.Find("landmarkStart");
                UXF.Utilities.UXFDebugLog("Landmark taskObject two found");
                _landmarkStartTrigger = _taskObject.GetComponent<LandmarkStartTrigger>();
                break;

            case TaskIndicatorEnum.ReachingStart:
                _taskObject = GameObject.Find("effectorStartPoint");
                UXF.Utilities.UXFDebugLog("Reaching taskObject found");
                _startTriggers = _taskObject.GetComponent<StartTriggers>();
                break;
                
            case TaskIndicatorEnum.ReachingStimuli:
                _taskObject = GameObject.Find("Stimulus_Rectangle");
                _stimuliTriggers = _taskObject.GetComponent<StimuliTriggers>();
                break;
        }
        isEnabled = true;
    }

    public void DisableIndicator()
    {
        isEnabled = false;
    }

    // Alternate color based on satisfaction of task criteria
    void Update()
    {
        if(isEnabled)
        {
            switch (taskIndicator)
            {
                case TaskIndicatorEnum.LandmarkAlignment:
                    if(_panel.isActiveAndEnabled)
                    {
                        if(_taskObject.GetComponent<OscillateStimulus>().isAligned)
                        {
                            _panel.color = Color.green;
                        } 
                        else
                        {
                            _panel.color = Color.red;
                        }
                    }
                    break;
                    

                case TaskIndicatorEnum.LandmarkLength:
                    if(_panel.isActiveAndEnabled)
                    {
                        if(_taskObject.GetComponent<OscillateStimulus>().sameLength)
                        {
                            _panel.color = Color.green;
                        } 
                        else
                        {
                            _panel.color = Color.yellow;
                        }
                    }
                    break;

                case TaskIndicatorEnum.LandmarkLengthText:
                    if(_text.isActiveAndEnabled)
                    {
                        _text.text = (_taskObject.GetComponent<OscillateStimulus>().forearmLengthDifference*100).ToString("F0");
                    }
                    break;

                case TaskIndicatorEnum.LandmarkStart:
                    if(_panel.isActiveAndEnabled)
                    {
                        if(_taskObject.GetComponent<LandmarkStartTrigger>().armIsPlaced)
                        {
                            _panel.color = Color.green;
                        } 
                        else
                        {
                            _panel.color = Color.yellow;
                        }
                    }
                    break;

                case TaskIndicatorEnum.ReachingStart:
                    if(_panel.isActiveAndEnabled)
                    {
                        if(_taskObject.GetComponent<StartTriggers>().isReturned)
                        {
                            _panel.color = Color.green;
                        } 
                        else
                        {
                            _panel.color = Color.yellow;
                        }
                    }
                    break;

                case TaskIndicatorEnum.ReachingStimuli:
                    if(_panel.isActiveAndEnabled)
                    {
                        if(_taskObject.GetComponent<StimuliTriggers>().isReplaced)
                        {
                            _panel.color = Color.green;
                        } 
                        else
                        {
                            _panel.color = Color.yellow;
                        }
                    }
                    break;
            }
        }
    }
}
