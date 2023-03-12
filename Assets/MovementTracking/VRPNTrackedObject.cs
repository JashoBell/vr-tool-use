using MovementTracking.Filters.OneEuroFilterUnity.Assets.Scripts;
using UnityEngine;

namespace MovementTracking
{
/// <summary>
/// Updates the position of a GameObject based on the values of a VRPN-based positional tracker.
/// </summary>
public class VRPNTrackedObject : MonoBehaviour
{
    [Tooltip("The name of the VRPN device you are sampling position data from.")]
    public string device = "PPT0";
    [Tooltip("The address of the VRPN server.")]
    public string address = "localhost";
    [Tooltip("zero-indexed id of the device you are sampling position data from.")]
    public int id = 0;
    [Tooltip("Whether to use the position data from the VRPN tracker.")]
    public bool position = true;
    [Tooltip("Whether to use the orientation data from the VRPN tracker.")]
    public bool orientation = false;
    [Tooltip("Whether to use the parent's orientation.")]
    public bool useParentOrientation = true;
    [Tooltip("Whether to use the OneEuroFilter to smooth the data.")]
    public bool useOneEuroFilter = false;
    [Tooltip("Any pre-translations to apply to the data.")]
    public Vector3 preTranslate = new Vector3(0, 0, 0);
    public Vector3 preRotate = new Vector3(0, 0, 0);

    readonly OneEuroFilter<Vector3> _vec3Filter = new OneEuroFilter<Vector3>(90.0f, 1.0f, 0.001f, 1.0f);
    readonly OneEuroFilter<Quaternion> _quatFilter = new OneEuroFilter<Quaternion>(90.0f, 1.0f, 0.001f, 1.0f);
    /// <summary>
    /// Combines name and address.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="device"></param>
    /// <returns>The address and device name.</returns>
    private static string GetTrackerAddress(string address, string device)
    {
        address = "@" + address;
        var fulladdress = device + address;
        return fulladdress;
    }

    private static Vector3 GetPosition(string fulladdress, int id)
    {
        var vrpnPos = VRPNUpdate.VrpnTrackerPos(fulladdress, id);
        Vector3 pos = new (vrpnPos[0], vrpnPos[1], vrpnPos[2]);
        return pos;
    }

    private static Quaternion GetQuaternion(string fulladdress, int id)
    {
        var vrpnQuat = VRPNUpdate.VrpnTrackerQuat(fulladdress, id);
        Quaternion quat = new (vrpnQuat[0], vrpnQuat[1], vrpnQuat[2], vrpnQuat[3]);
        return quat;
    }

    // Update is called once per frame
    private void Update()
    {
    
        Vector3 trackedPosition = position ? 
                                  GetPosition(GetTrackerAddress(address, device), id) 
                                : transform.position;
        Quaternion trackedOrientation = orientation ?
                                  GetQuaternion(GetTrackerAddress(address, device), id)
                                : transform.rotation;

        if(preTranslate != Vector3.zero)
        {
            trackedPosition += preTranslate;
        }
        if(preRotate != Vector3.zero)
        {
            trackedOrientation *=  Quaternion.Euler(preRotate);
        }

        transform.position = useOneEuroFilter ? 
            _vec3Filter.Filter(trackedPosition, Time.time) 
            : trackedPosition;

        if(useParentOrientation)
        {
            transform.rotation = useOneEuroFilter ? 
                _quatFilter.Filter(transform.parent.rotation * trackedOrientation, Time.time) 
                : transform.parent.rotation * trackedOrientation;
        }
        else
        {
            transform.rotation = useOneEuroFilter ? 
                _quatFilter.Filter(trackedOrientation, Time.time) 
                : trackedOrientation;
        }
    }
}
}