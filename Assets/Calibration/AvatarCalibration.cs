using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UXF;
using static BoneUtilities;
using RootMotion.FinalIK;
using Manus.Skeletons;
using MovementTracking;
using taskAssets.Instructions;
using UnityEngine.Animations;
using UnityEngine.Serialization;
using Valve.VR;

public class AvatarCalibration : MonoBehaviour
{
    
    private Transform _avatarRoot, _avatarUpperArmL, _avatarUpperArmR, _avatarHead, _avatarHandL, _avatarHandR, _avatarForearmR, _avatarForearmL,
        _participantHeadTarget, _participantHeadTracker, _participantHandRTarget, _participantHandLTarget, _participantFingerTarget, _participantFootLTarget, _participantFootRTarget;
    private VRIK _ik;
    [SerializeField] private InstructionAudio _instructionAudio;
    [SerializeField] private AudioClip inTPose, outOfTPose;
    [SerializeField] private Material _headStandinMaterial;
    private readonly float _threshold = .15f;
    private readonly float _angleThreshold = 45f;
    public bool addAntiScaleBone = true;
    public float distanceOffset = 0;
    public float heightOffset = 0;
    public bool leftHigher = false;
    public float angleOffset = 0;
    public enum CalibrationStatus {
        Uninitiated,
        Uncalibrated,
        HeightCalibrated,
        Calibrated,
        Confirmed
    }
    public CalibrationStatus calibrationStatus = CalibrationStatus.Uninitiated;
    
    // Rocketbox avatar height and arm length measurements.
    // Height from eye, arm length from shoulder to hand bone.
    private const float FemaleAvatarBaseHeight = 1.61262f;
    private const float FemaleAvatarBaseArmLength = .492835f;
    private const float FemaleAvatarUpperArmLength = .2527f;
    private const float FemaleAvatarBaseWingSpan = 1.332f;
    private const float MaleAvatarBaseHeight = 1.68895f;
    private const float MaleAvatarBaseArmLength = .556721f;
    private const float MaleAvatarUpperArmLength = .2858f;
    private const float MaleAvatarBaseWingSpan = 1.546f;
    private const float FemaleAvatarForearmLength = .2402f;
    private const float MaleAvatarForearmLength = .2709f;
    private const float FootHeight = 0.002f;
    
    private float _participantHeight, _participantArmLength, _participantHandLength, _participantHandWidth;

    /// <summary>
    /// Uses the participant's height to scale the avatar's height.
    /// This is done by comparing the offset between the head target and the root position, and the base height of the avatar.
    /// </summary>
    public void CalibrateAvatarHeight()
    {
        bool female = PlayerPrefs.GetString("AvatarSex") == "Female";
        var avatarHeight = female
            ? FemaleAvatarBaseHeight
            : MaleAvatarBaseHeight;
        
        // Compare the height of the head target to the height of the eye bone, multiply scale by that value.
        var rootPosition = _ik.references.root.position;
        var heightRatio = (_participantHeadTracker.position.y - rootPosition.y) / avatarHeight;
        var avatarScale = Vector3.one * heightRatio;
        _avatarRoot.localScale = avatarScale;

        // Change the position of the foot targets to put them at the same global height as the avatar's foot
        // bones when the avatar is standing straight on the floor.

        _participantFootLTarget.position = new Vector3(_participantFootLTarget.position.x, FootHeight, _participantFootLTarget.position.z);
        _participantFootRTarget.position = new Vector3(_participantFootRTarget.position.x, FootHeight, _participantFootRTarget.position.z);

        CreateFakeHead(avatarScale.y);

        calibrationStatus = CalibrationStatus.HeightCalibrated;
        _instructionAudio.PlayAudio(inTPose);
        PlayerPrefs.SetFloat("avatarScale", avatarScale.y);
        PlayerPrefs.SetFloat("ParticipantHeight", avatarHeight * avatarScale.y);
        Debug.Log("Height: " + (avatarHeight * avatarScale.y));
    }

