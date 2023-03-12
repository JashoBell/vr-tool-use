/* 
 * OneEuroFilter.cs
 * Author: Dario Mazzanti (dario.mazzanti@iit.it), 2016
 * 
 * This Unity C# utility is based on the C++ implementation of the OneEuroFilter algorithm by Nicolas Roussel (http://www.lifl.fr/~casiez/1euro/OneEuroFilter.cc)
 * More info on the 1€ filter by Géry Casiez at http://www.lifl.fr/~casiez/1euro/
 *
 */

using System;
using UnityEngine;

namespace MovementTracking.Filters.OneEuroFilterUnity.Assets.Scripts
{
	class LowPassFilter 
	{
		float _y, _a, _s;
		bool _initialized;

		public void SetAlpha(float alpha) 
		{
			if (alpha<=0.0f || alpha>1.0f)
			{
				Debug.LogError("alpha should be in (0.0., 1.0]");
				return;
			}
			_a = alpha;
		}

		public LowPassFilter(float alpha, float initval=0.0f) 
		{
			_y = _s = initval;
			SetAlpha(alpha);
			_initialized = false;
		}

		public float Filter(float value) 
		{
			float result;
			if (_initialized)
				result = _a*value + (1.0f-_a)*_s;
			else 
			{
				result = value;
				_initialized = true;
			}
			_y = value;
			_s = result;
			return result;
		}

		public float FilterWithAlpha(float value, float alpha) 
		{
			SetAlpha(alpha);
			return Filter(value);
		}

		public bool HasLastRawValue() 
		{
			return _initialized;
		}

		public float LastRawValue() 
		{
			return _y;
		}

	};

// -----------------------------------------------------------------

	public class OneEuroFilter 
	{
		float _freq;
		float _mincutoff;
		float _beta;
		float _dcutoff;
		LowPassFilter _x;
		LowPassFilter _dx;
		float _lasttime;

		// currValue contains the latest value which have been succesfully filtered
		// prevValue contains the previous filtered value
		public float CurrValue {get; protected set;}
		public float PrevValue {get; protected set;}

		float Alpha(float cutoff) 
		{
			float te = 1.0f/_freq;
			float tau = 1.0f/(2.0f*Mathf.PI*cutoff);
			return 1.0f/(1.0f + tau/te);
		}

		void SetFrequency(float f) 
		{
			if (f<=0.0f)
			{
				Debug.LogError("freq should be > 0");
				return;
			}
			_freq = f;
		}

		void SetMinCutoff(float mc) 
		{
			if (mc<=0.0f)
			{
				Debug.LogError("mincutoff should be > 0");
				return;
			}
			_mincutoff = mc;
		}

		void SetBeta(float b) 
		{
			_beta = b;
		}

		void SetDerivateCutoff(float dc) 
		{
			if (dc<=0.0f)
			{
				Debug.LogError("dcutoff should be > 0");
				return;
			}
			_dcutoff = dc;
		}

		public OneEuroFilter(float freq, float mincutoff=1.0f, float beta=0.0f, float dcutoff=1.0f) 
		{
			SetFrequency(freq);
			SetMinCutoff(mincutoff);
			SetBeta(beta);
			SetDerivateCutoff(dcutoff);
			_x = new LowPassFilter(Alpha(this._mincutoff));
			_dx = new LowPassFilter(Alpha(this._dcutoff));
			_lasttime = -1.0f;

			CurrValue = 0.0f;
			PrevValue = CurrValue;
		}

		public void UpdateParams(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
		{
			SetFrequency(freq);
			SetMinCutoff(mincutoff);
			SetBeta(beta);
			SetDerivateCutoff(dcutoff);
			_x.SetAlpha(Alpha(this._mincutoff));
			_dx.SetAlpha(Alpha(this._dcutoff));	
		}

		public float Filter(float value, float timestamp = -1.0f) 
		{
			PrevValue = CurrValue;
		
			// update the sampling frequency based on timestamps
			if (_lasttime!=-1.0f && timestamp != -1.0f)
				_freq = 1.0f/(timestamp-_lasttime);
			_lasttime = timestamp;
			// estimate the current variation per second 
			float dvalue = _x.HasLastRawValue() ? (value - _x.LastRawValue())*_freq : 0.0f; // FIXME: 0.0 or value? 
			float edvalue = _dx.FilterWithAlpha(dvalue, Alpha(_dcutoff));
			// use it to update the cutoff frequency
			float cutoff = _mincutoff + _beta*Mathf.Abs(edvalue);
			// filter the given value
			CurrValue = _x.FilterWithAlpha(value, Alpha(cutoff));

			return CurrValue;
		}
	} ;
	

// this class instantiates an array of OneEuroFilter objects to filter each component of Vector2, Vector3, Vector4 or Quaternion types
	public class OneEuroFilter<T> where T : struct
	{
		// containst the type of T
		Type _type;
		// the array of filters
		OneEuroFilter[] _oneEuroFilters;

