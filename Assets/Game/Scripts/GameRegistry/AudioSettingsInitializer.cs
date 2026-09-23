using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-1000)]
public sealed class AudioSettingsInitializer : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    private void Awake()
    {
        if (audioMixer != null)
            audioMixer.SetFloat("volume", SettingsMenu.VolumePercentToDecibels(SettingsMenu.GetSavedVolume()));
    }
}
