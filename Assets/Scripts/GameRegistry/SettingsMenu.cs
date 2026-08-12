using UnityEngine;
using UnityEngine.Audio;
public class SettingsMenu : MonoBehaviour
{
    public AudioMixer audioMixer;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void SetVolume(float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogError("[SettingsMenu] audioMixer is NULL!");
            return;
        }

        audioMixer.SetFloat("volume", volume);
    }
}
