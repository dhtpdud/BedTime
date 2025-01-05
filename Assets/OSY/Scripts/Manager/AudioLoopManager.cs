using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioLoopManager : MonoBehaviour
{
    public AudioSource audioSource1;
    public AudioSource audioSource2;
    public float fadeTime = 10;
    public float changeMin = 5;
    public List<AudioClip> audioClip = new List<AudioClip>();
    public void Awake()
    {
        UniTask.RunOnThreadPool(async () =>
        {
            await UniTask.SwitchToMainThread();
            for (int i = 0; !destroyCancellationToken.IsCancellationRequested; i++)
            {
                if (i >= audioClip.Count)
                    i = 0;
                AudioSource currentAudioSource = i % 2 == 0 ? audioSource1 : audioSource2;
                AudioSource nextAudioSource = i % 2 == 0 ? audioSource2 : audioSource1;
                currentAudioSource.clip = audioClip[i];
                currentAudioSource.Play();
                currentAudioSource.DOFade(1, fadeTime).SetEase(Ease.InOutSine);
                await UniTask.Delay(TimeSpan.FromSeconds(changeMin * 60));
                currentAudioSource.DOFade(0, fadeTime).SetEase(Ease.InOutSine)
                .OnComplete(() => currentAudioSource.Stop());
            }
        }, true, destroyCancellationToken).Forget();
    }
}