    /// <summary>
    /// Use the participant's arm length to scale the avatar's arm length.
    /// This is done by comparing the distance between (FinalIK's estimated position of) the upper arm bone 
    /// and the hand target, which is slightly offset from the actual tracker.
    /// </summary>
    public void CalibrateAvatarArms()
    {
        var avatarArmLength = PlayerPrefs.GetString("AvatarSex") == "Female"
            ? FemaleAvatarBaseArmLength
            : MaleAvatarBaseArmLength;
        var participantArmLength = Vector3.Distance(_ik.solver.rightArm.upperArm.solverPosition, _participantHandRTarget.position);
        print("upperarm current scale: " + _avatarUpperArmR.lossyScale.x);
        var armLengthRatio = participantArmLength / avatarArmLength;
        _avatarUpperArmR.localScale = Vector3.one * armLengthRatio;
        _avatarUpperArmL.localScale = Vector3.one * armLengthRatio;

        print(armLengthRatio.ToString(CultureInfo.CurrentCulture));
        
        if(addAntiScaleBone)
        {
            // Sometimes the scale bones need to be toggled off and on again to work properly.
            var antiScaleBoneLeft = _avatarHandL.GetComponent<Skeleton>();
            var antiScaleBoneRight = _avatarHandR.GetComponent<Skeleton>();
            antiScaleBoneLeft.skeletonData.settings.scaleToTarget = false;
            antiScaleBoneRight.skeletonData.settings.scaleToTarget = false;            
            antiScaleBoneLeft.skeletonData.settings.scaleToTarget = true;
            antiScaleBoneRight.skeletonData.settings.scaleToTarget = true;
        }
        

        
        PlayerPrefs.SetFloat("ArmScale", _avatarUpperArmR.localScale.x);
        PlayerPrefs.SetFloat("ArmLengthFromCalibration", avatarArmLength * armLengthRatio);
        PlayerPrefs.SetFloat("ParticipantArmLength", participantArmLength);
        calibrationStatus = CalibrationStatus.Calibrated;
    }

    /// <summary>
    /// Checks whether the participant is currently standing in t-pose. Returns true if they are.
    /// To evaluate whether the participant is in t-pose, we check the angle between the vectors
    /// from the head to the left hand, and from the head to the right hand. If the angle is within
    /// a certain threshold, we assume the participant is in t-pose.
    /// </summary>
    /// <returns>Whether the participant is in t-pose.</returns>
    /// <param name="ik">The VRIK component of the avatar.</param>
    private bool IsInTPose(VRIK ik)
    {
        var leftHand =_participantHandLTarget;
        var rightHand =_participantHandRTarget;
        var head = _participantHeadTarget;
        var participantHeight = PlayerPrefs.GetFloat("ParticipantHeight");

        // check that the -z and +z directions of the left and right hand targets' local space are pointing in the same direction
        var leftHandForward = leftHand.TransformDirection(Vector3.back);
        var rightHandForward = rightHand.TransformDirection(Vector3.forward);
        var angle = Vector3.Angle(leftHandForward, rightHandForward);
        bool handsAtSameAngle = angle < _angleThreshold;
        angleOffset = angle;

        // check that the hands are at least 2/3 of the participant's height above the floor
        var leftHandPosition = leftHand.position;
        var rightHandPosition = rightHand.position;
        bool handsAboveChest = leftHandPosition.y > participantHeight * 2 / 3 && rightHandPosition.y > participantHeight * 2 / 3;

        

        // check if the left and right hands are at the same height
        heightOffset = Mathf.Abs(leftHandPosition.y - rightHandPosition.y);
        bool handsAtSameHeight = heightOffset < _threshold;
        leftHigher = leftHandPosition.y > rightHandPosition.y;
        

        // check if the left and right hands are at the same distance from the head
        var headPosition = head.position;
        bool handsAtSameDistance = Mathf.Abs(Vector3.Distance(leftHandPosition, headPosition) - Vector3.Distance(rightHandPosition, headPosition)) < _threshold;
        distanceOffset = Mathf.Abs(Vector3.Distance(leftHandPosition, headPosition) - Vector3.Distance(rightHandPosition, headPosition));

        return handsAboveChest 
                && handsAtSameAngle 
                && handsAtSameHeight 
                && handsAtSameDistance;
    }

