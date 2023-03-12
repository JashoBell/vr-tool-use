using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UXF.UI;

public class UXFUIFill : MonoBehaviour
{

    public string ppidString, sessionString, participantNumberString, blockString, trialString;

    private InputField _ppid, _session, _participantNumber, _block, _trial;
    public Canvas UXFCanvas;
    public Camera participantCamera;
    public UIController uiController;
    public Toggle readyToBeginToggle;
    public Button beginSessionButton;
    public bool readyToBegin = false;
    // Start is called before the first frame update
    void Start()
    {
        ppidString = "d3-0";
        sessionString = "1";
        participantNumberString = "";
        blockString = "1";
        trialString = "1";
    }

    void UpdateInputFields()
    {
        _ppid = GameObject.Find("PPIDElement").GetComponent<TextElementSetup>().content;
        _session = GameObject.Find("SessionNumDropdown").GetComponent<TextElementSetup>().content;
        _participantNumber = GameObject.Find("id").GetComponent<TextElementSetup>().content;
        _block = GameObject.Find("startblock").GetComponent<TextElementSetup>().content;
        _trial = GameObject.Find("starttrial").GetComponent<TextElementSetup>().content;

        _ppid.text = ppidString;
        _session.text = sessionString;
        _participantNumber.text = participantNumberString;
        _block.text = blockString;
        _trial.text = trialString;
    }

    private IEnumerator BeginExperiment()
    {
        readyToBegin = false;
        UpdateInputFields();
        yield return new WaitForSeconds(0.1f);
        readyToBeginToggle.isOn = true;
        participantCamera = GameObject.FindGameObjectWithTag("participantCamera").GetComponent<Camera>();
        UXFCanvas.worldCamera = participantCamera;
        beginSessionButton.onClick.Invoke();
        yield return new WaitUntil(() => uiController.gameObject.activeSelf == false);
        this.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(readyToBegin)
        {
            StartCoroutine(BeginExperiment());
        }
    }
}
