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

    private AudioSource source;

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

    public static void PlayTeleport() => I?.Play(I.teleport);
    public static void PlayLethalHit() => I?.Play(I.lethalHit);
    public static void PlayBlackHoleThrow() => I?.Play(I.blackHoleThrow);
    public static void PlayAttractThrow() => I?.Play(I.attractThrow);
    public static void PlaySlide() => I?.Play(I.slide);
    public static void PlayRepelThrow() => I?.Play(I.repelThrow);
    public static void PlayWin() => I?.Play(I.dash18);
    public static void PlaySkinPurchase() => I?.Play(I.skinPurchase);
    public static void PlayRepelExplosion() => I?.Play(I.repelExplosion);
    public static void PlayAttractExplosion() => I?.Play(I.attractExplosion);
    public static void PlayJump() => I?.Play(I.jump);

    public static void PlayDash()
    {
        if (I == null) return;
        I.Play(I.dash38);
    }

    private void PlayMenuButton() => Play(menuButton);
}
