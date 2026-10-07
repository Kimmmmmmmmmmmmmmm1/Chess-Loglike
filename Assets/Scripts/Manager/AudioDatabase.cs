using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Audio Database")]
public class AudioDatabase : ScriptableObject
{
    [System.Serializable]
    public class BGMEntry
    {
        public BGMType type;
        public AudioClip clip;
    }

    [System.Serializable]
    public class SFXEntry
    {
        public SFXType type;
        public AudioClip clip;
    }

    [System.Serializable]
    public class UIEntry
    {
        public UIType type;
        public AudioClip clip;
    }

    [SerializeField] private List<BGMEntry> bgmClips = new();
    [SerializeField] private List<SFXEntry> sfxClips = new();
    [SerializeField] private List<UIEntry> uiClips = new();

    private Dictionary<BGMType, AudioClip> bgmLookup;
    private Dictionary<SFXType, List<AudioClip>> sfxLookup;
    private Dictionary<UIType, AudioClip> uiLookup;

    private void OnEnable()
    {
        BuildLookups();
    }

    private void BuildLookups()
    {
        bgmLookup = new Dictionary<BGMType, AudioClip>();
        sfxLookup = new Dictionary<SFXType, List<AudioClip>>();
        uiLookup = new Dictionary<UIType, AudioClip>();

        foreach (var entry in bgmClips)
            bgmLookup[entry.type] = entry.clip;

        foreach (var entry in sfxClips)
        {
            if (entry.clip == null)
            {
                continue;
            }

            if (!sfxLookup.TryGetValue(entry.type, out var clips))
            {
                clips = new List<AudioClip>();
                sfxLookup[entry.type] = clips;
            }

            clips.Add(entry.clip);
        }

        foreach (var entry in uiClips)
            uiLookup[entry.type] = entry.clip;
    }

    public AudioClip GetBGM(BGMType type)
    {
        if (bgmLookup == null) BuildLookups();
        bgmLookup.TryGetValue(type, out var clip);
        return clip;
    }

    public AudioClip GetSFX(SFXType type)
    {
        if (sfxLookup == null) BuildLookups();

        if (!sfxLookup.TryGetValue(type, out var clips) || clips == null || clips.Count == 0)
        {
            return null;
        }

        return clips[UnityEngine.Random.Range(0, clips.Count)];
    }

    public AudioClip GetUI(UIType type)
    {
        if (uiLookup == null) BuildLookups();
        uiLookup.TryGetValue(type, out var clip);
        return clip;
    }
}
