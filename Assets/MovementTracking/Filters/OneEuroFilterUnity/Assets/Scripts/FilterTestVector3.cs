/* 
 * FilterTestVector3.cs
 * Author: Dario Mazzanti (dario.mazzanti@iit.it), 2016
 * 
 * Testing OneEuroFilter utility on a Unity Vector3
 *
 */

using UnityEngine;

namespace MovementTracking.Filters.OneEuroFilterUnity.Assets.Scripts
{
	public class FilterTestVector3 : MonoBehaviour 
	{
		public Transform noisyTransform;
		public Transform filteredTransform;

		Vector3 _startingPosition;
		Vector3 _offset;

		OneEuroFilter<Vector3> _positionFilter;

		public bool filterOn = true;

		public float filterFrequency = 120.0f;
		public float filterMinCutoff = 1.0f;
		public float filterBeta = 0.0f;
		public float filterDcutoff = 1.0f;

		public float noiseAmount = 1f;

		public float oscillationSpeed  = 0.025f;
		float _angle  = 0.0f;

		void Start () 
		{
			_positionFilter = new OneEuroFilter<Vector3>(filterFrequency);
			_startingPosition = noisyTransform.position;

			_offset = filteredTransform.position - noisyTransform.position;
		}

		void Update () 
		{
			noisyTransform.position = PerturbedPosition(_startingPosition) + Oscillation();

			if(filterOn)
			{	
				_positionFilter.UpdateParams(filterFrequency, filterMinCutoff, filterBeta, filterDcutoff);
				filteredTransform.position = _positionFilter.Filter(noisyTransform.position) + _offset;
			}
			else
				filteredTransform.position = noisyTransform.position + _offset;
		}

		Vector3 PerturbedPosition(Vector3 position)
		{
			Vector3 noise = new Vector3(Random.value*noiseAmount - noiseAmount/2.0f, Random.value*noiseAmount - noiseAmount/2.0f, Random.value*noiseAmount - noiseAmount/2.0f)*Time.deltaTime;

			return position + noise;
		}

		Vector3 Oscillation()
		{
			_angle += oscillationSpeed*Time.deltaTime;
			if(_angle == 360f)
				_angle = 0f;		
		
			return new  Vector3(0f, Mathf.Sin(_angle), 0f);
		}
	}
}