    /// <summary>
    /// Calibrates the arm length of the avatar by comparing the distance between the hand targets when the participant
    /// is in t-pose with what would be the distance between the hand bones of the avatar when the avatar is in t-pose.
    /// For female avatars, the hand-bone-to-hand-bone distance is 1.332m. For male avatars, it is 1.546m.
    /// First, we check whether the participant is in t-pose. If they are, we calculate the distance between
    /// the hand targets, taking several measurements across a few seconds. Then, we calculate the average
    /// and compare it to the avatar distance, scaling the avatar's arm length accordingly.
    /// </summary>
    public IEnumerator CalibrateArmLength()
    {
        bool calibrated = false;
        var measurements = new List<float>();
        var avatarWingspan = PlayerPrefs.GetString("AvatarSex") == "Female" ? FemaleAvatarBaseWingSpan : MaleAvatarBaseWingSpan;
        var avatarUpperArmSegment = PlayerPrefs.GetString("AvatarSex") == "Female" ? FemaleAvatarUpperArmLength : MaleAvatarUpperArmLength;
        var avatarForearmSegment = PlayerPrefs.GetString("AvatarSex") == "Female" ? FemaleAvatarForearmLength : MaleAvatarForearmLength;
        var participantElbowTarget = GameObject.Find("right_elbow_target").transform;
        var participantUpperArmTarget = GameObject.Find("right_upperarm_target").transform;
        print("Avatar wingspan: " + avatarWingspan);
        var startTime = Time.time;
        var notificationSoundTime = Time.time;
        var antiScaleBoneLeft = _avatarHandL.GetComponent<Skeleton>();
        var antiScaleBoneRight = _avatarHandR.GetComponent<Skeleton>();

        if(addAntiScaleBone)
            {
                antiScaleBoneLeft.skeletonData.settings.scaleToTarget = false;
                antiScaleBoneRight.skeletonData.settings.scaleToTarget = false;
            }

        while (!calibrated)
        {
            if (IsInTPose(_ik))
            {
                var handDistance = Vector3.Distance(_participantHandLTarget.position, _participantHandRTarget.position);
                measurements.Add(handDistance);
                if (Time.time - notificationSoundTime > 1)
                {
                    notificationSoundTime = Time.time;
                    _instructionAudio.PlayAudio(inTPose);
                }
            }
            else
            {
                if(Time.time - startTime > .5) 
                {
                    Debug.Log("T-pose dropped.");
                    _instructionAudio.PlayAudio(outOfTPose);
                }
                measurements.Clear();
                startTime = Time.time;
            }
            if (Time.time - startTime > 3)
            {
                // Calculate the average of the 50 longest measurements
                measurements.Sort();
                measurements.Reverse();
                var average = measurements.Take(50).Average();
                Debug.Log("[AvatarCalibration] Measured hand distance: " + average);
                
                // Scale the avatar's arm length, correcting for the avatar's current scale
                var avatarRootLocalScale = _avatarRoot.localScale;
                var participantUpperArmLength = Vector3.Distance(participantUpperArmTarget.position, participantElbowTarget.position);
                var armLengthRatio = participantUpperArmLength / avatarUpperArmSegment;
                _avatarUpperArmR.localScale = Vector3.one * armLengthRatio / avatarRootLocalScale.x;
                _avatarUpperArmL.localScale = Vector3.one * armLengthRatio / avatarRootLocalScale.x;
                var participantForearmLength = Vector3.Distance(_participantHandRTarget.position, participantElbowTarget.position);
                var forearmLengthRatio = participantForearmLength / avatarForearmSegment;
                _avatarForearmR.localScale = Vector3.one * forearmLengthRatio / _avatarUpperArmR.localScale.x;
                _avatarForearmL.localScale = Vector3.one * forearmLengthRatio / _avatarUpperArmR.localScale.x;

                // Save the arm length scale and the arm length from calibration
                PlayerPrefs.SetFloat("ArmScale", _avatarUpperArmR.localScale.x);
                PlayerPrefs.SetFloat("ForearmScale", _avatarForearmR.localScale.x);

                PlayerPrefs.SetFloat("ArmLengthFromCalibration", avatarWingspan * armLengthRatio);
                PlayerPrefs.SetFloat("ForearmLengthFromCalibration", avatarForearmSegment * forearmLengthRatio);

                PlayerPrefs.SetFloat("ParticipantWristToWrist", average);
                PlayerPrefs.SetFloat("ParticipantForearmLength", participantForearmLength);

                if(addAntiScaleBone)
                {
                    antiScaleBoneLeft.skeletonData.settings.scaleToTarget = true;
                    antiScaleBoneRight.skeletonData.settings.scaleToTarget = true;
                }
                
                Debug.Log("[AvatarCalibration] Average hand distance: " + average);
                Debug.Log("[AvatarCalibration] Arm length ratio: " + armLengthRatio);
                Debug.Log("[AvatarCalibration] Forearm length ratio: " + forearmLengthRatio);
                Debug.Log("[AvatarCalibration] Distance between bones: " + 
                    Vector3.Distance(_ik.solver.leftArm.hand.solverPosition, _ik.solver.rightArm.hand.solverPosition)
                    );


                calibrated = true;
                calibrationStatus = CalibrationStatus.Calibrated;
            }
            yield return null;
        }
    }

