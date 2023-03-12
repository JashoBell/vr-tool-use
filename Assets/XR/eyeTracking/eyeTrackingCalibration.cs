using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ViveSR.anipal.Eye;
using ViveSR.anipal;
using ViveSR;
using UnityEngine.InputSystem;

public class EyeTrackingCalibration : MonoBehaviour
{
    private bool _calibrationRun;
    private bool _calibrationSuccess;
    // Start is called before the first frame update
    void Start()
    {
        _calibrationRun = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(!_calibrationRun & Keyboard.current.spaceKey.isPressed)
        {
            _calibrationRun = true;
            _calibrationSuccess = SRanipal_Eye_v2.LaunchEyeCalibration();
        } else if(_calibrationRun & !_calibrationSuccess & Keyboard.current.spaceKey.isPressed)
        {
            _calibrationSuccess = SRanipal_Eye_v2.LaunchEyeCalibration();
        }
    }
}
