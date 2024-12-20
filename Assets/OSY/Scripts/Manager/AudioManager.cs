using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    public AudioSource audioSource;
    public AudioClip creeperTrigger;
    public List<AudioClip> explosions = new List<AudioClip>();
    private void Awake()
    {
        instance = this;
    }
}