    /// <summary>
    /// Generates a "head" at the avatar's head bone, 10cm (times the avatar's scale) -x (in the head bone space)
    /// and adds a parent constraint from the Unity Animation package to the sphere to keep it
    /// aligned with the head bone.
    /// This is used because the rocketbox avatars have a single mesh, and the head cannot selectively
    /// be made transparent to the participant, so it must be set to scale 0. 
    /// The sphere casts a shadow in place of the avatar's head.
    /// </summary>
    /// <Param name="head">The head bone of the avatar.</Param>
    /// <Param name="scale">The scale of the avatar.</Param>
    private void CreateFakeHead(float scale)
    {
        var fakeHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fakeHead.transform.localScale = new Vector3(.20f, .20f, .13f) * scale;
        var fakeHeadRenderer = fakeHead.GetComponent<Renderer>() ?? throw new ArgumentNullException("fakeHead.GetComponent<Renderer>()");
        fakeHeadRenderer.material = _headStandinMaterial;
        //Add the standin renderer to light layers 0, 1, 2 and 3
        fakeHeadRenderer.renderingLayerMask = 1 << 0 | 1 << 1 | 1 << 2 | 1 << 3;
        fakeHeadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        fakeHead.GetComponent<Collider>().enabled = false;
        fakeHead.name = "HeadStandIn";
        fakeHead.layer = 0;
        fakeHead.transform.parent = _participantHeadTarget;
        
        fakeHead.transform.localPosition = new Vector3(-.07f*scale, 0, 0);

    }

    public void DestroyFakeHead()
    {
        var fakeHead = _participantHeadTarget.Find("HeadStandIn");
        if (fakeHead != null)
        {
            Destroy(fakeHead.gameObject);
        }
    }

