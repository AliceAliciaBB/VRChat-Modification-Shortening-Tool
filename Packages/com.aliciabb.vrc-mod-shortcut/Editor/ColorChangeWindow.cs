using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // 既存衣装の色変え(マテリアル一括置き換え)を行う単独ツールウィンドウ。
    // 置き換え対象(Hierarchy)と色変え版プレハブ(Project)を指定し、
    // 同じ階層パスのRendererどうしでマテリアルをスロット位置ごとに置き換える。
    public class ColorChangeWindow : EditorWindow
    {
        private GameObject _target;
        private GameObject _colorSourcePrefab;
        private bool _preview;
        private bool _applyDistanceFadeOnExecute = true;

        private bool _isPreviewing;
        private readonly Dictionary<Renderer, Material[]> _previewBackup = new Dictionary<Renderer, Material[]>();
        private readonly DistanceFadeSection _distanceFadeSection = new DistanceFadeSection();

        [MenuItem("ALICILIA/カラーチェンジツール")]
        public static void ShowWindow()
        {
            var window = GetWindow<ColorChangeWindow>("カラーチェンジ");
            window.minSize = new Vector2(VrcmstStyles.MinContentWidth, 260);
            window.Show();
            window.Focus();
        }

        private void OnDisable()
        {
            // 未確定(Undo記録の無い)プレビュー変更をウィンドウを閉じた際に残さない。
            RestorePreview();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(VrcmstStyles.WindowPadding))
            {
                EditorGUILayout.LabelField("カラーチェンジツール", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "同じ階層構造を持つ色変え版プレハブのマテリアルを、Hierarchy上の衣装へRendererのマテリアルスロット位置ごとに置き換えます。",
                    VrcmstStyles.WrappedMiniLabel);

                EditorGUILayout.Space();

                using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
                {
                    var newTarget = (GameObject)EditorGUILayout.ObjectField("置き換え対象の衣装 (Hierarchy)", _target, typeof(GameObject), true);
                    var newSource = (GameObject)EditorGUILayout.ObjectField("変える色 (Projectのプレハブ)", _colorSourcePrefab, typeof(GameObject), false);

                    if (newTarget != _target || newSource != _colorSourcePrefab)
                    {
                        RestorePreview();
                        _target = newTarget;
                        _colorSourcePrefab = newSource;
                        if (_preview) StartPreview();
                    }

                    EditorGUILayout.Space();

                    var preview = VrcmstStyles.WrappedToggleLeft("プレビュー(置き換え結果を一時的に見た目へ反映する)", _preview);
                    if (preview != _preview)
                    {
                        _preview = preview;
                        if (_preview) StartPreview();
                        else RestorePreview();
                    }

                    _applyDistanceFadeOnExecute = VrcmstStyles.WrappedToggleLeft(
                        "実行時にlilToon距離フェードを再適用する",
                        _applyDistanceFadeOnExecute);
                }

                EditorGUILayout.Space();

                using (new EditorGUI.DisabledScope(_target == null || _colorSourcePrefab == null))
                {
                    if (GUILayout.Button("置き換えを実行", GUILayout.Height(28)))
                    {
                        Execute();
                    }
                }
            }
        }

        private void StartPreview()
        {
            RestorePreview();
            if (_target == null || _colorSourcePrefab == null) return;

            foreach (var renderer in _target.GetComponentsInChildren<Renderer>(true))
            {
                _previewBackup[renderer] = renderer.sharedMaterials;
            }

            ReplaceMaterialsByMeshPosition(_target, _colorSourcePrefab, recordUndo: false, out _);
            _isPreviewing = true;
        }

        private void RestorePreview()
        {
            if (!_isPreviewing) return;

            foreach (var pair in _previewBackup)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }

            _previewBackup.Clear();
            _isPreviewing = false;
        }

        private void Execute()
        {
            RestorePreview();

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Color Change: Replace Materials");

            var matchedRendererCount = ReplaceMaterialsByMeshPosition(_target, _colorSourcePrefab, recordUndo: true, out var materialSlotCount);

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

            if (matchedRendererCount == 0)
            {
                Debug.LogWarning("[VRCMST] カラーチェンジ: 置き換え対象と同じ階層パスのRendererが見つからず、何も置き換えられませんでした。");
                return;
            }

            if (_applyDistanceFadeOnExecute)
            {
                _distanceFadeSection.Apply(_target);
            }

            Debug.Log($"[VRCMST] カラーチェンジ完了: Renderer {matchedRendererCount}個 / マテリアルスロット{materialSlotCount}個を置き換えました。");
        }

        // target配下のRendererを、source配下の同じ相対パスのRendererとマッチングし、
        // マテリアルスロットを位置(インデックス)ごとに置き換える。戻り値は置き換えたRenderer数。
        private static int ReplaceMaterialsByMeshPosition(GameObject target, GameObject source, bool recordUndo, out int materialSlotCount)
        {
            materialSlotCount = 0;
            var matchedRendererCount = 0;

            var sourceRenderersByPath = new Dictionary<string, Renderer>();
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                sourceRenderersByPath[AnimationUtility.CalculateTransformPath(renderer.transform, source.transform)] = renderer;
            }

            foreach (var targetRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(targetRenderer.transform, target.transform);
                if (!sourceRenderersByPath.TryGetValue(path, out var sourceRenderer)) continue;

                var materials = targetRenderer.sharedMaterials;
                var sourceMaterials = sourceRenderer.sharedMaterials;
                var slotCount = Mathf.Min(materials.Length, sourceMaterials.Length);
                if (slotCount == 0) continue;

                if (recordUndo) Undo.RecordObject(targetRenderer, "Replace Materials (Color Change)");

                for (var i = 0; i < slotCount; i++)
                {
                    materials[i] = sourceMaterials[i];
                }

                targetRenderer.sharedMaterials = materials;
                materialSlotCount += slotCount;
                matchedRendererCount++;

                if (recordUndo) PrefabUtility.RecordPrefabInstancePropertyModifications(targetRenderer);
            }

            return matchedRendererCount;
        }
    }
}
