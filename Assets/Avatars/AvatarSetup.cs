using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using RootMotion.FinalIK;
using static BoneUtilities;

public class AvatarSetup : MonoBehaviour
{

    //public RuntimeAnimatorController animatorController;
    private string _avatarSexPreference;
    private string _avatarNumberPreference;
    private const string AvatarPrefabName = "{avatarSex}_Adult_{avatarNumber}";

    public string FindAvatarPrefab(bool testing)
    {
        if(testing){
            _avatarSexPreference = "Female";
            _avatarNumberPreference = "01";
            Debug.Log("Testing mode. Avatar set to " + _avatarSexPreference + " " + _avatarNumberPreference);
        } else {
            _avatarSexPreference = PlayerPrefs.GetString("AvatarSex", "None");
            _avatarNumberPreference = PlayerPrefs.GetString("AvatarNumber", "None");
            Debug.Log("Avatar set to " + _avatarSexPreference + " " + _avatarNumberPreference);
        }
        
        if(_avatarSexPreference == "None" || _avatarNumberPreference == "None")
        {
            Debug.Log("No avatar selected. Please return to the menu and select an avatar.");
            return "None";
        }
        string chosenAvatarPrefab = AvatarPrefabName.Replace("{avatarSex}", _avatarSexPreference).Replace("{avatarNumber}", _avatarNumberPreference);

        PlayerPrefs.SetString("Chosen Avatar", chosenAvatarPrefab);
        return chosenAvatarPrefab;
    }

    public GameObject LoadAvatarPrefab(string avatarPrefabName)
    {
        if(avatarPrefabName == "None")
        {
            Debug.Log("No avatar selected. Please return to the menu and select an avatar.");
            return null;
        }
        
        var avatarFile = Resources.Load("Avatars/Adults/" + avatarPrefabName + "/Export/" + avatarPrefabName);
        var avatarObj = GameObject.Instantiate((GameObject)avatarFile);
        var headBone = SearchHierarchyForBone(avatarObj.transform, "Bip01 Head");
        // Hide the head from the participant so they don't see the inside of it.
        headBone.localScale = Vector3.zero;
       
        avatarObj.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Avatars/Rocketbox-Unity/Assets/Resources/Avatars/VRIK_Edit.controller");

        return avatarObj;
    }
    public void AttachAnchors(GameObject avatarObj, Transform chairAnchors)
    {
        var ikRig = avatarObj.GetComponent<VRIK>().solver;
        foreach (Transform target in chairAnchors)
        {
            if (target.name == "left_foot_target")
            {
                ikRig.leftLeg.target = target;
                ikRig.leftLeg.positionWeight = 1f;
                ikRig.leftLeg.rotationWeight = 1f;
            }
            else if (target.name == "right_foot_target")
            {
                ikRig.rightLeg.target = target;
                ikRig.rightLeg.positionWeight = 1f;
                ikRig.rightLeg.rotationWeight = 1f;
            }
            else if (target.name == "waist_target")
            {
                ikRig.spine.pelvisTarget = target;
                ikRig.spine.pelvisPositionWeight = 1f;
                ikRig.spine.pelvisRotationWeight = 0f;
                ikRig.spine.maintainPelvisPosition = 0.2f;
                ikRig.locomotion.weight = 0f;
            }
        }

    }


    public void AttachTargets(GameObject avatarObj, List<Transform> participantTargets)
    {
        var ikRig = avatarObj.GetComponent<VRIK>().solver;
        foreach (var t in participantTargets)
        {
            switch (t.name)
            {
                case "left_hand_target":
                    ikRig.leftArm.target = t;
                    ikRig.leftArm.positionWeight = 1;
                    ikRig.leftArm.rotationWeight = 1;
                    break;
                case "right_hand_target":
                    ikRig.rightArm.target = t;
                    ikRig.rightArm.positionWeight = 1;
                    ikRig.rightArm.rotationWeight = 1;
                    break;
                case "right_elbow_target":
                    ikRig.rightArm.bendGoal = t;
                    ikRig.rightArm.bendGoalWeight = .6f;
                    break;
                case "head_target":
                    ikRig.spine.headTarget = t;
                    break;
                case "left_foot_target":
                    ikRig.leftLeg.target = t;
                    ikRig.leftLeg.positionWeight = 1f;
                    ikRig.leftLeg.rotationWeight = 1f;
                    break;
                case "right_foot_target":
                    ikRig.rightLeg.target = t;
                    ikRig.rightLeg.positionWeight = 1f;
                    ikRig.rightLeg.rotationWeight = 1f;
                    break;
                case "waist_target":
                    ikRig.spine.pelvisTarget = t;
                    ikRig.spine.pelvisPositionWeight = .55f;
                    ikRig.spine.pelvisRotationWeight = 0f;
                    ikRig.spine.maintainPelvisPosition = 0f;
                   break;
                default:
                    break;
            }
        }
    }
}
