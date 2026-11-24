using System;
using UnityEngine;

[Serializable]
public sealed class GameSettingsData
{
    public float master = .8f, interfaceVolume = .65f, hudOpacity = 1;
    public bool fullscreen, vsync = true, minimap = true;
    public int width, height;
}

public static class GameSettings
{
    private const string Key = "GyaruRogue.Settings.v1";
    public static GameSettingsData Current
    {
        get; private set;
    }
    public static void Load()
    {
        GameSettingsData value = null;
        try
        {
            string json = GamePreferences.ReadString(Key);
            if (!string.IsNullOrEmpty(json))
                value = JsonUtility.FromJson<GameSettingsData>(json);
        }
        catch (ArgumentException) { }
        Current = value ?? new GameSettingsData { width = Screen.width, height = Screen.height, fullscreen = Screen.fullScreen };
        Normalize(Current);
        Apply(Current, false);
    }
    public static GameSettingsData Copy()
    {
        return JsonUtility.FromJson<GameSettingsData>(JsonUtility.ToJson(Current));
    }
    private static void Normalize(GameSettingsData data)
    {
        data.master = ValidVolume(data.master, .8f);
        data.interfaceVolume = ValidVolume(data.interfaceVolume, .65f);
        data.hudOpacity = Mathf.Clamp(ValidVolume(data.hudOpacity, 1), .5f, 1);
        data.width = Mathf.Clamp(data.width, 800, 7680);
        data.height = Mathf.Clamp(data.height, 600, 4320);
    }
    private static float ValidVolume(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }
    public static void Apply(GameSettingsData data, bool persist = true)
    {
        Normalize(data);
        Current = data;
        AudioListener.volume = data.master;
        QualitySettings.vSyncCount = data.vsync ? 1 : 0;
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
            Screen.SetResolution(data.width, data.height, data.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (persist)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
