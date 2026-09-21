using UnityEngine;

public class Grow : MonoBehaviour
{
    public float growthSpeed = 1f;
    public float growDuration = 15f;
    [Min(0f)] public float maximumUniformScale;
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
            transform.localScale = ResolveScale(
                transform.localScale, growthSpeed, Time.deltaTime, maximumUniformScale);
            timer += Time.deltaTime;
            currentSize = transform.localScale;
            //Debug.Log(currentSize);
        }
    }

    public static Vector3 ResolveScale(Vector3 currentScale, float speed, float deltaTime,
        float maximumScale)
    {
        Vector3 grownScale = currentScale + Vector3.one *
            (Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        if (maximumScale <= 0f)
            return grownScale;

        return new Vector3(
            Mathf.Min(grownScale.x, maximumScale),
            Mathf.Min(grownScale.y, maximumScale),
            Mathf.Min(grownScale.z, maximumScale));
    }
}
