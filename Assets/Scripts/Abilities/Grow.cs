using UnityEngine;

public class Grow : MonoBehaviour
{
    public float growthSpeed = 1f;
    public float growDuration = 15f;
    //public Vector3 currentSize{get; private set;}
    public Vector3 currentSize { get; private set; }
    public Vector3 origin { get; private set; }

    float timer = 0f;

    void Awake()
    {
        origin = transform.position;
        currentSize = transform.localScale;
    }

    void Update()
    {
        if (timer < growDuration)
        {
            transform.localScale += Vector3.one * growthSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            currentSize = transform.localScale;
            //Debug.Log(currentSize);
        }
    }
}
