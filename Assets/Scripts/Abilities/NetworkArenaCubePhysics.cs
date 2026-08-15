using System.Collections;
using PurrNet;
using UnityEngine;

/// <summary>
/// Keeps movable arena cubes server-authoritative. The server simulates their
/// rigidbodies; clients keep local copies kinematic and display transforms
/// received through PurrNet's NetworkTransform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkArenaCubePhysics : MonoBehaviour
{
    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Prevent even a single client physics step before network authority
        // has been resolved. The server enables simulation below.
        if (body != null)
            body.isKinematic = true;
    }

    private IEnumerator Start()
    {
        NetworkManager net = NetworkManager.main;
        while (net == null)
        {
            yield return null;
            net = NetworkManager.main;
        }

        if (body == null)
            yield break;

        if (net.isServer)
        {
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            yield break;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
    }
}
