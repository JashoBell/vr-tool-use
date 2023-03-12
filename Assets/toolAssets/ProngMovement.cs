using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  Script measures the distance between left and right prong (as tracked by PPT), and applies a rotation value to the prong it is attached to, 
/// which is the ratio of the open and current distances (minus the closed distance). Animates the tool opening and closing.
/// </summary>
public class ProngMovement : MonoBehaviour
{
    public Transform leftProng;
    public Transform rightProng;
    public enum prongSide {
        left,
        right
    }

    private float _distanceStart;
    private float _distanceClosed;
    private float _rotationStart;
    private float _rotationClosed;
    public prongSide side;
    

    // Start is called before the first frame update
    void Start()
    {
        //Tracker distances when opened and closed.
        _distanceStart = .153037f;
        _distanceClosed = .0623704f;
        print(_distanceStart.ToString());
        _rotationStart = this.transform.localRotation.eulerAngles.y;
        _rotationClosed = this.transform.localRotation.eulerAngles.y - 40f;
        print(_rotationStart.ToString());
    }
    // Update is called once per frame
    void Update()
    {
        float distanceNow = Vector3.Distance(leftProng.position, rightProng.position);
        //print(distanceNow.ToString());
        
        float apertureProportion = (distanceNow-_distanceClosed)/(_distanceStart-_distanceClosed);
        //print(apertureProportion.ToString());
        var rot = gameObject.transform.localEulerAngles;
        if(side == prongSide.right)
        {
        rot = new Vector3(rot.x, 
                Mathf.Lerp(_rotationClosed,
                           _rotationStart,
                           apertureProportion), 
                rot.z);
        } else if(side == prongSide.left)
        {
        rot = new Vector3(rot.x, 
                -Mathf.Lerp(_rotationClosed,
                           _rotationStart,
                           apertureProportion), 
                rot.z);    
        }
        gameObject.transform.localEulerAngles = rot;
        
    }
}
