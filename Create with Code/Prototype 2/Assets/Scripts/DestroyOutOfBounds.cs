using UnityEngine;

public class DestroyOutOfBounds : MonoBehaviour
{
    
    private float topBound = 30.0f;
    private float lowerBound = -10.0f;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position.z > topBound)
        {
            Destroy(gameObject);
        } else if (transform.position.z < lowerBound)
        {
            // animal has gotten past player
            Debug.Log("Game Over!");
            Destroy(gameObject);
        }

    }
}
