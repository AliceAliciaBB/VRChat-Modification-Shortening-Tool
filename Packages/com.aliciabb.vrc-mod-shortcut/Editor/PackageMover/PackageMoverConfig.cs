using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class DestinationEntry
{
    public string label;
    public string path;
}

[Serializable]
class DestinationListWrapper
{
    public List<DestinationEntry> destinations;
}

public static class PackageMoverConfig
{
    const string AssetPath = "Assets/ALICILIA/Editor/PackageMover/destinations.json";

    static string AbsPath =>
        Path.Combine(Application.dataPath, "ALICILIA/Editor/PackageMover/destinations.json")
            .Replace('\\', '/');

    static readonly List<DestinationEntry> Defaults = new List<DestinationEntry>
    {
        new DestinationEntry { label = "衣装",       path = "Assets/衣装" },
        new DestinationEntry { label = "アバター",   path = "Assets/アバター" },
        new DestinationEntry { label = "髪型",       path = "Assets/髪型" },
        new DestinationEntry { label = "ツール",     path = "Assets/ツール" },
        new DestinationEntry { label = "シェーダー", path = "Assets/シェーダー" },
        new DestinationEntry { label = "ギミック",   path = "Assets/ギミック" },
        new DestinationEntry { label = "マテリアル", path = "Assets/マテリアル・テクスチャ" },
    };

    public static List<DestinationEntry> Load()
    {
        if (!File.Exists(AbsPath))
            WriteDefaults();

        try
        {
            string json = File.ReadAllText(AbsPath, System.Text.Encoding.UTF8);
            var wrapper = JsonUtility.FromJson<DestinationListWrapper>(json);
            if (wrapper?.destinations != null && wrapper.destinations.Count > 0)
                return wrapper.destinations;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PackageMover] destinations.json 読み込み失敗: {e.Message}");
        }

        return new List<DestinationEntry>(Defaults);
    }

    public static void OpenInExplorer()
    {
        if (!File.Exists(AbsPath))
        {
            WriteDefaults();
            AssetDatabase.ImportAsset(AssetPath);
        }
        EditorUtility.RevealInFinder(AbsPath);
    }

    static void WriteDefaults()
    {
        var wrapper = new DestinationListWrapper { destinations = new List<DestinationEntry>(Defaults) };
        string json = JsonUtility.ToJson(wrapper, prettyPrint: true);
        Directory.CreateDirectory(Path.GetDirectoryName(AbsPath));
        File.WriteAllText(AbsPath, json, System.Text.Encoding.UTF8);
    }
}
