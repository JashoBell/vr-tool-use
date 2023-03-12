using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  Script measures the distance between left and right prong (as tracked by PPT), and applies a rotation value to the handle, 
/// which is the ratio of the open and current distances (minus the closed distance). Animates the handle's depression when squeezed.
/// </summary>
public class HandleMovement : MonoBehaviour
{
    public Transform leftProng;
    public Transform rightProng;
    private float _distanceStart;
    private float _distanceClosed;
    private float _rotationStart;
    private float _rotationClosed;
    // Start is called before the first frame update
    void Start()
    {
        
        //Distance closed, measured physically and calibrated
        _distanceStart = .153037f;
        _distanceClosed = .0623704f;
        print(_distanceStart.ToString());
        _rotationStart = this.transform.localRotation.eulerAngles.x;
        _rotationClosed = this.transform.localRotation.eulerAngles.x - 16f;
        print(_rotationStart.ToString());
    }

    // Update is called once per frame
    void Update()
    {
        float distanceNow = Vector3.Distance(leftProng.position, rightProng.position);
        
        float apertureProportion = (distanceNow-_distanceClosed)/(_distanceStart-_distanceClosed);

        var rot = gameObject.transform.localEulerAngles;
        rot.Set(Mathf.Lerp(_rotationClosed,
                           _rotationStart,
                           apertureProportion), 
                rot.y,
                rot.z);
        this.transform.localEulerAngles = rot;
        
    }
}
