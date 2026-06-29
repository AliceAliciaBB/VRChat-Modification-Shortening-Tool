using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 「フォルダ左クリック時の移動ルール」: Projectウィンドウで選択中のフォルダを移動対象とする。
// 元のカテゴリ(パスの第2階層)から選択フォルダまでの中間階層だけを移動先に再作成し、
// 選択フォルダ自身とその配下すべてを移動する。中間階層より上(元のカテゴリ・Assets自身)は移動しない。
public class FolderMoveWindow : EditorWindow
{
    List<DestinationEntry> _destinations;
    string[] _popupLabels;
    int _popupIndex;
    string _customPath = "Assets/";

    int OtherIndex => _destinations.Count;

    [MenuItem("ALICILIA/フォルダ移動ツール")]
    public static void ShowWindow()
    {
        var window = GetWindow<FolderMoveWindow>("フォルダ移動");
        window.minSize = new Vector2(420, 260);
        window.Show();
        window.Focus();
    }

    void OnEnable()
    {
        _destinations = PackageMoverConfig.Load();

        _popupLabels = new string[_destinations.Count + 1];
        for (int i = 0; i < _destinations.Count; i++)
            _popupLabels[i] = _destinations[i].label;
        _popupLabels[OtherIndex] = "その他...";

        Selection.selectionChanged += Repaint;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= Repaint;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("フォルダ移動ツール", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
            EditorGUILayout.LabelField("Projectウィンドウで選択中のフォルダが移動対象になります。", EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
        PackageMoverFileOps.DrawLine();
        EditorGUILayout.Space(4);

        string targetPath = GetSelectedFolderPath();
        string[] relativeParts = targetPath != null ? GetRelativeParts(targetPath) : null;

        // ── 移動対象 ──
        EditorGUILayout.LabelField("移動対象", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope(1))
        {
            if (targetPath == null)
                EditorGUILayout.HelpBox("Projectウィンドウでフォルダを選択してください(Assets自身やカテゴリフォルダ直下は対象外です)。", MessageType.Info);
            else
                EditorGUILayout.LabelField(targetPath);
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

            string previewPath = _popupIndex < _destinations.Count ? _destinations[_popupIndex].path : _customPath;
            var grayStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            EditorGUILayout.LabelField(previewPath, grayStyle);
        }

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

        if (relativeParts != null)
        {
            string destBase = _popupIndex < _destinations.Count ? _destinations[_popupIndex].path : _customPath.TrimEnd('/');
            var grayStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"→ {destBase}/{string.Join("/", relativeParts)}", grayStyle);
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

            using (new EditorGUI.DisabledGroupScope(targetPath == null))
            {
                if (GUILayout.Button("移動する", GUILayout.Width(90)))
                    Execute(targetPath, relativeParts);
            }
        }

        EditorGUILayout.Space(4);
    }

    // 選択中オブジェクトが Assets から3階層以上深い有効なフォルダなら、そのアセットパスを返す。
    // Assets自身や、カテゴリ直下(深さ2以下)のフォルダは移動対象外として null を返す。
    static string GetSelectedFolderPath()
    {
        var obj = Selection.activeObject;
        if (obj == null) return null;

        string path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;

        return path.Split('/').Length >= 3 ? path : null;
    }

    // パスの第2階層(元のカテゴリ)を除いた、移動先で再現すべき相対パスの各要素を返す。
    static string[] GetRelativeParts(string path)
    {
        return path.Split('/').Skip(2).ToArray();
    }

    void Execute(string targetPath, string[] relativeParts)
    {
        string destBase = _popupIndex < _destinations.Count
            ? _destinations[_popupIndex].path
            : _customPath.TrimEnd('/');

        if (string.IsNullOrEmpty(destBase) || !destBase.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("エラー", "移動先は Assets/ 以下のパスを指定してください。", "OK");
            return;
        }

        string dst = destBase + "/" + string.Join("/", relativeParts);

        PackageMoverFileOps.EnsureFolder(Path.GetDirectoryName(dst).Replace('\\', '/'));
        PackageMoverFileOps.MoveOrMerge(targetPath, dst);

        AssetDatabase.Refresh();

        var moved = AssetDatabase.LoadAssetAtPath<Object>(dst);
        if (moved != null)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = moved;
            EditorGUIUtility.PingObject(moved);
        }
    }
}
