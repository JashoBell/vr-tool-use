using UnityEngine;
using System.Collections;
using TMPro;
using Valve.VR;

public class SteamVRInitText : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public bool OpenVRInitialized = false;

    public IEnumerator UpdateText()
    {
        while(!OpenVRInitialized)
        {
            if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess)
            {
                statusText.text = "SteamVR Status: Initialized.";
                statusText.color = Color.green;
                OpenVRInitialized = true;
            }
            else if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeFailure)
            {
                statusText.text = "SteamVR Status: Failed to initialize.";
                statusText.color = Color.red;
            }
            else if (SteamVR.initializedState == SteamVR.InitializedStates.Initializing)
            {
                statusText.text = "SteamVR Status: Initializing.";
                statusText.color = Color.yellow;
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public void Start()
    {
        StartCoroutine(UpdateText());
    }
}