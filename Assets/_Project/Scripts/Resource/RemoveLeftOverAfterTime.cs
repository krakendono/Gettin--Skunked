using UnityEngine;

public class RemoveLeftOverAfterTime : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Destroy(gameObject, 5f); // Destroy this GameObject after 5 seconds
    }

}
