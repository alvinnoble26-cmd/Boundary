using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    private const string VolumePreferenceKey = "settings.volumePercent";

    public AudioMixer audioMixer;
    [SerializeField] private Slider volumeSlider;

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
