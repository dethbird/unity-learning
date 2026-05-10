using UnityEngine;

public class SpinPropellar : MonoBehaviour
{

    private float speed = 1200.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Vector3.forward * Time.deltaTime * speed);

    }
}