		// filter parameters
		public float Freq {get; protected set;}
		public float Mincutoff {get; protected set;}
		public float Beta {get; protected set;}
		public float Dcutoff {get; protected set;}

		// currValue contains the latest value which have been succesfully filtered
		// prevValue contains the previous filtered value
		public T CurrValue {get; protected set;}
		public T PrevValue {get; protected set;}

		// initialization of our filter(s)
		public OneEuroFilter(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
		{
			_type = typeof(T);
			CurrValue = new T();
			PrevValue = new T();

			Freq = freq;
			Mincutoff = mincutoff;
			Beta = beta;
			Dcutoff = dcutoff;

			if(_type == typeof(Vector2))
				_oneEuroFilters = new OneEuroFilter[2];

			else if(_type == typeof(Vector3))
				_oneEuroFilters = new OneEuroFilter[3];

			else if(_type == typeof(Vector4) || _type == typeof(Quaternion))
				_oneEuroFilters = new OneEuroFilter[4];
			else
			{
				Debug.LogError(_type + " is not a supported type");
				return;
			}

			for(int i = 0; i < _oneEuroFilters.Length; i++)
				_oneEuroFilters[i] = new OneEuroFilter(Freq, Mincutoff, Beta, Dcutoff);		
		}

		// updates the filter parameters
		public void UpdateParams(float freq, float mincutoff = 1.0f, float beta = 0.0f, float dcutoff = 1.0f)
		{
			Freq = freq;
			Mincutoff = mincutoff;
			Beta = beta;
			Dcutoff = dcutoff;
		
			for(int i = 0; i < _oneEuroFilters.Length; i++)
				_oneEuroFilters[i].UpdateParams(Freq, Mincutoff, Beta, Dcutoff);
		}


		// filters the provided _value and returns the result.
		// Note: a timestamp can also be provided - will override filter frequency.
		public T Filter<TU>(TU value, float timestamp = -1.0f) where TU : struct
		{
			PrevValue = CurrValue;
		
			if(typeof(TU) != _type)
			{
				Debug.LogError("WARNING! " + typeof(TU) + " when " + _type + " is expected!\nReturning previous filtered value" );
				CurrValue = PrevValue;
	
				return (T) Convert.ChangeType(CurrValue, typeof(T));
			}

			if(_type == typeof(Vector2))
			{
				Vector2 output = Vector2.zero;
				Vector2 input = (Vector2) Convert.ChangeType(value, typeof(Vector2));

				for(int i = 0; i < _oneEuroFilters.Length; i++)
					output[i] = _oneEuroFilters[i].Filter(input[i], timestamp);

				CurrValue = (T) Convert.ChangeType(output, typeof(T));
			}

			else if(_type == typeof(Vector3))
			{
				Vector3 output = Vector3.zero;
				Vector3 input = (Vector3) Convert.ChangeType(value, typeof(Vector3));

				for(int i = 0; i < _oneEuroFilters.Length; i++)
					output[i] = _oneEuroFilters[i].Filter(input[i], timestamp);

				CurrValue = (T) Convert.ChangeType(output, typeof(T));
			}

			else if(_type == typeof(Vector4))
			{
				Vector4 output = Vector4.zero;
				Vector4 input = (Vector4) Convert.ChangeType(value, typeof(Vector4));

				for(int i = 0; i < _oneEuroFilters.Length; i++)
					output[i] = _oneEuroFilters[i].Filter(input[i], timestamp);

				CurrValue = (T) Convert.ChangeType(output, typeof(T));
			}

			else
			{
				Quaternion output = Quaternion.identity;
				Quaternion input = (Quaternion) Convert.ChangeType(value, typeof(Quaternion));
            
				// Workaround that take into account that some input device sends
				// quaternion that represent only a half of all possible values.
				// this piece of code does not affect normal behaviour (when the
				// input use the full range of possible values).
				if (Vector4.SqrMagnitude(new Vector4(_oneEuroFilters[0].CurrValue, _oneEuroFilters[1].CurrValue, _oneEuroFilters[2].CurrValue, _oneEuroFilters[3].CurrValue).normalized
				                         - new Vector4(input[0], input[1], input[2], input[3]).normalized) > 2)
				{
					input = new Quaternion(-input.x, -input.y, -input.z, -input.w);
				}

				for(int i = 0; i < _oneEuroFilters.Length; i++)
					output[i] = _oneEuroFilters[i].Filter(input[i], timestamp);

				CurrValue = (T) Convert.ChangeType(output, typeof(T));
			}

			return (T) Convert.ChangeType(CurrValue, typeof(T));
		}
	}
}