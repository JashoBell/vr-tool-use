using UnityEngine;
using System.Collections;


public class UniverseCalibrationValidation : MonoBehaviour
{
    public GameObject calibrationTracker, originTracker;
    public float alignmentDuration = 5f;
    public bool universeValidated;


    public float timer;
    public bool triggered;
    public float angle;

    private void Start()
    {
        triggered = false;
        timer = 0f;
        universeValidated = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.name == "calibration_object")
        {
            triggered = true;
            print("triggered");
            StartCoroutine(AlignmentRoutine());
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.name == "calibration_object")
        {
            timer = 0f;
        }
    }

    /// <summary>
    /// Evaluates whether the calibration_object and the originTracker are aligned along the world Z axis, returning false if
    /// the angle is greater than 5 degrees.
    /// </summary>
    
    private bool IsAligned()
    {
        angle = calibrationTracker.transform.position.x - originTracker.transform.position.x;
        return angle < .01f;
    }

    private IEnumerator AlignmentRoutine()
    {
        while (triggered && timer < alignmentDuration)
        {
            timer += Time.deltaTime;
            if (!IsAligned())
            {
                timer = 0f;
            }
            yield return null;
        }
        universeValidated = true;
        triggered = false;
        timer = 0f;
        this.gameObject.GetComponent<Collider>().enabled = false;
        this.gameObject.SetActive(false);
    }
}
