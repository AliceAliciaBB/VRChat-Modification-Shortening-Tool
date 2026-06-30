using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Vrcmst
{
    // 手順書の「③アイテム追加」に対応。プレハブを格納先へ追加し、
    // メニュー作成タイプ(衣装/髪型/プレハブ全体のオン/オフのみ/作成しない)に応じて以降の処理を分岐する。
    internal class CostumeSection
    {
        private enum ItemType
        {
            Costume,
            Hairstyle,
            ToggleOnly,
            Other,
        }

        private static readonly string[] ItemTypeLabels = { "衣装", "髪型", "追加するプレハブのオンオフ", "作成しない" };
        private static readonly ItemType[] ItemTypeValues = { ItemType.Costume, ItemType.Hairstyle, ItemType.ToggleOnly, ItemType.Other };

        private const string ReplaceNameWithPrefabNamePrefKey = "Vrcmst.CostumeSection.ReplaceNameWithPrefabName";
        private const string ShowTranslationSectionPrefKey = "Vrcmst.CostumeSection.ShowTranslationSection";

        private const string TranslateFromLanguage = "en";
        private const string TranslateToLanguage = "ja";

        private GameObject _prefab;
        private ItemType _itemType = ItemType.Costume;
        private int _categoryIndex;
        private bool? _replaceNameWithPrefabName;
        private bool? _showTranslationSection;
        private string _predictedAvatarName = "";

        private readonly List<ModularAvatarMenuItem> _pendingTranslationItems = new List<ModularAvatarMenuItem>();
        private string _pendingTranslationText = "";

        // ④(髪型の排他グループ化)を表示すべきか、MainWindowから参照するための公開フラグ。
        public bool IsHairstyleTypeSelected => _itemType == ItemType.Hairstyle;

        // EditorPrefsはScriptableObject(MainWindow)のフィールド初期化子から呼ぶと例外になるため、
        // 実際にGUIが描画される時点まで読み込みを遅延させる。
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
            using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
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

            using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
            {
                EditorGUILayout.LabelField("追加オプション", EditorStyles.miniBoldLabel);

                var replaceNameWithPrefabName = VrcmstStyles.WrappedToggleLeft(
                    $"プレハブ割り当て時にアバター名へ反映する(仮名_{ModularAvatarOps.DuplicateNameMarker}は置き換え、それ以外は末尾に追加)",
                    ReplaceNameWithPrefabName);
                if (replaceNameWithPrefabName != ReplaceNameWithPrefabName)
                {
                    _replaceNameWithPrefabName = replaceNameWithPrefabName;
                    EditorPrefs.SetBool(ReplaceNameWithPrefabNamePrefKey, replaceNameWithPrefabName);
                }

                if (ReplaceNameWithPrefabName && _prefab != null)
                {
                    EditorGUILayout.LabelField("変更後アバター名(予測・編集可)", VrcmstStyles.WrappedLabel);
                    _predictedAvatarName = EditorGUILayout.TextField(_predictedAvatarName);
                }

                var showTranslationSection = VrcmstStyles.WrappedToggleLeft(
                    "衣装追加後にメニュー名の翻訳区画を表示する(自動翻訳/手動編集してから適用)",
                    ShowTranslationSection);
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
            using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
            {
                EditorGUILayout.LabelField("メニュー名の翻訳", EditorStyles.miniBoldLabel);

                EditorGUILayout.LabelField("元の名前(参考・コピー用)", VrcmstStyles.WrappedLabel);
                var originalNamesText = string.Join("\n", _pendingTranslationItems.Select(item => item != null ? item.Control.name : ""));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextArea(
                        originalNamesText,
                        GUILayout.Height(EditorStyles.textArea.CalcHeight(new GUIContent(originalNamesText), EditorGUIUtility.currentViewWidth - 40f)));
                }

                EditorGUILayout.LabelField(
                    "各行が上の「元の名前」と1対1で対応します。コピーして外部の翻訳サービスに貼り付け、" +
                    "結果をここに貼り付けてから「適用」を押してください。「自動翻訳」でこのツール内から一括翻訳することもできます。",
                    VrcmstStyles.WrappedLabel);

                _pendingTranslationText = EditorGUILayout.TextArea(
                    _pendingTranslationText,
                    GUILayout.Height(EditorStyles.textArea.CalcHeight(new GUIContent(_pendingTranslationText), EditorGUIUtility.currentViewWidth - 40f)));

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

                // テキストエリアがフォーカスされていると、Unity内部の編集中キャッシュが優先されて
                // コードからの代入が画面に反映されないため、フォーカスを外して再同期させる。
                GUI.FocusControl(null);
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

                // Control.name(VRC Expression Menu上の表示名)とGameObject自体の名前(Hierarchy上の名前)は
                // 別々のフィールドなので、Hierarchy上でも変化が分かるようGameObject名も合わせて変更する。
                Undo.RecordObject(item.gameObject, "Translate Menu Item Name");
                item.gameObject.name = newNames[i];
            }

            _pendingTranslationItems.Clear();
            _pendingTranslationText = "";

            Debug.Log($"[VRCMST] メニュー名の翻訳を{newNames.Length}件適用しました。");
        }

        // プレハブにもともと付いていたギミックメニュー(MenuInstaller)が見つかった場合、
        // 自動統合はせずダイアログで確認した上でM_<categoryName>配下へ統合する。
        private static void OfferToIntegrateExistingMenuInstallers(GameObject instance, string categoryName, VRCExpressionsMenu menuAsset)
        {
            var existingInstallers = ModularAvatarOps.FindUnboundMenuInstallers(instance);
            if (existingInstallers.Count == 0) return;

            var names = string.Join("\n", existingInstallers.Select(installer => "・" + installer.gameObject.name));
            var integrate = EditorUtility.DisplayDialog(
                "既存メニューの統合",
                $"このプレハブには、もともと以下のメニューインストーラーが付いていました。\n\n{names}\n\n" +
                $"「{categoryName}」(M_{categoryName})配下に統合しますか？",
                "統合する",
                "スキップ");

            if (!integrate) return;

            foreach (var installer in existingInstallers)
            {
                ModularAvatarOps.WireInstallerToCategoryMenu(installer, menuAsset);
            }
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

            OfferToIntegrateExistingMenuInstallers(instance, categoryName, menuAsset);

            if (fadeSection.ApplyMeshSettingsInheritOnAdd)
            {
                ModularAvatarOps.ApplySetOrInheritToMeshSettings(instance);
            }

            if (fadeSection.AutoApplyOnAdd)
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

                case ItemType.ToggleOnly:
                    // SetupOutfitや子オブジェクトへの分解は行わず、追加したプレハブ全体に単独のON/OFFトグルだけを作る。
                    var toggleOnlyInstallers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, new List<GameObject> { instance }, out _);
                    foreach (var installer in toggleOnlyInstallers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, menuAsset);
                    }

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
