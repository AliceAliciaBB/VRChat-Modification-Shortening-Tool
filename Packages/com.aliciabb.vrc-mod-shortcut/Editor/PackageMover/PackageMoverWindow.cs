using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 1件の移動候補。SourcePathが実際に移動するアセット、RelativeDestPathは
// 移動先カテゴリ配下に作る相対パス(パターン1: "作者フォルダ"、パターン2: "作者フォルダ/アセットフォルダ")。
class PackageMoverCandidate
{
    public string SourcePath;
    public string RelativeDestPath;
}

public class PackageMoverWindow : EditorWindow
{
    string _packageName;
    List<PackageMoverCandidate> _candidates;
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
        _candidates   = BuildCandidates(roots);
        _checked      = new bool[_candidates.Count];
        for (int i = 0; i < _checked.Length; i++) _checked[i] = true;

        _destinations = PackageMoverConfig.Load();

        // ポップアップラベル: 設定値 + "その他..."
        _popupLabels = new string[_destinations.Count + 1];
        for (int i = 0; i < _destinations.Count; i++)
            _popupLabels[i] = _destinations[i].label;
        _popupLabels[OtherIndex] = "その他...";

        _popupIndex = 0;
    }

    // 新規追加された各ルート(<作者フォルダ>)を、構造だけで2パターンに分類する。
    // ・他のカテゴリ内に同名フォルダが無い → ルート全体が1候補(パターン1)
    // ・他のカテゴリ内に同名フォルダが既にある → ルート直下の各<アセットフォルダ>が個別の候補(パターン2)
    static List<PackageMoverCandidate> BuildCandidates(List<string> roots)
    {
        var result = new List<PackageMoverCandidate>();

        foreach (string root in roots)
        {
            string authorName = Path.GetFileName(root);

            if (SameNameFolderExistsElsewhere(root))
            {
                foreach (string assetFolder in AssetDatabase.GetSubFolders(root))
                {
                    result.Add(new PackageMoverCandidate
                    {
                        SourcePath = assetFolder,
                        RelativeDestPath = authorName + "/" + Path.GetFileName(assetFolder),
                    });
                }
            }
            else
            {
                result.Add(new PackageMoverCandidate
                {
                    SourcePath = root,
                    RelativeDestPath = authorName,
                });
            }
        }

        return result;
    }

    // newRootPath と同じ名前のフォルダが、Assets内の(newRootPath自身を除く)どこかに既に存在するか。
    static bool SameNameFolderExistsElsewhere(string newRootPath)
    {
        string targetName = Path.GetFileName(newRootPath);
        return AssetDatabase.GetSubFolders("Assets")
            .Any(sub => SearchFolderName(sub, targetName, newRootPath));
    }

    static bool SearchFolderName(string current, string targetName, string exclude)
    {
        if (current == exclude) return false;
        if (Path.GetFileName(current) == targetName) return true;

        return AssetDatabase.GetSubFolders(current)
            .Any(sub => SearchFolderName(sub, targetName, exclude));
    }

    // ─── GUI ───────────────────────────────────────────────

    void OnGUI()
    {
        if (_candidates == null) { Close(); return; }

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField(".unitypackage インポート完了", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
            EditorGUILayout.LabelField($"パッケージ: {_packageName}", EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
        PackageMoverFileOps.DrawLine();
        EditorGUILayout.Space(4);

        string destBaseForPreview = _popupIndex < _destinations.Count
            ? _destinations[_popupIndex].path
            : _customPath.TrimEnd('/');

        // ── 追加フォルダ一覧 ──
        EditorGUILayout.LabelField("追加されたフォルダ", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
        {
            var hintStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };

            for (int i = 0; i < _candidates.Count; i++)
            {
                _checked[i] = EditorGUILayout.ToggleLeft(_candidates[i].SourcePath, _checked[i]);
                using (new EditorGUI.IndentLevelScope(1))
                    EditorGUILayout.LabelField($"→ {destBaseForPreview}/{_candidates[i].RelativeDestPath}", hintStyle);
            }
        }

        EditorGUILayout.Space(6);
        PackageMoverFileOps.DrawLine();
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
                    _customPath = PackageMoverFileOps.PickFolderUnderAssets(_customPath);
            }
        }

        EditorGUILayout.Space(6);
        PackageMoverFileOps.DrawLine();
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

        string focusPath = destBase;
        int movedCount = 0;

        for (int i = 0; i < _candidates.Count; i++)
        {
            if (!_checked[i]) continue;

            string src = _candidates[i].SourcePath;
            string dst = destBase + "/" + _candidates[i].RelativeDestPath;

            // パターン2では作者フォルダ階層が移動先にまだ無い場合があるため、親フォルダをここで確保する。
            PackageMoverFileOps.EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
            PackageMoverFileOps.MoveOrMerge(src, dst);

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

    // ─── ユーティリティ ────────────────────────────────────

    bool AnyChecked()
    {
        foreach (var c in _checked)
            if (c) return true;
        return false;
    }
}
