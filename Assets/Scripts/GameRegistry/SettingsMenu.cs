using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    private const string VolumePreferenceKey = "settings.volumePercent";
    private const string ScreenShakePreferenceKey = "settings.damageScreenShake";

    public static bool ScreenShakeEnabled => PlayerPrefs.GetInt(ScreenShakePreferenceKey, 1) == 1;

    public AudioMixer audioMixer;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button screenShakeButton;
    [SerializeField] private Text screenShakeCheck;
    private GameObject screenShakeCheckGraphic;

    void Awake()
    {
        if (volumeSlider == null)
            volumeSlider = GetComponentInChildren<Slider>(true);

        float savedVolume = Mathf.Clamp(PlayerPrefs.GetFloat(VolumePreferenceKey, 100f), 0f, 100f);
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 100f;
            volumeSlider.wholeNumbers = true;
            volumeSlider.SetValueWithoutNotify(savedVolume);
        }

        ApplyVolume(savedVolume);
        InitializeScreenShakeCheckbox();
    }

    public void SetScreenShake(bool enabled)
    {
        PlayerPrefs.SetInt(ScreenShakePreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
        RefreshScreenShakeCheckbox(enabled);
    }

    private void InitializeScreenShakeCheckbox()
    {
        if (screenShakeButton == null || screenShakeCheck == null)
        {
            Debug.LogError("[SettingsMenu] Screen Shake checkbox references are missing.");
            return;
        }

        screenShakeButton.onClick.RemoveListener(ToggleScreenShake);
        screenShakeButton.onClick.AddListener(ToggleScreenShake);
        RemoveScreenShakeButtonBackground();
        screenShakeCheckGraphic = CreateScreenShakeCheckGraphic();
        RefreshScreenShakeCheckbox(ScreenShakeEnabled);
    }

    private void ToggleScreenShake()
    {
        SetScreenShake(!ScreenShakeEnabled);
    }

    private void RefreshScreenShakeCheckbox(bool enabled)
    {
        if (screenShakeCheck != null)
        {
            // LegacyRuntime.ttf does not contain the Unicode checkmark glyph on
            // every platform, so use image strokes for a reliable checkmark.
            screenShakeCheck.gameObject.SetActive(false);
        }

        if (screenShakeCheckGraphic != null)
            screenShakeCheckGraphic.SetActive(enabled);
    }

    private void RemoveScreenShakeButtonBackground()
    {
        Image buttonBackground = screenShakeButton.GetComponent<Image>();
        if (buttonBackground != null)
            buttonBackground.color = Color.clear;
    }

    private GameObject CreateScreenShakeCheckGraphic()
    {
        Transform checkbox = screenShakeCheck.transform.parent;
        Transform existingCheck = checkbox.Find("Visible Checkmark");
        if (existingCheck != null)
            return existingCheck.gameObject;

        GameObject checkmark = new GameObject("Visible Checkmark", typeof(RectTransform));
        checkmark.layer = checkbox.gameObject.layer;
        checkmark.transform.SetParent(checkbox, false);

        RectTransform checkmarkRect = (RectTransform)checkmark.transform;
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = new Vector2(3f, 3f);
        checkmarkRect.offsetMax = new Vector2(-3f, -3f);

        CreateCheckStroke(checkmark.transform, "Short Stroke", new Vector2(-3f, -1f), 7f, -45f);
        CreateCheckStroke(checkmark.transform, "Long Stroke", new Vector2(3f, 1f), 12f, 45f);
        return checkmark;
    }

    private static void CreateCheckStroke(Transform parent, string name, Vector2 position, float width, float angle)
    {
        GameObject stroke = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        stroke.transform.SetParent(parent, false);

        RectTransform strokeRect = (RectTransform)stroke.transform;
        strokeRect.anchorMin = new Vector2(0.5f, 0.5f);
        strokeRect.anchorMax = new Vector2(0.5f, 0.5f);
        strokeRect.anchoredPosition = position;
        strokeRect.sizeDelta = new Vector2(width, 2f);
        strokeRect.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image image = stroke.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    public void SetVolume(float volumePercent)
    {
        volumePercent = Mathf.Clamp(volumePercent, 0f, 100f);
        ApplyVolume(volumePercent);
        PlayerPrefs.SetFloat(VolumePreferenceKey, volumePercent);
        PlayerPrefs.Save();
    }

    private void ApplyVolume(float volumePercent)
    {
        if (audioMixer == null)
        {
            Debug.LogError("[SettingsMenu] audioMixer is NULL!");
            return;
        }

        // The UI is an intuitive 0-100 percentage. Convert it to the mixer's
        // logarithmic decibel range, reserving 0 for complete silence.
        float decibels = volumePercent <= 0f
            ? -80f
            : Mathf.Log10(volumePercent / 100f) * 20f;
        audioMixer.SetFloat("volume", decibels);
    }
}
