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
        private const string ReplaceNameWithPrefabNamePrefKey = "Vrcmst.CostumeSection.ReplaceNameWithPrefabName";

        private GameObject _prefab;
        private ItemType _itemType = ItemType.Costume;
        private int _categoryIndex;
        private bool? _autoApplyDistanceFade;
        private bool? _replaceNameWithPrefabName;

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

        public void DrawGUI(GameObject avatarRoot, DistanceFadeSection fadeSection)
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

            var previousPrefab = _prefab;
            _prefab = (GameObject)EditorGUILayout.ObjectField("プレハブ", _prefab, typeof(GameObject), false);
            if (_prefab != null && _prefab != previousPrefab && avatarRoot != null && ReplaceNameWithPrefabName)
            {
                ModularAvatarOps.ApplyPrefabNameToAvatarName(avatarRoot, _prefab.name);
            }

            var itemTypeIndex = System.Array.IndexOf(ItemTypeValues, _itemType);
            itemTypeIndex = EditorGUILayout.Popup("メニュー作成タイプ", itemTypeIndex, ItemTypeLabels);
            _itemType = ItemTypeValues[itemTypeIndex];

            var autoApplyDistanceFade = EditorGUILayout.ToggleLeft("追加時に距離フェードを一括適用", AutoApplyDistanceFade);
            if (autoApplyDistanceFade != AutoApplyDistanceFade)
            {
                _autoApplyDistanceFade = autoApplyDistanceFade;
                EditorPrefs.SetBool(AutoApplyDistanceFadePrefKey, autoApplyDistanceFade);
            }

            var replaceNameWithPrefabName = EditorGUILayout.ToggleLeft(
                $"プレハブ割り当て時にアバター名へ反映する(仮名_{ModularAvatarOps.DuplicateNameMarker}は置き換え、それ以外は末尾に追加)",
                ReplaceNameWithPrefabName);
            if (replaceNameWithPrefabName != ReplaceNameWithPrefabName)
            {
                _replaceNameWithPrefabName = replaceNameWithPrefabName;
                EditorPrefs.SetBool(ReplaceNameWithPrefabNamePrefKey, replaceNameWithPrefabName);
            }

            using (new EditorGUI.DisabledScope(_prefab == null))
            {
                if (GUILayout.Button("追加"))
                {
                    AddItem(avatarRoot, categoryName, fadeSection);
                }
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
