using UnityEngine;
using LSL;

public class LSLOutlet : MonoBehaviour
{
    private StreamOutlet outlet;
    private float[] data = new float[3]; // Euler angles
    void Start()
    {
        string StreamName = "BrainWorks.Sample.Orientation";
        string StreamType = "Mocap";
        Hash128 hash = new Hash128();
        hash.Append(StreamName);
        hash.Append(StreamType);
        hash.Append(gameObject.GetInstanceID());
        StreamInfo info = new StreamInfo(StreamName, StreamType, data.Length, 60, channel_format_t.cf_float32, hash.ToString());
        outlet = new StreamOutlet(info);
    }

    void LateUpdate()
    {
        if (outlet != null)
        {
            data[0] = transform.eulerAngles.x;
            data[1] = transform.eulerAngles.y;
            data[2] = transform.eulerAngles.z;
            outlet.push_sample(data);
        }
    }
}
