using System.Collections.Generic;
using System.Linq;
using UnityEditor;

[InitializeOnLoad]
public static class PackageMoverWatcher
{
    static HashSet<string> _snapshot;

    static PackageMoverWatcher()
    {
        AssetDatabase.importPackageStarted   += OnStarted;
        AssetDatabase.importPackageCompleted += OnCompleted;
    }

    static void OnStarted(string packageName)
    {
        _snapshot = new HashSet<string>(AssetDatabase.GetAllAssetPaths());
    }

    static void OnCompleted(string packageName)
    {
        if (_snapshot == null) return;
        var snapshot = _snapshot;
        _snapshot = null;

        // delayCall でDB更新完了を待つ
        EditorApplication.delayCall += () =>
        {
            var added = AssetDatabase.GetAllAssetPaths()
                .Where(p => !snapshot.Contains(p) && p.StartsWith("Assets/"))
                .OrderBy(p => p)
                .ToArray();

            if (added.Length == 0) return;

            var roots = ExtractRoots(added);
            if (roots.Count == 0) return;

            PackageMoverWindow.Open(packageName, roots);
        };
    }

    // 追加パスの中から最上位フォルダのみを返す
    static List<string> ExtractRoots(string[] sortedPaths)
    {
        var roots = new List<string>();
        foreach (var p in sortedPaths)
        {
            if (!AssetDatabase.IsValidFolder(p)) continue;
            if (roots.Any(r => p.StartsWith(r + "/"))) continue;
            roots.Add(p);
        }
        return roots;
    }
}
