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

    void Awake()
    {
        if (volumeSlider == null)
            volumeSlider = GetComponentInChildren<Slider>(true);

        float savedVolume = GetSavedVolume();
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 100f;
            volumeSlider.wholeNumbers = true;
            volumeSlider.SetValueWithoutNotify(savedVolume);
        }

        ApplyVolume(savedVolume);
        InitializeScreenShakeToggle();
    }

    public void SetScreenShake(bool enabled)
    {
        PlayerPrefs.SetInt(ScreenShakePreferenceKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void InitializeScreenShakeToggle()
    {
        if (screenShakeButton == null)
        {
            Debug.LogError("[SettingsMenu] Screen Shake button reference is missing.");
            return;
        }

        screenShakeButton.onClick.RemoveListener(ToggleScreenShake);
        screenShakeButton.onClick.AddListener(ToggleScreenShake);
        RemoveScreenShakeButtonBackground();
        if (screenShakeCheck != null)
            screenShakeCheck.transform.parent.gameObject.SetActive(false);
    }

    private void ToggleScreenShake()
    {
        SetScreenShake(!ScreenShakeEnabled);
    }

    private void RemoveScreenShakeButtonBackground()
    {
        Image buttonBackground = screenShakeButton.GetComponent<Image>();
        if (buttonBackground != null)
            buttonBackground.color = Color.clear;
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
        audioMixer.SetFloat("volume", VolumePercentToDecibels(volumePercent));
    }

    public static float GetSavedVolume()
    {
        return Mathf.Clamp(PlayerPrefs.GetFloat(VolumePreferenceKey, 100f), 0f, 100f);
    }

    public static float VolumePercentToDecibels(float volumePercent)
    {
        volumePercent = Mathf.Clamp(volumePercent, 0f, 100f);
        return volumePercent <= 0f
            ? -80f
            : Mathf.Log10(volumePercent / 100f) * 20f;
    }
}
