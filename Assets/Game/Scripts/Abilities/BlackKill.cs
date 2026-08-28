using PurrNet;
using UnityEngine;

public class BlackKill : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        RegisterServerContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterServerContact(other);
    }

    private void RegisterServerContact(Collider other)
    {
        NetworkManager net = NetworkManager.main;
        if (net == null || !net.isServer || other == null)
            return;

        BoundaryPlayerState state = other.GetComponentInParent<BoundaryPlayerState>();
        if (state != null)
            state.ServerRegisterBlackHoleContact(gameObject.GetInstanceID());
    }
}
