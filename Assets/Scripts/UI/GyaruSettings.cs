using System;
using UnityEngine;

[Serializable]
public sealed class GyaruSettingsData
{
    public float master = .8f, interfaceVolume = .65f, hudOpacity = 1;
    public bool fullscreen, vsync = true, minimap = true;
    public int width, height;
}

public static class GyaruSettings
{
    private const string Key = "GyaruRogue.Settings.v1";
    public static GyaruSettingsData Current { get; private set; }
    public static void Load()
    {
        GyaruSettingsData value = null;
        try { if (PlayerPrefs.HasKey(Key)) value = JsonUtility.FromJson<GyaruSettingsData>(PlayerPrefs.GetString(Key)); }
        catch (ArgumentException) { }
        Current = value ?? new GyaruSettingsData { width=Screen.width, height=Screen.height, fullscreen=Screen.fullScreen };
        Normalize(Current); Apply(Current,false);
    }
    public static GyaruSettingsData Copy()
    { return JsonUtility.FromJson<GyaruSettingsData>(JsonUtility.ToJson(Current)); }
    private static void Normalize(GyaruSettingsData data)
    {
        data.master = ValidVolume(data.master,.8f); data.interfaceVolume = ValidVolume(data.interfaceVolume,.65f);
        data.hudOpacity = Mathf.Clamp(ValidVolume(data.hudOpacity,1),.5f,1);
        data.width = Mathf.Clamp(data.width,800,7680); data.height = Mathf.Clamp(data.height,600,4320);
    }
    private static float ValidVolume(float value,float fallback)
    { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value); }
    public static void Apply(GyaruSettingsData data, bool persist = true)
    {
        Normalize(data); Current = data;
        AudioListener.volume = data.master;
        QualitySettings.vSyncCount = data.vsync ? 1 : 0;
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
            Screen.SetResolution(data.width,data.height,data.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (persist) { PlayerPrefs.SetString(Key,JsonUtility.ToJson(data)); PlayerPrefs.Save(); }
    }
}
