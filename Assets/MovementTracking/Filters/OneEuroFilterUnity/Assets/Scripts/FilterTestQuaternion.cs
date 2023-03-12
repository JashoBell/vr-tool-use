/* 
 * FilterTestQuaternion.cs
 * Author: Dario Mazzanti (dario.mazzanti@iit.it), 2016
 * 
 * Testing OneEuroFilter utility on a Unity Quaternion
 *
 */


using UnityEngine;

namespace MovementTracking.Filters.OneEuroFilterUnity.Assets.Scripts
{
	public class FilterTestQuaternion : MonoBehaviour 
	{
		public Transform noisyTransform;
		public Transform filteredTransform;
		Quaternion _quat;


		OneEuroFilter<Quaternion> _rotationFilter;
		public bool filterOn = true;
		public float filterFrequency = 120.0f;
		public float filterMinCutoff = 1.0f;
		public float filterBeta = 0.0f;
		public float filterDcutoff = 1.0f;

		public float noiseAmount = 1.0f;

		float _timer = 0.0f;

		void Start () 
		{
			_quat = new Quaternion();
			_quat.eulerAngles = new Vector3(Random.Range(-180.0f, 180.0f),Random.Range(-180.0f, 180.0f), Random.Range(-180.0f, 180.0f));

			_rotationFilter = new OneEuroFilter<Quaternion>(filterFrequency);
		}
	

		void Update () 
		{

			_timer+= Time.deltaTime;

			if(_timer > 2.0f)
			{
				_quat.eulerAngles = new Vector3(Random.Range(-180.0f, 180.0f),Random.Range(-180.0f, 180.0f), Random.Range(-180.0f, 180.0f));
				_timer = 0.0f;
			}

			noisyTransform.rotation = Quaternion.Slerp(noisyTransform.rotation, PerturbedRotation(_quat), 0.5f*Time.deltaTime);


			if(filterOn)
			{	
				_rotationFilter.UpdateParams(filterFrequency, filterMinCutoff, filterBeta, filterDcutoff);
				filteredTransform.rotation = _rotationFilter.Filter(noisyTransform.rotation);
			}
			else
				filteredTransform.rotation = noisyTransform.rotation;

		}

		//private void OnDrawGizmos()
		//{
		//    Gizmos.DrawSphere(new Vector3(noisyTransform.rotation.x, noisyTransform.rotation.y, noisyTransform.rotation.z) * 5 + Vector3.up * 5, 0.1f);
		//    Gizmos.DrawSphere(new Vector3(noisyTransform.rotation.w, 0, 0) * 5 + Vector3.up * 5, 0.1f);
		//}


		Quaternion PerturbedRotation(Quaternion rotation)
		{
			Quaternion noise = new Quaternion(Random.value*noiseAmount - noiseAmount/2.0f, Random.value*noiseAmount - noiseAmount/2.0f, Random.value*noiseAmount - noiseAmount/2.0f, Random.value*noiseAmount - noiseAmount/2.0f);

			noise.x*=Time.deltaTime;
			noise.y*=Time.deltaTime;
			noise.z*=Time.deltaTime;
			noise.w*=Time.deltaTime;

			return noise*rotation;
		}
	}
}
