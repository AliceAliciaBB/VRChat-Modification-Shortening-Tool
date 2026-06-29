using System.IO;
using UnityEditor;
using UnityEngine;

// PackageMover系ツール(PackageMoverWindow / FolderMoveWindow)が共通で使う、
// フォルダ階層の確保・マージ移動・共通UI部品。
internal static class PackageMoverFileOps
{
    public static void EnsureFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string name   = Path.GetFileName(assetPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    // dst が既に存在する場合は再帰的にマージ、無ければそのまま移動する。
    public static void MoveOrMerge(string src, string dst)
    {
        if (AssetDatabase.IsValidFolder(dst))
        {
            MergeFolder(src, dst);
        }
        else
        {
            string err = AssetDatabase.MoveAsset(src, dst);
            if (!string.IsNullOrEmpty(err))
                Debug.LogError($"[PackageMover] 移動失敗: {src} → {dst}\n{err}");
        }
    }

    // 再帰マージ: src の中身を dst へ移動し、空になった src を削除する
    static void MergeFolder(string src, string dst)
    {
        // 子フォルダを再帰処理
        foreach (string subSrc in AssetDatabase.GetSubFolders(src))
        {
            string name   = Path.GetFileName(subSrc);
            string subDst = dst + "/" + name;

            if (!AssetDatabase.IsValidFolder(subDst))
                AssetDatabase.CreateFolder(dst, name);

            MergeFolder(subSrc, subDst);
        }

        // 直下ファイルを移動（物理ファイルシステムを使って列挙）
        string physDir = Path.Combine(Application.dataPath,
            src.Substring("Assets/".Length)).Replace('\\', '/');

        if (Directory.Exists(physDir))
        {
            foreach (string physFile in Directory.GetFiles(physDir, "*", SearchOption.TopDirectoryOnly))
            {
                if (physFile.EndsWith(".meta")) continue;

                string fileName = Path.GetFileName(physFile);
                string fileSrc  = src + "/" + fileName;
                string fileDst  = dst + "/" + fileName;

                if (AssetDatabase.LoadAssetAtPath<Object>(fileDst) != null)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "ファイル競合",
                        $"既に存在します:\n{fileDst}\n\n上書きしますか？",
                        "上書き", "スキップ", "キャンセル");

                    if (choice == 0)
                    {
                        AssetDatabase.DeleteAsset(fileDst);
                        AssetDatabase.MoveAsset(fileSrc, fileDst);
                    }
                }
                else
                {
                    string err = AssetDatabase.MoveAsset(fileSrc, fileDst);
                    if (!string.IsNullOrEmpty(err))
                        Debug.LogError($"[PackageMover] {fileSrc} → {fileDst}: {err}");
                }
            }
        }

        // src が空になったら削除
        if (AssetDatabase.GetSubFolders(src).Length == 0 &&
            AssetDatabase.FindAssets("", new[] { src }).Length == 0)
        {
            AssetDatabase.DeleteAsset(src);
        }
    }

    // Assets配下のフォルダを選ぶダイアログを開く。キャンセル/Assets外を選んだ場合は currentPath を返す。
    public static string PickFolderUnderAssets(string currentPath)
    {
        string selected = EditorUtility.OpenFolderPanel("移動先フォルダ", Application.dataPath, "");
        if (string.IsNullOrEmpty(selected)) return currentPath;

        string dataPath = Application.dataPath.Replace('\\', '/');
        selected = selected.Replace('\\', '/');

        if (selected.StartsWith(dataPath))
            return "Assets" + selected.Substring(dataPath.Length);

        EditorUtility.DisplayDialog("エラー", "Assets フォルダ以下を選択してください。", "OK");
        return currentPath;
    }

    public static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
    }
}
