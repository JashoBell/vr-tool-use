using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Session = UXF.Session;

public class OscillateStimulus : MonoBehaviour
{
    public Transform wristTransform, elbowTransform;
    public bool isAligned = false, landmarkMode = false, sameLength = false;
    public GameObject tableTop, corkMat;

    //Audio source and clips
    public AudioSource audioSource;
    public AudioClip countdown, begin, select, final, wrist, elbow, forearm;
    public Material tableMaterial, landmarkTableMaterial;
    public Vector3 previousPosition;
    public string direction = "";
    
    public float speed, noiseOne, noiseTwo, forearmLengthPre = 0, forearmLengthDifference;

    public Session session;


    /// <summary>
    /// Moves the stimuli between the two trackers (+ noise), but moves sphere to be level with the wrist (-2cm to adjust for the tracker's height) on the y axis
    /// </summary>
    private void Oscillate() 
    {
        previousPosition = transform.position;
        var wristPosition = wristTransform.position;
        var elbowPosition = elbowTransform.position;
        
        var time = Mathf.PingPong(noiseOne + (Time.time * speed), 1);
        
        transform.position = Vector3.Lerp(
            new Vector3(wristPosition.x,
                        wristPosition.y,
                        wristPosition.z + noiseOne), 
            new Vector3(elbowPosition.x,
                        wristPosition.y,
                        elbowPosition.z - noiseTwo), 
            time);

        if(previousPosition.z < transform.position.z)
        {
            direction = "forward";
        } else if(previousPosition.z > transform.position.z)
        {
            direction = "backward";
        } else {
            direction = "";
        }
    }

    /// <summary>
    /// Changes the table to "landmark mode", turning it black and making it much wider.
    /// </summary>
    public void ToggleLandmarkTable()
    {
        var tableDimensions = tableTop.transform.localScale;
        if(!landmarkMode)
        {
            tableTop.GetComponent<Renderer>().material = landmarkTableMaterial;
            tableTop.transform.localScale = new Vector3(1.3f, tableDimensions.y, tableDimensions.z);
            corkMat.SetActive(false);
            landmarkMode = true;
        }
        else
        {
            tableTop.GetComponent<Renderer>().material = tableMaterial;
            tableTop.transform.localScale = new Vector3(.750f, tableDimensions.y, tableDimensions.z);
            corkMat.SetActive(true);
            landmarkMode = false;
        }
    }

    /// <summary>
    /// Randomizes the noise and speed of the stimulus.
    /// </summary>
    public void RandomizeParameters()
    {
        noiseOne = UnityEngine.Random.Range(0.13f, 0.24f);
        noiseTwo = UnityEngine.Random.Range(0.13f, 0.24f);
        speed = UnityEngine.Random.Range(0.25f, 0.30f);
    }

    /// <summary>
    /// Toggles the stimulus's mesh renderer.
    /// </summary>
    public void ToggleVisibility()
    {
        var rend = GetComponent<Renderer>();
        rend.enabled = !rend.enabled;
    }

    public void PlaySound(string clipName)
    {
        switch (clipName)
        {
            case "countdown":
                audioSource.PlayOneShot(countdown);
                break;
            case "begin":
                audioSource.PlayOneShot(begin);
                break;
            case "select":
                audioSource.PlayOneShot(select);
                break;
            case "final":
                audioSource.PlayOneShot(final);
                break;
            case "wrist":
                audioSource.PlayOneShot(wrist);
                break;
            case "elbow":
                audioSource.PlayOneShot(elbow);
                break;
            case "forearm":
                audioSource.PlayOneShot(forearm);
                break;
        }
    }

    private void Start()
    {
        FindWristAndElbow();
    }

    private void FindWristAndElbow()
    {
        foreach (GameObject target in GameObject.FindGameObjectsWithTag("participantTargets"))
        {
            if (target.name == "right_hand_target")
            {
                wristTransform = target.transform;
            }
            else if (target.name == "right_elbow_target")
            {
                elbowTransform = target.transform;
            }
        }
    }

    private void Update() {
        //If before experiment, or in the last trial of a block during the experiment (i.e., when setting up before beginning the next block),
        //determine whether landmark trackers are aligned along the X axis, because this is the path of oscillation.
        if(session.currentBlockNum == 0 || session.CurrentTrial == session.CurrentBlock.lastTrial)
        {
            if(session.currentBlockNum < session.blocks.Count)
            {
                if(session.blocks[session.currentBlockNum].settings.GetString("task") == "landmark")
                {
                    isAligned = Mathf.Abs(wristTransform.position.x - elbowTransform.position.x) < .02;
                    
                }
            }
            else
            {
                if(session.blocks[session.currentBlockNum-1].settings.GetString("task") == "landmark")
                {
                    isAligned = Mathf.Abs(wristTransform.position.x - elbowTransform.position.x) < .02;
                }
            }
            Oscillate();
        } 
        else
        {
            if (session.CurrentBlock.settings.GetString("task") != "landmark") return;
            isAligned = Mathf.Abs(wristTransform.position.x - elbowTransform.position.x) < .02;
            Oscillate();
        }
    }

}