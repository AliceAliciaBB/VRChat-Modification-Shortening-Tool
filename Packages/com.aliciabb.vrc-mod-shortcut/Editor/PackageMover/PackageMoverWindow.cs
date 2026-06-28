using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class PackageMoverWindow : EditorWindow
{
    string _packageName;
    List<string> _roots;
    bool[] _checked;

    List<DestinationEntry> _destinations;
    string[] _popupLabels;
    int _popupIndex;
    string _customPath = "Assets/";

    int OtherIndex => _destinations.Count;

    // ─── 公開API ───────────────────────────────────────────

    public static void Open(string packageName, List<string> roots)
    {
        var win = GetWindow<PackageMoverWindow>(utility: true, title: "パッケージ移動");
        win.Init(packageName, roots);
        win.minSize = new Vector2(500, 320);
        win.ShowUtility();
    }

    // ─── 初期化 ────────────────────────────────────────────

    void Init(string packageName, List<string> roots)
    {
        _packageName  = packageName;
        _roots        = roots;
        _checked      = new bool[roots.Count];
        for (int i = 0; i < _checked.Length; i++) _checked[i] = true;

        _destinations = PackageMoverConfig.Load();

        // ポップアップラベル: 設定値 + "その他..."
        _popupLabels = new string[_destinations.Count + 1];
        for (int i = 0; i < _destinations.Count; i++)
            _popupLabels[i] = _destinations[i].label;
        _popupLabels[OtherIndex] = "その他...";

        _popupIndex = 0;
    }

    // ─── GUI ───────────────────────────────────────────────

    void OnGUI()
    {
        if (_roots == null) { Close(); return; }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField(".unitypackage インポート完了", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
            EditorGUILayout.LabelField($"パッケージ: {_packageName}", EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
        DrawLine();
        EditorGUILayout.Space(4);

        // ── 追加フォルダ一覧 ──
        EditorGUILayout.LabelField("追加されたフォルダ", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
        {
            for (int i = 0; i < _roots.Count; i++)
                _checked[i] = EditorGUILayout.ToggleLeft(_roots[i], _checked[i]);
        }

        EditorGUILayout.Space(6);
        DrawLine();
        EditorGUILayout.Space(4);

        // ── 移動先ドロップダウン ──
        EditorGUILayout.LabelField("移動先", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            int newIndex = EditorGUILayout.Popup(_popupIndex, _popupLabels, GUILayout.Width(130));
            if (newIndex != _popupIndex)
            {
                _popupIndex = newIndex;
                if (_popupIndex < _destinations.Count)
                    _customPath = _destinations[_popupIndex].path;
            }

            // 選択中パスをグレーで表示
            string previewPath = _popupIndex < _destinations.Count
                ? _destinations[_popupIndex].path
                : _customPath;
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            EditorGUILayout.LabelField(previewPath, style);
        }

        // "その他" 選択時のみパス入力欄を表示
        if (_popupIndex == OtherIndex)
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                _customPath = EditorGUILayout.TextField(_customPath);
                if (GUILayout.Button("選択...", GUILayout.Width(64)))
                    PickFolder();
            }
        }

        EditorGUILayout.Space(6);
        DrawLine();
        EditorGUILayout.Space(4);

        // ── ボタン行 ──
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("設定を開く", GUILayout.Width(100)))
                PackageMoverConfig.OpenInExplorer();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("スキップ", GUILayout.Width(90)))
                Close();

            using (new EditorGUI.DisabledGroupScope(!AnyChecked()))
            {
                if (GUILayout.Button("移動する", GUILayout.Width(90)))
                    ExecuteMove();
            }
        }

        EditorGUILayout.Space(4);
    }

    // ─── 移動処理 ──────────────────────────────────────────

    void ExecuteMove()
    {
        string destBase = _popupIndex < _destinations.Count
            ? _destinations[_popupIndex].path
            : _customPath.TrimEnd('/');

        if (string.IsNullOrEmpty(destBase) || !destBase.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("エラー", "移動先は Assets/ 以下のパスを指定してください。", "OK");
            return;
        }

        EnsureFolder(destBase);

        string focusPath = destBase;
        int movedCount = 0;

        for (int i = 0; i < _roots.Count; i++)
        {
            if (!_checked[i]) continue;

            string src        = _roots[i];
            string folderName = Path.GetFileName(src);
            string dst        = destBase + "/" + folderName;

            if (AssetDatabase.IsValidFolder(dst))
                MergeFolder(src, dst);
            else
            {
                string err = AssetDatabase.MoveAsset(src, dst);
                if (!string.IsNullOrEmpty(err))
                    Debug.LogError($"[PackageMover] 移動失敗: {src} → {dst}\n{err}");
            }

            // 1件だけ移動した場合はそのフォルダへ、複数の場合は destBase へ
            if (movedCount == 0) focusPath = dst;
            movedCount++;
        }

        AssetDatabase.Refresh();
        FocusFolder(focusPath);
        Close();
    }

    static void FocusFolder(string assetPath)
    {
        var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        if (obj == null) return;
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = obj;
        EditorGUIUtility.PingObject(obj);
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

    // ─── ユーティリティ ────────────────────────────────────

    void PickFolder()
    {
        string selected = EditorUtility.OpenFolderPanel("移動先フォルダ", Application.dataPath, "");
        if (string.IsNullOrEmpty(selected)) return;

        string dataPath = Application.dataPath.Replace('\\', '/');
        selected = selected.Replace('\\', '/');

        if (selected.StartsWith(dataPath))
            _customPath = "Assets" + selected.Substring(dataPath.Length);
        else
            EditorUtility.DisplayDialog("エラー", "Assets フォルダ以下を選択してください。", "OK");
    }

    static void EnsureFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath)) return;
        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string name   = Path.GetFileName(assetPath);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    bool AnyChecked()
    {
        foreach (var c in _checked)
            if (c) return true;
        return false;
    }

    static void DrawLine()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.35f, 0.35f, 0.35f));
    }
}
