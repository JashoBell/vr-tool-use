using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UXF;

public class StimuliTriggers : MonoBehaviour
{
    public bool isReplaced;
    public bool isLifted;
    public Session session;

private void OnTriggerEnter(Collider other) {
    if (other.gameObject.name == "liftTarget" & !isLifted){
        isLifted = true;
        session.settings.SetValue("phase", "replace");
    } else if (other.gameObject.name == "stimuliStart" & !isReplaced){
        isReplaced = true;
        session.settings.SetValue("phase", "return");
    }
}

private void OnTriggerExit(Collider other) {
    if (other.gameObject.name == "liftTarget" & isLifted){
        isLifted = false;
    } else if (other.gameObject.name == "stimuliStart" & isReplaced){
        isReplaced = false;
    }
}
}
