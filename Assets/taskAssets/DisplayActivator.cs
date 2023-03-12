using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UXF;
using Valve.VR;

/// <summary>
/// Allows the use of multiple displays during the experiment (HMD and monitor, for researcher-facing instructions.)
/// </summary>
public class DisplayActivator : MonoBehaviour
{
    public InputSystemUIInputModule input;
    public Canvas[] experimentUI;
    public Canvas[] participantUI;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(ActivateDisplays());
    }

    private void Awake() {
        input.enabled = false;
        input.enabled = true;
    }

    IEnumerator ActivateDisplays()
    {
        yield return new WaitForSeconds(1f);

        UXF.Utilities.UXFDebugLog("displays connected: " + Display.displays.Length);

        // Display.displays[0] is the primary, default display and is always ON, so start at index 1.
        // Check if additional displays are available and activate each.
    
        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }

        foreach(Canvas c in experimentUI)
        {
            c.targetDisplay = 1;
        }

        foreach(Canvas c in participantUI)
        {
            c.targetDisplay = 2;
        }
        

        yield return new WaitForSeconds(1f);

    
        
        yield break;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
