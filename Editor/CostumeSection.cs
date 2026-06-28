using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // 手順書の「③アイテム追加」に対応。プレハブをカテゴリへ追加し、
    // メニュー作成タイプ(衣装/髪型/その他)に応じて以降の処理を分岐する。
    internal class CostumeSection
    {
        private enum ItemType
        {
            Costume,
            Hairstyle,
            Other,
        }

        private GameObject _prefab;
        private ItemType _itemType = ItemType.Costume;
        private int _categoryIndex;

        public void DrawGUI(GameObject avatarRoot, DistanceFadeSection fadeSection)
        {
            EditorGUILayout.LabelField("③ アイテム追加 (プレハブ → カテゴリ)", EditorStyles.boldLabel);

            var categories = MenuSetupSection.GetCategoryNames(avatarRoot);
            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox("先に②でカテゴリを作成してください。", MessageType.Info);
                return;
            }

            _categoryIndex = Mathf.Clamp(_categoryIndex, 0, categories.Count - 1);
            _categoryIndex = EditorGUILayout.Popup("カテゴリ", _categoryIndex, categories.ToArray());
            var categoryName = categories[_categoryIndex];

            _prefab = (GameObject)EditorGUILayout.ObjectField("プレハブ", _prefab, typeof(GameObject), false);
            _itemType = (ItemType)EditorGUILayout.EnumPopup("メニュー作成タイプ", _itemType);

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
            var mRoot = MenuSetupSection.GetMenuCategoryRoot(avatarRoot, categoryName);
            if (oRoot == null || mRoot == null)
            {
                Debug.LogError("[VRCMST] カテゴリの構造(O_/M_)が見つかりません。②でカテゴリを作成し直してください。");
                return;
            }

            var instance = ModularAvatarOps.InstantiatePrefabUnder(_prefab, oRoot);
            fadeSection.Apply(instance);

            switch (_itemType)
            {
                case ItemType.Costume:
                    ModularAvatarOps.RunSetupOutfit(instance);
                    var targets = ModularAvatarOps.GetNonArmatureChildren(instance);
                    var installers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, targets);
                    foreach (var installer in installers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, mRoot);
                    }

                    // メニューの翻訳は手順書でも「検討のみ・実装は後回し」とされているため未対応(TODO)。
                    break;

                case ItemType.Hairstyle:
                    var hairInstallers = ModularAvatarOps.CreateTogglesForSelection(avatarRoot, new List<GameObject> { instance });
                    foreach (var installer in hairInstallers)
                    {
                        ModularAvatarOps.WireInstallerToCategoryMenu(installer, mRoot);
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