    /// <summary>
    /// Reset the avatar's scale to 1.
    /// </summary>
    public void ResetCalibration()
    {

        _avatarRoot.localScale = Vector3.one;
        _avatarUpperArmR.localScale = Vector3.one;
        _avatarUpperArmL.localScale = Vector3.one;

        if (addAntiScaleBone)
        {
            var antiScaleBoneLeft = _avatarHandL.GetComponent<Skeleton>();
            var antiScaleBoneRight = _avatarHandR.GetComponent<Skeleton>();
            antiScaleBoneLeft.skeletonData.settings.scaleToTarget = false;
            antiScaleBoneRight.skeletonData.settings.scaleToTarget = false;
        }

        calibrationStatus = CalibrationStatus.Uncalibrated;
    }


    /// <summary>
    /// Set up the calibration process by finding the relevant transforms.
    /// </summary>
    /// <param name="avatar">The root transform of the avatar</param>
    public void CalibrationSetup(Transform avatar)
    {
        _avatarRoot = avatar;
        FindAvatarBones();
        FindTargets();

        calibrationStatus = CalibrationStatus.Uncalibrated;
    }
    
    /// <summary>
    /// Find the relevant bones in the avatar's hierarchy and assign them to local variables.
    /// </summary>
    private void FindAvatarBones()
    {
        _avatarHead = SearchHierarchyForBone(_avatarRoot, "Bip01 Head");
        _avatarUpperArmR = SearchHierarchyForBone(_avatarRoot, "Bip01 R UpperArm");
        _avatarUpperArmL = SearchHierarchyForBone(_avatarRoot, "Bip01 L UpperArm");
        _avatarForearmR = SearchHierarchyForBone(_avatarRoot, "Bip01 R Forearm");
        _avatarForearmL = SearchHierarchyForBone(_avatarRoot, "Bip01 L Forearm");
        _avatarHandR = SearchHierarchyForBone(_avatarRoot, "Bip01 R Hand");
        _avatarHandL = SearchHierarchyForBone(_avatarRoot, "Bip01 L Hand");
        _ik = _avatarRoot.GetComponent<VRIK>();
    }

    /// <summary>
    /// Find the tracker targets in the avatar's hierarchy and assign them to local variables.
    /// </summary>
    private void FindTargets()
    {
        var solver = _ik.solver;

        _participantHeadTarget = solver.spine.headTarget;
        _participantHeadTracker = _participantHeadTarget.parent;
        _participantHandRTarget = solver.rightArm.target;
        _participantHandLTarget = solver.leftArm.target;
        _participantFootLTarget = solver.leftLeg.target;
        _participantFootRTarget = solver.rightLeg.target;
        var fingerTracker = _participantHandRTarget.parent.GetComponentInChildren<VRPNTrackedObject>();
        if(fingerTracker != null)
            _participantFingerTarget = fingerTracker.transform;
    }

    /// <summary>
    /// Takes the saved scale values and applies them to the avatar.
    /// </summary>
    public void ApplyCalibrationSettings()
    {
        var avatarScale = PlayerPrefs.GetFloat("AvatarHeightRatio");
        var armScale = PlayerPrefs.GetFloat("ArmScale");
        var forearmScale = PlayerPrefs.GetFloat("ForearmScale");
        if(avatarScale == 0)
            avatarScale = 1;
        if(armScale == 0)
            armScale = 1;
        _avatarRoot.localScale = Vector3.one * avatarScale;
        _avatarUpperArmR.localScale = Vector3.one * armScale/avatarScale;
        _avatarUpperArmL.localScale = Vector3.one * armScale/avatarScale;
        _avatarForearmR.localScale = Vector3.one * forearmScale/_avatarUpperArmR.localScale.x;
        _avatarForearmL.localScale = Vector3.one * forearmScale/_avatarUpperArmL.localScale.x;
        if (addAntiScaleBone)
        {
            var antiScaleBoneLeft = _avatarHandL.GetComponent<Skeleton>();
            var antiScaleBoneRight = _avatarHandR.GetComponent<Skeleton>();
            antiScaleBoneLeft.skeletonData.settings.scaleToTarget = false;
            antiScaleBoneRight.skeletonData.settings.scaleToTarget = false;
        }
    }


}
