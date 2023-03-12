using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Valve.VR;

namespace Calibration
{
/// <summary>
/// This class is used to automatically find the pivot point of a limb segment, so that a self-avatar can attach
/// to this point.
/// Could probably be improved. For example, by having the points match the segment tracker's orientation and
/// penalizing/rewarding them for their relative rotation to the tracker.
/// </summary>    
    public class AutoPivotPointFinder : MonoBehaviour
    {
        private Transform _segmentTracker;
        public Transform target;
        private Vector3 originalTargetPosition;
        private int _numTransforms;
        private float _transformSpacing;
        private float _rotationalDisplacementCriterion;
        private string _intendedGlobalRotationAxis;
        private int _numIterations;
        private bool _includeRenderers;
        private GameObject[] _gameObjects;
        private Transform[] _transforms;
        public AudioClip beginCalibration;
        public AudioClip continueCalibration;
        public AudioClip endCalibration;
        public AudioClip rotate;
        public AudioSource _audioSource;
        public Transform pivotPoint;
        public bool pivotPointFound;
        private Vector3[] _initialPositions;
        private Vector3[] _initialRotations;
        private float[] _positionalDisplacement;
        private float[] _rotationalDisplacement;
        [SerializeField] private float _targetXDisplacement, _targetYDisplacement, _targetZDisplacement;
        private Quaternion _previousTargetOrientation;
        private Transform rotationDisplacementObject;
        private Vector3[] _previousPositions;
        private Vector3[] _previousRotations;
        public GameObject cubePrefab, indicatorPrefab;

        public SteamVR_Action_Boolean triggerWatcher, touchpadWatcher;
        public SteamVR_Input_Sources handType;

