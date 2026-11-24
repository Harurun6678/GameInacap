using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;
#endif

internal static class GamePreferences
{
    internal static string ReadString(string key)
    {
        if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetString(key);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Unity añade al nombre de cada valor de PlayerPrefs el hash de la clave.
        uint hash = 5381;
        foreach (char c in key) hash = unchecked(hash * 33 ^ c);
        string prefix = Application.isEditor ? @"Software\Unity\UnityEditor\" : @"Software\";
        string path = prefix + Application.companyName + @"\GameInacap";
        return ReadPreviousValue(path, key + "_h" + hash) ?? ReadPreviousValue(path, key);
#else
        return null;
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    // PlayerPrefs guarda cadenas como REG_SZ o como bytes UTF-8 según la versión.
    private const uint StringOrBinary = 0x00000002 | 0x00000008;
    private static readonly IntPtr CurrentUser = new IntPtr(unchecked((int)0x80000001));
    private static string ReadPreviousValue(string path, string key)
    {
        uint size = 0;
        if (RegGetValueW(CurrentUser, path, key, StringOrBinary, out uint type, null, ref size) != 0 ||
            size == 0 || size > 1024 * 1024) return null;
        var data = new byte[size];
        if (RegGetValueW(CurrentUser, path, key, StringOrBinary, out type, data, ref size) != 0) return null;
        return (type == 1 ? Encoding.Unicode : Encoding.UTF8).GetString(data, 0, (int)size).TrimEnd('\0');
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegGetValueW(IntPtr root, string path, string value, uint flags,
        out uint type, [Out] byte[] data, ref uint size);
#endif
}
