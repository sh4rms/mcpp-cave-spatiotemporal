using UnityEngine;

public class Spin : MonoBehaviour
{
    [SerializeField]
    private Vector3 angles = new Vector3(0.1f, 0.1f, 0.1f);
    // Update is called once per frame
    void Update()
    {
        transform.Rotate(angles);
    }
}
