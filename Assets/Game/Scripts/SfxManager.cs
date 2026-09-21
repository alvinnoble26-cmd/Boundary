using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SfxManager : MonoBehaviour
{
    public const float AbilitySoundMaxDistance = 36f;
    public const float JumpVolume = 0.32f;

    public static SfxManager I { get; private set; }

    [SerializeField] private AudioClip teleport;
    [SerializeField] private AudioClip lethalHit;
    [SerializeField] private AudioClip blackHoleThrow;
    [SerializeField] private AudioClip menuButton;
    [SerializeField] private AudioClip attractThrow;
    [SerializeField] private AudioClip slide;
    [SerializeField] private AudioClip dash38;
    [SerializeField] private AudioClip repelThrow;
    [SerializeField] private AudioClip dash18;
    [SerializeField] private AudioClip skinPurchase;
    [SerializeField] private AudioClip repelExplosion;
    [SerializeField] private AudioClip attractExplosion;
    [SerializeField] private AudioClip jump;
    [SerializeField] private AudioClip outerRingClosing;
    [SerializeField] private AudioClip teleportFail;
    [SerializeField] private AudioClip blackHoleImplosion;
    [SerializeField] private AudioClip teleportWindup;
    [SerializeField] private AudioClip grappleActivation;
    [SerializeField] private AudioClip voidStart;
    [SerializeField] private AudioClip voidLoop;
    [SerializeField] private AudioClip voidSlash;
    [SerializeField] private AudioClip voidEnd;
    [SerializeField] private AudioClip baseCast;
    [SerializeField] private AudioClip baseDissolve;

    private AudioSource source;
    private AudioSource voidLoopSource;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(this);
            return;
        }

        I = this;
        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.spatialBlend = 0f;

        GameObject voidEmitter = new GameObject("Void Loop SFX", typeof(AudioSource));
        voidEmitter.transform.SetParent(transform, false);
        voidLoopSource = voidEmitter.GetComponent<AudioSource>();
        voidLoopSource.playOnAwake = false;
        voidLoopSource.loop = true;
        ConfigureWorldSource(voidLoopSource, 1f, AbilitySoundMaxDistance);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Menu")
            StartCoroutine(BindMenuButtonsNextFrame(scene));
    }

    private IEnumerator BindMenuButtonsNextFrame(Scene menuScene)
    {
        yield return null;

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject.scene != menuScene)
                continue;

            button.onClick.RemoveListener(PlayMenuButton);
            button.onClick.AddListener(PlayMenuButton);
        }
    }

    private void Play(AudioClip clip)
    {
        if (source != null && clip != null)
            source.PlayOneShot(clip);
    }

    private void PlayCapped(AudioClip clip, float maximumDuration)
    {
        if (clip == null || maximumDuration <= 0f)
            return;

        // Ability sounds may overlap. A temporary 2D source lets us cap one
        // short movement cue without stopping any simultaneous global SFX.
        GameObject emitter = new GameObject("Capped Ability SFX", typeof(AudioSource));
        emitter.transform.SetParent(transform, false);
        AudioSource cappedSource = emitter.GetComponent<AudioSource>();
        cappedSource.clip = clip;
        cappedSource.playOnAwake = false;
        cappedSource.spatialBlend = 0f;
        cappedSource.Play();
        Destroy(emitter, Mathf.Min(clip.length, maximumDuration));
    }

    private static void ConfigureWorldSource(AudioSource audioSource, float volume,
        float maximumDistance)
    {
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2.5f;
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.1f, maximumDistance);
        audioSource.dopplerLevel = 0f;
        audioSource.volume = Mathf.Clamp01(volume);
    }

    private void PlayWorld(AudioClip clip, Vector3 position, float volume,
        float maximumDuration, float maximumDistance)
    {
        if (clip == null)
            return;
        GameObject emitter = new GameObject("World Ability SFX", typeof(AudioSource));
        emitter.transform.position = position;
        AudioSource worldSource = emitter.GetComponent<AudioSource>();
        ConfigureWorldSource(worldSource, volume, maximumDistance);
        worldSource.clip = clip;
        worldSource.Play();
        float lifetime = maximumDuration > 0f
            ? Mathf.Min(clip.length, maximumDuration)
            : clip.length;
        Destroy(emitter, Mathf.Max(0.05f, lifetime));
    }

    public static void PlayWorldClip(AudioClip clip, Vector3 position, float volume = 1f,
        float maximumDuration = -1f, float maximumDistance = AbilitySoundMaxDistance)
    {
        I?.PlayWorld(clip, position, volume, maximumDuration, maximumDistance);
    }

    public static void PlayTeleport(Vector3 position) =>
        I?.PlayWorld(I.teleport, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayLethalHit() => I?.Play(I.lethalHit);
    public static void PlayBlackHoleThrow(Vector3 position) =>
        I?.PlayWorld(I.blackHoleThrow, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayAttractThrow(Vector3 position) =>
        I?.PlayWorld(I.attractThrow, position, 1f, 0.85f, AbilitySoundMaxDistance);
    public static void PlaySlide(Vector3 position) =>
        I?.PlayWorld(I.slide, position, 0.72f, 0.5f, AbilitySoundMaxDistance);
    public static void PlayRepelThrow(Vector3 position) =>
        I?.PlayWorld(I.repelThrow, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayWin() => I?.Play(I.dash18);
    public static void PlaySkinPurchase() => I?.Play(I.skinPurchase);
    public static void PlayRepelExplosion(Vector3 position) =>
        I?.PlayWorld(I.repelExplosion, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayAttractExplosion(Vector3 position) =>
        I?.PlayWorld(I.attractExplosion, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayLocalJump(Vector3 position) =>
        I?.PlayWorld(I.jump, position, JumpVolume, -1f, 18f);
    public static void PlayOuterRingClosing() => I?.PlayCapped(I.outerRingClosing, 7f);
    public static void PlayTeleportFail(Vector3 position) =>
        I?.PlayWorld(I.teleportFail, position, 0.8f, -1f, AbilitySoundMaxDistance);
    public static void PlayBlackHoleImplosion(Vector3 position) =>
        I?.PlayWorld(I.blackHoleImplosion, position, 1f, 0.7f, AbilitySoundMaxDistance);
    public static void PlayTeleportWindup(Vector3 position) =>
        I?.PlayWorld(I.teleportWindup, position, 0.9f, 0.5f, AbilitySoundMaxDistance);
    public static void PlayGrappleActivation(Vector3 position) =>
        I?.PlayWorld(I.grappleActivation, position, 0.85f, 0.35f, AbilitySoundMaxDistance);
    public static void PlayVoidStart(Vector3 position) =>
        I?.PlayWorld(I.voidStart, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayVoidSlash(Vector3 position) =>
        I?.PlayWorld(I.voidSlash, position, 0.9f, -1f, AbilitySoundMaxDistance);
    public static void PlayVoidEnd(Vector3 position) =>
        I?.PlayWorld(I.voidEnd, position, 1f, -1f, AbilitySoundMaxDistance);
    public static void PlayBaseCast(Vector3 position) =>
        I?.PlayWorld(I.baseCast, position, 0.5f, -1f, AbilitySoundMaxDistance);
    public static void PlayBaseDissolve(Vector3 position) =>
        I?.PlayWorld(I.baseDissolve, position, 0.45f, -1f, AbilitySoundMaxDistance);

    public static void StartVoidLoop(Vector3 position)
    {
        if (I == null || I.voidLoopSource == null || I.voidLoop == null)
            return;
        I.voidLoopSource.Stop();
        I.voidLoopSource.transform.position = position;
        I.voidLoopSource.clip = I.voidLoop;
        I.voidLoopSource.Play();
    }

    public static void StopVoidLoop()
    {
        if (I?.voidLoopSource != null)
            I.voidLoopSource.Stop();
    }

    public static void PlayDash(Vector3 position)
    {
        if (I == null) return;
        I.PlayWorld(I.dash38, position, 0.8f, 0.18f, AbilitySoundMaxDistance);
    }

    private void PlayMenuButton() => Play(menuButton);
}
