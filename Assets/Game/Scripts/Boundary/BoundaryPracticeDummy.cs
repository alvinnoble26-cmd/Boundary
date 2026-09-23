using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns Playground's server-authoritative practice target from the normal
/// player prefab, preserving every collision, health, hit-feedback and audio
/// path used by a real opponent.
/// </summary>
[DisallowMultipleComponent]
public sealed class BoundaryPracticeDummy : MonoBehaviour
{
    private static readonly string[] SkinIds = { "beard", "turtle", "sun_ducker" };
    private PlayerMovement movement;
    private bool initialized;

    public static bool TrySpawn(PlayerMovement human)
    {
        GameManager game = GameManager.I;
        NetworkManager net = NetworkManager.main;
        if (game == null || !game.IsPlayground || net == null || !net.isServer || !net.isClient ||
            human == null || !human.isOwner || human.IsCpuControlled)
            return false;

        BoundaryPracticeDummy existing = FindFirstObjectByType<BoundaryPracticeDummy>();
        if (existing != null)
            return existing.initialized && existing.movement != null && existing.movement.isSpawned &&
                !existing.movement.hasOwner && existing.movement.HasSimulationAuthority;

        if (!net.prefabProvider.TryGetPrefabData(human.prefabId, out PrefabData data))
            return false;

        BoundaryMatchController match = BoundaryMatchController.Instance;
        if (match == null)
            return false;

        Vector3 position = match.ArenaCenter;
        position.y = match.PlatformSurfaceYAtRadius(0f) + PlayerMovement.StandingCenterHeight;
        Vector3 facing = human.transform.position - position;
        facing.y = 0f;
        Quaternion rotation = facing.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(facing, Vector3.up)
            : Quaternion.identity;

        GameObject dummy = UnityProxy.InstantiateDirectly(data.prefab, position, rotation);
        SceneManager.MoveGameObjectToScene(dummy, human.gameObject.scene);
        dummy.name = "Practice Dummy";
        BoundaryPracticeDummy controller = dummy.AddComponent<BoundaryPracticeDummy>();
        if (!controller.Initialize(Random.Range(0, int.MaxValue)))
        {
            UnityProxy.DestroyDirectly(dummy);
            return false;
        }

        NetworkIdentity.Spawn(dummy, data.prefab, net);
        dummy.GetComponent<NetworkIdentity>().RemoveOwnership(propagateToChildren: true);
        Debug.Log("[Playground] Spawned stationary practice dummy with " + controller.SkinId);
        return true;
    }

    public static string SkinForSeed(int seed)
    {
        return SkinIds[(seed & int.MaxValue) % SkinIds.Length];
    }

    private string SkinId { get; set; }

    private bool Initialize(int skinSeed)
    {
        movement = GetComponent<PlayerMovement>();
        PlayerAbilities abilities = GetComponent<PlayerAbilities>();
        if (movement == null || GetComponent<BoundaryPlayerState>() == null || abilities == null)
            return false;

        movement.ConfigurePracticeDummy();
        SkinId = SkinForSeed(skinSeed);
        abilities.ConfigurePracticeDummySkin(SkinId);
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            camera.tag = "Untagged";
        }
        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        initialized = true;
        return true;
    }
}