        private static AutoPivotPointFinder _instance;
        public static AutoPivotPointFinder Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AutoPivotPointFinder>();
                }
                return _instance;
            }
        }

        public void Start()
        {

        }

        public static void OnTouchpadDown(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
        {
        }

        /// <summary>
        /// Generates a point cloud around the approximated target, iteratively closing in on the point which
        /// moves the least during rotation of the segmentTracker, which should be attached to the limb segment
        /// downstream of the joint of interest.
        /// </summary>
        /// <param name="segmentTracker">The tracker to which the point cloud becomes parented</param>
        /// <param name="numTransforms">The number of transforms to generate. Cubed.</param>
        /// <param name="transformSpacing">The amount of initial spacing between the points</param>
        /// <param name="rotationalDisplacementCriterion">The threshold of rotation along each axis that should be reached before moving on</param>
        /// <param name="numIterations">The number of times to iteratively reduce distance and close in on estimated target</param>
        /// <param name="distanceReduction">The reduction in distance between each loop</param>
        /// <param name="includeRenderers">Whether to include a renderer for the points</param>
        /// <returns></returns>
        public IEnumerator IsolateJointPosition(Transform segmentTracker, int numTransforms, float transformSpacing,
            float rotationalDisplacementCriterion, string intendedGlobalRotationAxis, int numIterations, float distanceReduction, bool includeRenderers)
        {
            this._segmentTracker = segmentTracker;
            this._numTransforms = numTransforms * numTransforms * numTransforms;
            this._transformSpacing = transformSpacing;
            this._rotationalDisplacementCriterion = rotationalDisplacementCriterion;
            this._intendedGlobalRotationAxis = intendedGlobalRotationAxis;
            this._numIterations = numIterations;
            this._includeRenderers = includeRenderers;
    
            // Initialize transforms array and store initial positions and rotations
            _gameObjects = new GameObject[_numTransforms];
            _transforms = new Transform[_numTransforms];
            _initialPositions = new Vector3[_numTransforms];
            _initialRotations = new Vector3[_numTransforms];

            touchpadWatcher.AddOnStateDownListener(OnTouchpadDown, handType);
            yield return new WaitUntil(() => Keyboard.current.rightArrowKey.wasPressedThisFrame || touchpadWatcher.GetStateDown(handType));

            var center = target.position;
            originalTargetPosition = center;
            var centerObject = new GameObject
            {
                transform =
                {
                    name = "cubeCenter",
                    position = center,
                    parent = segmentTracker
                }
            };
            var resolutionIndicator = Instantiate(indicatorPrefab, centerObject.transform.position, Quaternion.identity, centerObject.transform);
            // Scale the resolution indicator to the size of the cube
            resolutionIndicator.transform.localScale = new Vector3(_transformSpacing*numTransforms, _transformSpacing*numTransforms, _transformSpacing*numTransforms);

            // Create the gameObjects and place them in a cube
            CreateCube(numTransforms, transformSpacing, includeRenderers, centerObject);


            print("Created " + _numTransforms + " transforms");
            _audioSource.PlayOneShot(beginCalibration);
            // Iterate to find the pivot
            Transform potentialPivot = null;

            rotationDisplacementObject = target.parent == segmentTracker ? target : centerObject.transform;

            for (int iteration = 0; iteration < numIterations; iteration++)
            {
                print("Recording: Iteration " + iteration);
                // Record positional and rotational data
                float timeElapsed = 0f;
                _positionalDisplacement = new float[_numTransforms];
                _rotationalDisplacement = new float[_numTransforms];
                _previousPositions = new Vector3[_numTransforms];
                _previousRotations = new Vector3[_numTransforms];
                _previousTargetOrientation = target.rotation;

                _targetXDisplacement = 0f;
                _targetYDisplacement = 0f;
                _targetZDisplacement = 0f;

                for (int i = 0; i < _numTransforms; i++)
                {
                    _previousPositions[i] = _transforms[i].position;
                    _previousRotations[i] = _transforms[i].eulerAngles;

                }

                while (!RotationDisplacementCriterionMet())
                {
                    RecordData();
                    for (int i = 0; i < _numTransforms; i++)
                    {
                        _previousPositions[i] = _transforms[i].position;
                        _previousRotations[i] = _transforms[i].eulerAngles;
                    }
                    _previousTargetOrientation = rotationDisplacementObject.rotation;


                    timeElapsed += Time.deltaTime;
                    yield return null;
                }

                // Find the transform with the least positional displacement
                int indexOfLowestPositionalDisplacement = 0;
                float lowestPositionalDisplacement = float.MaxValue;
                for (int i = 0; i < _numTransforms; i++)
                {
                    if (_positionalDisplacement[i] < lowestPositionalDisplacement)
                    {
                        indexOfLowestPositionalDisplacement = i;
                        lowestPositionalDisplacement = _positionalDisplacement[i];
                    }
                }

                potentialPivot = _transforms[indexOfLowestPositionalDisplacement].transform;
                print("[PivotPointFinder]: Potential pivot displacement from original target: " + (potentialPivot.position - target.position));

                if (iteration < numIterations - 1)
                {
                    _audioSource.PlayOneShot(continueCalibration);
                    // Move the center object to the potential pivot, and cut the distance between the transforms by half
                    print("Moving transforms towards potential pivot");
                    var potentialPivotPosition = potentialPivot.position;
                    centerObject.transform.position = potentialPivotPosition;

                    for (int i = 0; i < _numTransforms; i++)
                    {
                        Vector3 directionToPotentialPivot = potentialPivotPosition - _transforms[i].position;
                        directionToPotentialPivot = directionToPotentialPivot.normalized;
                        float distanceToPotentialPivot = Vector3.Distance(_transforms[i].position, potentialPivotPosition);
                        distanceToPotentialPivot /= distanceReduction;
                        _transforms[i].position = _transforms[i].position +
                                                 (directionToPotentialPivot * distanceToPotentialPivot);
                    }
                    // Update the scale of the resolution indicator
                    resolutionIndicator.transform.localScale /= distanceReduction;
                }
                else
                {
                    print("[PivotPointFinder]: Pivot point set");
                    target.position = potentialPivot.position;
                    _audioSource.PlayOneShot(endCalibration);
                    // Clean up the GameObjects
                    for (int i = 0; i < _numTransforms; i++)
                    {
                        UnityEngine.Object.Destroy(_transforms[i].gameObject);
                    }
                    print("[PivotPointFinder]: Cleaning up objects...");
                    Destroy(centerObject);
                    Destroy(resolutionIndicator);
                    pivotPointFound = true;
                    touchpadWatcher.RemoveOnStateDownListener(OnTouchpadDown, handType);
                }
            }
        }

        private void CreateCube(int numTransforms, float transformSpacing, bool includeRenderers, GameObject centerObject)
        {
            int n = 0;
            // Loop through all possible positions of the cube
            for (int x = 0; x < numTransforms; x++)
            {
                for (int y = 0; y < numTransforms; y++)
                {
                    for (int z = 0; z < numTransforms; z++)
                    {
                        // Calculate the position of the cube
                        Vector3 cubePosition = centerObject.transform.position +
                                               new Vector3(x * transformSpacing, y * transformSpacing, z * transformSpacing) -
                                               (Vector3.one * (transformSpacing * (numTransforms - 1)) / 2f);
                        GameObject cube = new();
                        // Decide whether to include a renderer or not
                        if (includeRenderers)
                        {
                            cube = Instantiate(cubePrefab, cubePosition, Quaternion.identity, centerObject.transform
                            );
                        }
                        else
                        {
                            cube.transform.name = "cube" + n;
                            cube.transform.position = cubePosition;
                            cube.transform.parent = centerObject.transform;
                        }

                        _transforms[n] = cube.transform;
                        _initialPositions[n] = cube.transform.position;
                        _initialRotations[n] = cube.transform.rotation.eulerAngles;
                        n++;
                    }
                }
            }
        }

        private IEnumerator IsolateWristJointOrientation(Transform segmentTracker, int numTransforms, float transformSpacing,
            float rotationalDisplacementCriterion, int numIterations, bool includeRenderers)
        {
            this._segmentTracker = segmentTracker;
            this._numTransforms = numTransforms * numTransforms * numTransforms;
            this._transformSpacing = transformSpacing;
            this._rotationalDisplacementCriterion = rotationalDisplacementCriterion;
            this._numIterations = numIterations;
            this._includeRenderers = includeRenderers;
            _audioSource = GetComponent<AudioSource>();

            // Initialize transforms array and store initial positions and rotations
            _gameObjects = new GameObject[_numTransforms];
            _transforms = new Transform[_numTransforms];
            _initialRotations = new Vector3[_numTransforms];

            yield return new WaitUntil(() => Keyboard.current.spaceKey.wasPressedThisFrame);
            string[] intendedAxis = {"x", "y", "z"};
            foreach(string axis in intendedAxis)
            {
                
                // Create a GameObject to hold the transforms
                var containerObject = new GameObject("Center")
                {
                    transform =
                    {
                        position = target.position,
                        rotation = target.rotation
                    }
                };

                // Create numTransforms transforms, with their rotations evenly spaced along the
                // front 180 degrees of the current axis, local to the containerObject.
                float angleBetweenTransforms = 180f / (numTransforms - 1);
                for (int n = 0; n < _numTransforms; n++)
                {
                    _gameObjects[n] = new GameObject("Transform " + n);
                    _transforms[n] = _gameObjects[n].transform;
                    _transforms[n].parent = containerObject.transform;
                    _transforms[n].localPosition = Vector3.zero;
                    _transforms[n].localRotation = Quaternion.identity;
                    float angle = angleBetweenTransforms * n;
                    if (angle > 90)
                    {
                        angle = angle - 180;
                    }
                    switch (axis)
                    {
                        case "x":
                            _transforms[n].RotateAround(containerObject.transform.position, containerObject.transform.right,
                                angle);
                            break;
                        case "y":
                            _transforms[n].RotateAround(containerObject.transform.position, containerObject.transform.up,
                                angle);
                            break;
                        case "z":
                            _transforms[n].RotateAround(containerObject.transform.position, containerObject.transform.forward,
                                angle);
                            break;
                    }

                }
                
                // The rotation value of the pivot point is the one that results in the most displacement along the
                // intended axis, and the least in the other two axes. So, record these values, with displacement
                // along the intended axis being positive, and displacement along the other two axes being negative.
                // The pivot point is the transform with the highest value.

                print("Created " + _numTransforms + " transforms");
                _audioSource.PlayOneShot(beginCalibration);
                // Iterate to find the pivot
                Transform potentialPivot = null;
                for (int iteration = 0; iteration < numIterations; iteration++)
                {
                    print("Recording: Iteration " + iteration);
                    // Record positional and rotational data
                    float timeElapsed = 0f;
                    _positionalDisplacement = new float[_numTransforms];
                    _rotationalDisplacement = new float[_numTransforms];
                    _previousPositions = new Vector3[_numTransforms];
                    _previousRotations = new Vector3[_numTransforms];

                    for (int i = 0; i < _numTransforms; i++)
                    {
                        _previousPositions[i] = _transforms[i].position;
                        _previousRotations[i] = _transforms[i].eulerAngles;
                    }

                    while (timeElapsed < rotationalDisplacementCriterion)
                    {
                        RecordData();
                        for (int i = 0; i < _numTransforms; i++)
                        {
                            _previousPositions[i] = _transforms[i].position;
                            _previousRotations[i] = _transforms[i].eulerAngles;
                        }

                        timeElapsed += Time.deltaTime;
                        yield return null;
                    }

                    // Find the transform with the least positional displacement
                    int indexOfLowestPositionalDisplacement = 0;
                    float lowestPositionalDisplacement = float.MaxValue;
                    for (int i = 0; i < _numTransforms; i++)
                    {
                        if (_positionalDisplacement[i] < lowestPositionalDisplacement)
                        {
                            indexOfLowestPositionalDisplacement = i;
                            lowestPositionalDisplacement = _positionalDisplacement[i];
                        }
                    }

                    _audioSource.PlayOneShot(rotate);
                    potentialPivot = _transforms[indexOfLowestPositionalDisplacement].transform;
                    print("Potential pivot position: " + (potentialPivot.position - target.position));

                    if (iteration < numIterations - 1)
                    {
                        // Move the center object to the potential pivot, and cut the distance between the transforms by half
                        print("Moving transforms towards potential pivot");
                        var potentialPivotPosition = potentialPivot.position;

                        for (int i = 0; i < _numTransforms; i++)
                        {
                            Vector3 directionToPotentialPivot = potentialPivotPosition - _transforms[i].position;
                            directionToPotentialPivot = directionToPotentialPivot.normalized;
                            float distanceToPotentialPivot =
                                Vector3.Distance(_transforms[i].position, potentialPivotPosition);
                            distanceToPotentialPivot /= 3;
                            _transforms[i].position = _transforms[i].position +
                                                      (directionToPotentialPivot * distanceToPotentialPivot);
                        }
                    }
                    else
                    {
                        print("Pivot point set");
                        target.position = potentialPivot.position;
                        _audioSource.PlayOneShot(beginCalibration);
                        // Clean up the GameObjects
                        for (int i = 0; i < _numTransforms; i++)
                        {
                            UnityEngine.Object.Destroy(_transforms[i].gameObject);
                        }

                        pivotPointFound = true;
                    }
                }
            }
        }

    public IEnumerator WristPivotRotation(Transform rightWrist, Transform leftWrist, Transform rightElbow)
    {
        pivotPointFound = false;

        yield return new WaitUntil(() => Keyboard.current.rightArrowKey.wasPressedThisFrame || touchpadWatcher.GetStateDown(handType));
        yield return new WaitForSeconds(5f);
        // Play the begin calibration audio clip
        AudioSource.PlayClipAtPoint(beginCalibration, transform.position);

        // Wait until the arm is parallel to the floor
        // Calculate the vectors between the rightWrist, leftWrist, and rightElbow
        float elbowToWrist = rightWrist.position.y - rightElbow.position.y;
        float rightWristToLeftWrist = leftWrist.position.y - rightWrist.position.y;

        // Check if the vectors are parallel to the ground plane
        while (!IsArmParallelToGround(elbowToWrist, rightWristToLeftWrist))
        {
            // Calculate the vectors between the rightWrist, leftWrist, and rightElbow
            elbowToWrist = rightWrist.position.y - rightElbow.position.y;
            rightWristToLeftWrist = leftWrist.position.y - rightWrist.position.y;
            yield return null;
        }

        // Rotate the wrist targets
        RotateWristTargets(rightWrist, leftWrist, rightElbow);
        AudioSource.PlayClipAtPoint(endCalibration, transform.position);
        pivotPointFound = true;
    }

    private bool IsArmParallelToGround(float elbowToWrist, float rightWristToLeftWrist)
    {
        return Mathf.Abs(elbowToWrist) < 0.025f && Mathf.Abs(rightWristToLeftWrist) < 0.025f;
    }

    private void RotateWristTargets(Transform rightWrist, Transform leftWrist, Transform rightElbow)
    {
        // Point the rightWrist and leftWrist local up toward each other, 
        // point the local right vector of the right wrist toward the right elbow, applying
        // the same rotation to the left wrist
        Vector3 rightWristUp = leftWrist.position - rightWrist.position;
        Vector3 leftWristUp = (rightWrist.position - leftWrist.position).normalized;
        Vector3 rightWristRight = rightElbow.position - rightWrist.position;
        Vector3 rightWristForward = Vector3.Cross(rightWristRight, rightWristUp).normalized;
        Vector3 leftWristForward = -rightWristForward;
        // Rotate the rightWrist and leftWrist to point their local up vectors toward each other, and their local right vectors toward the right elbow
        rightWrist.rotation = Quaternion.LookRotation(rightWristForward, rightWristUp);
        leftWrist.rotation = Quaternion.LookRotation(leftWristForward, leftWristUp);
    }


        private void CalculateRotationalOffset()
        {
            for(int i = 0; i < _numTransforms; i++)
            {
                switch (_intendedGlobalRotationAxis)
                {
                    case "x":
                        _rotationalDisplacement[i] += Mathf.Abs(_transforms[i].localEulerAngles.x - _previousRotations[i].x);
                        break;
                    case "y":
                        _rotationalDisplacement[i] += Mathf.Abs(_transforms[i].localEulerAngles.y - _previousRotations[i].y);
                        break;
                    case "z":
                        _rotationalDisplacement[i] += Mathf.Abs(_transforms[i].localEulerAngles.z - _previousRotations[i].z);
                        break;
                }
            }
            
            
        }
        
        private void RecordData()
        {
            // Record the positional displacement
            for (int i = 0; i < _numTransforms; i++)
            {
                _positionalDisplacement[i] += Vector3.Distance(_transforms[i].position, _previousPositions[i]);
            }
            // Record the target's rotational displacement
            var rotationDisplacement = Quaternion.Inverse(_previousTargetOrientation) * rotationDisplacementObject.rotation;

            _targetXDisplacement += rotationDisplacement.eulerAngles.x > 180 ? 360 - rotationDisplacement.eulerAngles.x : rotationDisplacement.eulerAngles.x;
            _targetYDisplacement += rotationDisplacement.eulerAngles.y > 180 ? 360 - rotationDisplacement.eulerAngles.y : rotationDisplacement.eulerAngles.y;
            _targetZDisplacement += rotationDisplacement.eulerAngles.z > 180 ? 360 - rotationDisplacement.eulerAngles.z : rotationDisplacement.eulerAngles.z;
            }

        /// <summary>
        /// Checks if all three of the rotational axes have been rotated by the minimum amount
        /// </summary>
        /// <returns></returns>
        private bool RotationDisplacementCriterionMet()
        {
            bool xDisplacementMet = _targetXDisplacement >= _rotationalDisplacementCriterion;
            bool yDisplacementMet = _targetYDisplacement >= _rotationalDisplacementCriterion;
            bool zDisplacementMet = _targetZDisplacement >= _rotationalDisplacementCriterion;
            return xDisplacementMet && yDisplacementMet && zDisplacementMet;
        }

    }
}