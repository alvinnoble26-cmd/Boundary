using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SfxManager : MonoBehaviour
{
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

        voidLoopSource = gameObject.AddComponent<AudioSource>();
        voidLoopSource.playOnAwake = false;
        voidLoopSource.loop = true;
        voidLoopSource.spatialBlend = 0f;
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

    public static void PlayTeleport() => I?.Play(I.teleport);
    public static void PlayLethalHit() => I?.Play(I.lethalHit);
    public static void PlayBlackHoleThrow() => I?.Play(I.blackHoleThrow);
    public static void PlayAttractThrow() => I?.PlayCapped(I.attractThrow, 0.85f);
    public static void PlaySlide() => I?.PlayCapped(I.slide, 0.5f);
    public static void PlayRepelThrow() => I?.Play(I.repelThrow);
    public static void PlayWin() => I?.Play(I.dash18);
    public static void PlaySkinPurchase() => I?.Play(I.skinPurchase);
    public static void PlayRepelExplosion() => I?.Play(I.repelExplosion);
    public static void PlayAttractExplosion() => I?.Play(I.attractExplosion);
    public static void PlayJump() => I?.Play(I.jump);
    public static void PlayOuterRingClosing() => I?.PlayCapped(I.outerRingClosing, 7f);
    public static void PlayTeleportFail() => I?.Play(I.teleportFail);
    public static void PlayBlackHoleImplosion() => I?.PlayCapped(I.blackHoleImplosion, 0.7f);
    public static void PlayTeleportWindup() => I?.PlayCapped(I.teleportWindup, 0.5f);
    public static void PlayGrappleActivation() => I?.PlayCapped(I.grappleActivation, 0.35f);
    public static void PlayVoidStart() => I?.Play(I.voidStart);
    public static void PlayVoidSlash() => I?.Play(I.voidSlash);
    public static void PlayVoidEnd() => I?.Play(I.voidEnd);

    public static void StartVoidLoop()
    {
        if (I == null || I.voidLoopSource == null || I.voidLoop == null)
            return;
        I.voidLoopSource.Stop();
        I.voidLoopSource.clip = I.voidLoop;
        I.voidLoopSource.Play();
    }

    public static void StopVoidLoop()
    {
        if (I?.voidLoopSource != null)
            I.voidLoopSource.Stop();
    }

    public static void PlayDash()
    {
        if (I == null) return;
        I.PlayCapped(I.dash38, 0.18f);
    }

    private void PlayMenuButton() => Play(menuButton);
}
