using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public class MenuButtons : MonoBehaviour
{
    public bool isCalibrateSteamVR;
    public bool isCalibrateParticipant;
    public bool isBeginExperiment;
    public bool isEndApplication;
    private Button _button;
    public InputSystemUIInputModule input;
    public AvatarSelection avatarSelection;
    public TextMeshProUGUI warningText;

    // Start is called before the first frame update
    void Start(){
        _button = this.GetComponent<Button>();
        _button.onClick.AddListener(MenuClick);
    }

    /// <summary>
    /// Handles clicks on the menu, loading the appropriate scene.
    /// </summary>
    void MenuClick()
    {
        if (isCalibrateParticipant)
        {
            SceneManager.LoadScene("participantCalibration", LoadSceneMode.Single);
        } 
        else if(isCalibrateSteamVR)
        {
            SceneManager.LoadScene("CalibrationXR", LoadSceneMode.Single);
        } 
        else if (isBeginExperiment)
        {
            SceneManager.LoadScene("AvatarLab", LoadSceneMode.Single);
        }
    }
    
}
