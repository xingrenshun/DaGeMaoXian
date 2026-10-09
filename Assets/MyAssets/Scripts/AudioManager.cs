using System;
using UnityEngine;
//电子邮件puhalskijsemen@gmail.com
//源码网站 开vpn全局模式打开 https://web3incubators.com/
//电报https://t.me/gamecode999
//网页客服 https://web3incubators.com/kefu.html
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public AudioClip[] audioClips;

    private void Awake()
    {
        if (Instance != null)
            Destroy(gameObject);
        else
            Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void Play(string name)
    {
        AudioClip clip = Array.Find(audioClips, sound => sound.name == name);

        if (clip == null)
        {
            Debug.LogWarning("Clip not found: " + name);
            return;
        }

        AudioSource tempAudioSource = gameObject.AddComponent<AudioSource>();

        if (name == "Coin")
            tempAudioSource.volume = .2f;

        tempAudioSource.clip = clip;
        tempAudioSource.Play();
        Destroy(tempAudioSource, clip.length);
    }
}