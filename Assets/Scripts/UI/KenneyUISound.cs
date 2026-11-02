using System.Collections.Generic;
using UnityEngine;

public sealed class KenneyUISound : MonoBehaviour
{
    private static KenneyUISound instance;
    private AudioSource source;
    private float hoverAfter;
    private readonly Dictionary<string,AudioClip> clips = new Dictionary<string,AudioClip>();
    private void Awake()
    {
        instance=this; source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false; source.spatialBlend=0; source.ignoreListenerPause=true;
        foreach (string name in new [] { "Hover","Click","Back","Confirm","Error" }) clips[name]=Resources.Load<AudioClip>("KenneyUI/Audio/"+name);
    }
    public static void Play(string name)
    {
        if (instance == null || instance.source == null) return;
        if (name == "Hover" && Time.unscaledTime < instance.hoverAfter) return;
        instance.hoverAfter=Time.unscaledTime+.06f;
        AudioClip clip;
        if (instance.clips.TryGetValue(name,out clip) && clip != null)
        { instance.source.volume=GyaruSettings.Current != null ? GyaruSettings.Current.interfaceVolume : .65f; instance.source.PlayOneShot(clip); }
    }
    private void OnDestroy() { if (instance == this) instance=null; }
}
