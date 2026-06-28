using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // 手順書の「③アイテム追加」に対応。プレハブを格納先へ追加し、
    // メニュー作成タイプ(衣装/髪型/作成しない)に応じて以降の処理を分岐する。
    internal class CostumeSection
    {
        private enum ItemType
        {
            Costume,
            Hairstyle,
            Other,
        }

        private static readonly string[] ItemTypeLabels = { "衣装", "髪型", "作成しない" };
        private static readonly ItemType[] ItemTypeValues = { ItemType.Costume, ItemType.Hairstyle, ItemType.Other };

        private const string AutoApplyDistanceFadePrefKey = "Vrcmst.CostumeSection.AutoApplyDistanceFade";
        private const string ReplaceNameWithPrefabNamePrefKey = "Vrcmst.CostumeSection.ReplaceNameWithPrefabName";
        private const string ApplyMeshSettingsInheritPrefKey = "Vrcmst.CostumeSection.ApplyMeshSettingsInherit";
        private const string ShowTranslationSectionPrefKey = "Vrcmst.CostumeSection.ShowTranslationSection";

        private const string TranslateFromLanguage = "en";
        private const string TranslateToLanguage = "ja";

        private GameObject _prefab;
        private ItemType _itemType = ItemType.Costume;
        private int _categoryIndex;
        private bool? _autoApplyDistanceFade;
        private bool? _replaceNameWithPrefabName;
        private bool? _applyMeshSettingsInherit;
        private bool? _showTranslationSection;
        private string _predictedAvatarName = "";

        private readonly List<ModularAvatarMenuItem> _pendingTranslationItems = new List<ModularAvatarMenuItem>();
        private string _pendingTranslationText = "";

        // ④(髪型の排他グループ化)を表示すべきか、MainWindowから参照するための公開フラグ。
        public bool IsHairstyleTypeSelected => _itemType == ItemType.Hairstyle;

        // EditorPrefsはScriptableObject(MainWindow)のフィールド初期化子から呼ぶと例外になるため、
        // 実際にGUIが描画される時点まで読み込みを遅延させる。
        private bool AutoApplyDistanceFade
        {
            get
            {
                if (_autoApplyDistanceFade == null)
                {
                    _autoApplyDistanceFade = EditorPrefs.GetBool(AutoApplyDistanceFadePrefKey, true);
                }

                return _autoApplyDistanceFade.Value;
            }
        }

        private bool ReplaceNameWithPrefabName
        {
            get
            {
                if (_replaceNameWithPrefabName == null)
                {
                    _replaceNameWithPrefabName = EditorPrefs.GetBool(ReplaceNameWithPrefabNamePrefKey, true);
                }

                return _replaceNameWithPrefabName.Value;
            }
        }

        private bool ApplyMeshSettingsInherit
        {
            get
            {
                if (_applyMeshSettingsInherit == null)
                {
                    _applyMeshSettingsInherit = EditorPrefs.GetBool(ApplyMeshSettingsInheritPrefKey, true);
                }

                return _applyMeshSettingsInherit.Value;
            }
        }

        private bool ShowTranslationSection
        {
            get
            {
                if (_showTranslationSection == null)
                {
                    _showTranslationSection = EditorPrefs.GetBool(ShowTranslationSectionPrefKey, false);
                }

                return _showTranslationSection.Value;
            }
        }

        private GUIStyle _wrappedLabelStyle;

        // 説明文がWindow幅で切れずに折り返されるようにするスタイル。
        // EditorStyles系はGUI描画中以外でのアクセスを避けるため、フィールド初期化子ではなく遅延生成する。
        private GUIStyle WrappedLabelStyle
        {
            get
            {
                if (_wrappedLabelStyle == null)
                {
                    _wrappedLabelStyle = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        margin = new RectOffset(2, 2, 4, 4),
                    };
                }

                return _wrappedLabelStyle;
            }
        }

        public void DrawGUI(GameObject avatarRoot, DistanceFadeSection fadeSection)
        {
            EditorGUILayout.LabelField("③ アイテム追加 (プレハブ → 格納先)", EditorStyles.boldLabel);

            var categories = MenuSetupSection.GetCategoryNames(avatarRoot);
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("先に②で格納先を作成してください。", MessageType.Info);
                return;
            }

            string categoryName;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                _categoryIndex = Mathf.Clamp(_categoryIndex, 0, categories.Count - 1);
                _categoryIndex = EditorGUILayout.Popup("格納先", _categoryIndex, categories.ToArray());
                categoryName = categories[_categoryIndex];

                var previousPrefab = _prefab;
                _prefab = (GameObject)EditorGUILayout.ObjectField("プレハブ", _prefab, typeof(GameObject), false);
                if (_prefab != null && _prefab != previousPrefab && avatarRoot != null && ReplaceNameWithPrefabName)
                {
                    _predictedAvatarName = ModularAvatarOps.PredictAvatarNameForPrefab(avatarRoot, _prefab.name);
                }

                var itemTypeIndex = System.Array.IndexOf(ItemTypeValues, _itemType);
                itemTypeIndex = EditorGUILayout.Popup("メニュー作成タイプ", itemTypeIndex, ItemTypeLabels);
                _itemType = ItemTypeValues[itemTypeIndex];
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("追加オプション", EditorStyles.miniBoldLabel);

                var replaceNameWithPrefabName = EditorGUILayout.ToggleLeft(
                    $"プレハブ割り当て時にアバター名へ反映する(仮名_{ModularAvatarOps.DuplicateNameMarker}は置き換え、それ以外は末尾に追加)",
                    ReplaceNameWithPrefabName,
                    WrappedLabelStyle);
                if (replaceNameWithPrefabName != ReplaceNameWithPrefabName)
                {
                    _replaceNameWithPrefabName = replaceNameWithPrefabName;
                    EditorPrefs.SetBool(ReplaceNameWithPrefabNamePrefKey, replaceNameWithPrefabName);
                }

                if (ReplaceNameWithPrefabName && _prefab != null)
                {
                    EditorGUILayout.LabelField("変更後アバター名(予測・編集可)", WrappedLabelStyle);
                    _predictedAvatarName = EditorGUILayout.TextField(_predictedAvatarName);
                }

                var applyMeshSettingsInherit = EditorGUILayout.ToggleLeft(
                    "MA Mesh Settingsが設定の場合、親に設定があれば継承に変更する",
                    ApplyMeshSettingsInherit,
                    WrappedLabelStyle);
                if (applyMeshSettingsInherit != ApplyMeshSettingsInherit)
                {
                    _applyMeshSettingsInherit = applyMeshSettingsInherit;
                    EditorPrefs.SetBool(ApplyMeshSettingsInheritPrefKey, applyMeshSettingsInherit);
                }

                var autoApplyDistanceFade = EditorGUILayout.ToggleLeft(
                    "追加時に距離フェードを一括適用",
                    AutoApplyDistanceFade,
                    WrappedLabelStyle);
                if (autoApplyDistanceFade != AutoApplyDistanceFade)
                {
                    _autoApplyDistanceFade = autoApplyDistanceFade;
                    EditorPrefs.SetBool(AutoApplyDistanceFadePrefKey, autoApplyDistanceFade);
                }

                var showTranslationSection = EditorGUILayout.ToggleLeft(
                    "衣装追加後にメニュー名の翻訳区画を表示する(自動翻訳/手動編集してから適用)",
                    ShowTranslationSection,
                    WrappedLabelStyle);
                if (showTranslationSection != ShowTranslationSection)
                {
                    _showTranslationSection = showTranslationSection;
                    EditorPrefs.SetBool(ShowTranslationSectionPrefKey, showTranslationSection);
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_prefab == null))
            {
                if (GUILayout.Button("追加", GUILayout.Height(28)))
                {
                    AddItem(avatarRoot, categoryName, fadeSection);
                }
            }

            if (_pendingTranslationItems.Count > 0)
            {
                EditorGUILayout.Space();
                DrawTranslationSection();
            }
        }

        // TRenameTool.cs(Assets/ツール/LGC/翻訳ツール)の「名前リスト」と同じく、
        // 1行=1項目の複数行テキストエリアにして外部の翻訳サービスへのコピペを容易にする。
        private void DrawTranslationSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("メニュー名の翻訳", EditorStyles.miniBoldLabel);

                EditorGUILayout.LabelField("元の名前(参考・コピー用)", WrappedLabelStyle);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextArea(
                        string.Join("\n", _pendingTranslationItems.Select(item => item != null ? item.Control.name : "")),
                        GUILayout.Height(LineAreaHeight(_pendingTranslationItems.Count)));
                }

                EditorGUILayout.LabelField(
                    "各行が上の「元の名前」と1対1で対応します。コピーして外部の翻訳サービスに貼り付け、" +
                    "結果をここに貼り付けてから「適用」を押してください。「自動翻訳」でこのツール内から一括翻訳することもできます。",
                    WrappedLabelStyle);

                _pendingTranslationText = EditorGUILayout.TextArea(
                    _pendingTranslationText,
                    GUILayout.Height(LineAreaHeight(_pendingTranslationItems.Count)));

                var lineCount = _pendingTranslationText
                    .Split('\n')
                    .Count(line => !string.IsNullOrWhiteSpace(line));
                var matchStyle = new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter };
                var statusText = lineCount == _pendingTranslationItems.Count
                    ? $"<color=green>行数が一致: {lineCount}/{_pendingTranslationItems.Count}</color>"
                    : $"<color=red>行数が不一致: {lineCount}/{_pendingTranslationItems.Count}</color>";
                EditorGUILayout.LabelField(statusText, matchStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"自動翻訳 ({TranslateFromLanguage}→{TranslateToLanguage})"))
                    {
                        AutoFillTranslations();
                    }

                    using (new EditorGUI.DisabledScope(lineCount != _pendingTranslationItems.Count))
                    {
                        if (GUILayout.Button("適用"))
                        {
                            ApplyPendingTranslations();
                        }
                    }

                    if (GUILayout.Button("閉じる(適用しない)"))
                    {
                        _pendingTranslationItems.Clear();
                        _pendingTranslationText = "";
                    }
                }
            }
        }

        private static float LineAreaHeight(int lineCount)
        {
            return Mathf.Clamp(lineCount * 18f + 6f, 36f, 200f);
        }

        private void AutoFillTranslations()
        {
            var lines = _pendingTranslationText
                .Split('\n')
                .Select(line => TranslationOps.NormalizeForTranslation(line.TrimEnd('\r')))
                .ToArray();

            try
            {
                var translated = TranslationOps.Translate(lines, TranslateFromLanguage, TranslateToLanguage);
                _pendingTranslationText = string.Join("\n", translated);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VRCMST] メニュー名の自動翻訳に失敗しました: " + e.Message);
            }
        }

        private void ApplyPendingTranslations()
        {
            var newNames = _pendingTranslationText
                .Split('\n')
                .Select(line => line.TrimEnd('\r').Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (newNames.Length != _pendingTranslationItems.Count)
            {
                EditorUtility.DisplayDialog(
                    "行数の不一致",
                    $"入力された行数({newNames.Length})がメニュー項目の数({_pendingTranslationItems.Count})と一致しません。",
                    "OK");
                return;
            }

            for (var i = 0; i < _pendingTranslationItems.Count; i++)
            {
                var item = _pendingTranslationItems[i];
                if (item == null) continue;

                Undo.RecordObject(item, "Translate Menu Item Name");
                var control = item.Control;
                control.name = newNames[i];
                item.Control = control;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }

            _pendingTranslationItems.Clear();
            _pendingTranslationText = "";
        }

        private void AddItem(GameObject avatarRoot, string categoryName, DistanceFadeSection fadeSection)
        {
            var oRoot = MenuSetupSection.GetObjectCategoryRoot(avatarRoot, categoryName);
            var menuAsset = MenuSetupSection.GetMenuAsset(avatarRoot, categoryName);
            if (oRoot == null || menuAsset == null)
            {
                Debug.LogError("[VRCMST] 格納先の構造(O_/M_)が見つかりません。②で格納先を作成し直してください。");
                return;
            }

            var instance = ModularAvatarOps.InstantiatePrefabUnder(_prefab, oRoot);

            if (ApplyMeshSettingsInherit)
            {
                ModularAvatarOps.ApplySetOrInheritToMeshSettings(instance);
            }

            if (AutoApplyDistanceFade)
            {
                fadeSection.Apply(instance);
            }

            switch (_itemType)
            {
                case ItemType.Costume:
                    ModularAvatarOps.RunSetupOutfit(instance);
                    var targets = ModularAvatarOps.GetNonArmatureChildren(instance);
                    var installers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, targets, out var createdMenuItems);
                    foreach (var installer in installers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, menuAsset);
                    }

                    if (ShowTranslationSection)
                    {
                        _pendingTranslationItems.Clear();
                        _pendingTranslationItems.AddRange(createdMenuItems);
                        _pendingTranslationText = string.Join("\n", createdMenuItems.Select(item => item.Control.name));
                    }

                    break;

                case ItemType.Hairstyle:
                    var hairInstallers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, new List<GameObject> { instance }, out _);
                    foreach (var installer in hairInstallers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, menuAsset);
                    }

                    // 既存の他の髪型との排他切り替えは④セクションで設定する。
                    break;

                case ItemType.Other:
                    // 手順書通り、追加のみで以降の処理は無し。
                    break;
            }

            if (ReplaceNameWithPrefabName)
            {
                ModularAvatarOps.RenameAvatar(avatarRoot, _predictedAvatarName);
            }

            Selection.activeGameObject = instance;
            _prefab = null;
            _predictedAvatarName = "";
        }
    }
}
