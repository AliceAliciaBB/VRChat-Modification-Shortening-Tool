using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // 手順書の「③アイテム追加」に対応。プレハブを格納先へ追加し、
    // メニュー作成タイプ(衣装/髪型/その他)に応じて以降の処理を分岐する。
    internal class CostumeSection
    {
        private enum ItemType
        {
            Costume,
            Hairstyle,
            Other,
        }

        private static readonly string[] ItemTypeLabels = { "衣装", "髪型", "その他" };
        private static readonly ItemType[] ItemTypeValues = { ItemType.Costume, ItemType.Hairstyle, ItemType.Other };

        private const string AutoApplyDistanceFadePrefKey = "Vrcmst.CostumeSection.AutoApplyDistanceFade";
        private const string DuplicateBeforeAddingPrefKey = "Vrcmst.CostumeSection.DuplicateBeforeAdding";

        private GameObject _prefab;
        private ItemType _itemType = ItemType.Costume;
        private int _categoryIndex;
        private bool? _autoApplyDistanceFade;
        private bool? _duplicateBeforeAdding;

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

        private bool DuplicateBeforeAdding
        {
            get
            {
                if (_duplicateBeforeAdding == null)
                {
                    _duplicateBeforeAdding = EditorPrefs.GetBool(DuplicateBeforeAddingPrefKey, false);
                }

                return _duplicateBeforeAdding.Value;
            }
        }

        // avatarRootをコピーに差し替えた場合に、MainWindow側の対象アバター参照を更新するためのコールバック。
        public void DrawGUI(GameObject avatarRoot, DistanceFadeSection fadeSection, System.Action<GameObject> onAvatarReplaced)
        {
            EditorGUILayout.LabelField("③ アイテム追加 (プレハブ → 格納先)", EditorStyles.boldLabel);

            var categories = MenuSetupSection.GetCategoryNames(avatarRoot);
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("先に②で格納先を作成してください。", MessageType.Info);
                return;
            }

            _categoryIndex = Mathf.Clamp(_categoryIndex, 0, categories.Count - 1);
            _categoryIndex = EditorGUILayout.Popup("格納先", _categoryIndex, categories.ToArray());
            var categoryName = categories[_categoryIndex];

            _prefab = (GameObject)EditorGUILayout.ObjectField("プレハブ", _prefab, typeof(GameObject), false);

            var itemTypeIndex = System.Array.IndexOf(ItemTypeValues, _itemType);
            itemTypeIndex = EditorGUILayout.Popup("メニュー作成タイプ", itemTypeIndex, ItemTypeLabels);
            _itemType = ItemTypeValues[itemTypeIndex];

            var autoApplyDistanceFade = EditorGUILayout.ToggleLeft("追加時に距離フェードを一括適用", AutoApplyDistanceFade);
            if (autoApplyDistanceFade != AutoApplyDistanceFade)
            {
                _autoApplyDistanceFade = autoApplyDistanceFade;
                EditorPrefs.SetBool(AutoApplyDistanceFadePrefKey, autoApplyDistanceFade);
            }

            var duplicateBeforeAdding = EditorGUILayout.ToggleLeft(
                "アバターを複製してから追加する(複製先は元アバター名_プレハブ名、元アバターは非表示)",
                DuplicateBeforeAdding);
            if (duplicateBeforeAdding != DuplicateBeforeAdding)
            {
                _duplicateBeforeAdding = duplicateBeforeAdding;
                EditorPrefs.SetBool(DuplicateBeforeAddingPrefKey, duplicateBeforeAdding);
            }

            using (new EditorGUI.DisabledScope(_prefab == null))
            {
                if (GUILayout.Button("追加"))
                {
                    AddItem(avatarRoot, categoryName, fadeSection, onAvatarReplaced);
                }
            }
        }

        private void AddItem(GameObject avatarRoot, string categoryName, DistanceFadeSection fadeSection, System.Action<GameObject> onAvatarReplaced)
        {
            var oRoot = MenuSetupSection.GetObjectCategoryRoot(avatarRoot, categoryName);
            var menuAsset = MenuSetupSection.GetMenuAsset(avatarRoot, categoryName);
            if (oRoot == null || menuAsset == null)
            {
                Debug.LogError("[VRCMST] 格納先の構造(O_/M_)が見つかりません。②で格納先を作成し直してください。");
                return;
            }

            if (DuplicateBeforeAdding)
            {
                avatarRoot = ModularAvatarOps.DuplicateAvatarForModification(avatarRoot, _prefab.name);
                onAvatarReplaced?.Invoke(avatarRoot);
                oRoot = MenuSetupSection.GetObjectCategoryRoot(avatarRoot, categoryName);
                menuAsset = MenuSetupSection.GetMenuAsset(avatarRoot, categoryName);
            }

            var instance = ModularAvatarOps.InstantiatePrefabUnder(_prefab, oRoot);
            if (AutoApplyDistanceFade)
            {
                fadeSection.Apply(instance);
            }

            switch (_itemType)
            {
                case ItemType.Costume:
                    ModularAvatarOps.RunSetupOutfit(instance);
                    var targets = ModularAvatarOps.GetNonArmatureChildren(instance);
                    var installers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, targets);
                    foreach (var installer in installers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, menuAsset);
                    }

                    // メニューの翻訳は手順書でも「検討のみ・実装は後回し」とされているため未対応(TODO)。
                    break;

                case ItemType.Hairstyle:
                    var hairInstallers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, new List<GameObject> { instance });
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

            Selection.activeGameObject = instance;
            _prefab = null;
        }
    }
}
