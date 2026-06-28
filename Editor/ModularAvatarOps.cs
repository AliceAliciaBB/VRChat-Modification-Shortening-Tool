using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace Vrcmst
{
    // Modular Avatarのpublic Runtime/Editor APIだけを叩く共通処理。
    // 「Create Toggle for Selection」「Install Targetの設定」はMA本体側がinternal実装のため、
    // MA本体(ToggleCreatorShortcut.cs / MenuInstallerEditor.cs)と同じ手順を直接GameObject操作で再現している。
    internal static class ModularAvatarOps
    {
        private const string ArmatureObjectName = "Armature";

        public static GameObject EnsureMenuObjRoot(GameObject avatarRoot)
        {
            return FindOrCreateChild(avatarRoot, "M_menuObj");
        }

        public static GameObject EnsureObjectCategoryRoot(GameObject avatarRoot, string categoryName)
        {
            return FindOrCreateChild(avatarRoot, "O_" + categoryName);
        }

        public static GameObject EnsureMenuCategoryRoot(GameObject menuObjRoot, string categoryName)
        {
            var objName = "M_" + categoryName;
            var existing = menuObjRoot.transform.Find(objName);
            if (existing != null) return existing.gameObject;

            var obj = new GameObject(objName);
            Undo.RegisterCreatedObjectUndo(obj, "Create Menu Category");
            obj.transform.SetParent(menuObjRoot.transform, false);

            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.InitSettings();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = categoryName,
            };
            menuItem.MenuSource = SubmenuSource.Children;

            var installer = obj.AddComponent<ModularAvatarMenuInstaller>();
            installer.installTargetMenu = null; // ルート(アバター直下)メニューへインストール

            return obj;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            var existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            var obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            obj.transform.SetParent(parent.transform, false);
            return obj;
        }

        public static GameObject InstantiatePrefabUnder(GameObject prefabAsset, GameObject parent)
        {
            GameObject instance;
            if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.NotAPrefab)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            }
            else
            {
                instance = Object.Instantiate(prefabAsset);
                instance.name = prefabAsset.name;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Instantiate " + prefabAsset.name);
            instance.transform.SetParent(parent.transform, false);
            return instance;
        }

        public static void RunSetupOutfit(GameObject outfitRoot)
        {
            SetupOutfit.SetupOutfitUI(outfitRoot);
        }

        // SetupOutfit実行後、衣装ルート直下からArmature(MergeArmatureが付与された/名前が"Armature"の)を除いた子を返す
        public static List<GameObject> GetNonArmatureChildren(GameObject outfitRoot)
        {
            var result = new List<GameObject>();
            foreach (Transform child in outfitRoot.transform)
            {
                if (child.GetComponent<ModularAvatarMergeArmature>() != null) continue;
                if (child.name == ArmatureObjectName) continue;
                result.Add(child.gameObject);
            }

            return result;
        }

        // MA本体のToggleCreatorShortcut.CreateToggleForSelectionと同じ方針:
        // 対象が複数なら "<親名> Toggles" サブメニューを作って配下にインストーラー無しのトグルを並べる。
        // 対象が1つなら、その場に単独のトグル+インストーラーを作る。
        public static List<ModularAvatarMenuInstaller> CreateTogglesForSelection(GameObject avatarRoot, IReadOnlyList<GameObject> targets)
        {
            var installers = new List<ModularAvatarMenuInstaller>();
            if (targets == null || targets.Count == 0) return installers;

            var parent = targets[0].transform.parent;
            if (parent == null) return installers;

            if (targets.Count > 1)
            {
                var submenuName = parent.gameObject.name + " Toggles";
                var submenuObj = new GameObject(submenuName);
                Undo.RegisterCreatedObjectUndo(submenuObj, "Create Toggle Submenu");
                submenuObj.transform.SetParent(parent, false);

                var submenuItem = submenuObj.AddComponent<ModularAvatarMenuItem>();
                submenuItem.InitSettings();
                submenuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    name = submenuName,
                };
                submenuItem.MenuSource = SubmenuSource.Children;

                var installer = submenuObj.AddComponent<ModularAvatarMenuInstaller>();
                installers.Add(installer);

                foreach (var target in targets)
                {
                    CreateSingleToggle(avatarRoot, target, submenuObj, false);
                }
            }
            else
            {
                var installer = CreateSingleToggle(avatarRoot, targets[0], parent.gameObject, true);
                if (installer != null) installers.Add(installer);
            }

            return installers;
        }

        private static ModularAvatarMenuInstaller CreateSingleToggle(GameObject avatarRoot, GameObject target, GameObject parent, bool createInstaller)
        {
            var name = $"{target.name} {(target.activeSelf ? "OFF" : "ON")}";
            var toggle = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(toggle, "Create Toggle");
            toggle.transform.SetParent(parent.transform, false);

            var objToggle = toggle.AddComponent<ModularAvatarObjectToggle>();
            var path = RuntimeUtil.RelativePath(avatarRoot, target);
            objToggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference { referencePath = path },
                Active = !target.activeSelf,
            });

            var menuItem = toggle.AddComponent<ModularAvatarMenuItem>();
            menuItem.InitSettings();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = name,
                value = 1,
            };

            if (!createInstaller) return null;
            return toggle.AddComponent<ModularAvatarMenuInstaller>();
        }

        // MA本体のMenuInstallerEditor.OpenSelectMenuの"MenuNodesUnder"分岐と同じ手順:
        // installTargetMenuはnullのままにし、categoryMenuRoot配下にModularAvatarMenuInstallTargetを作って紐付ける。
        public static void WireInstallerToCategoryMenu(ModularAvatarMenuInstaller installer, GameObject categoryMenuRoot)
        {
            if (installer == null || categoryMenuRoot == null) return;

            Undo.RecordObject(installer, "Set Install Target");
            installer.installTargetMenu = null;

            var child = new GameObject(installer.gameObject.name);
            Undo.RegisterCreatedObjectUndo(child, "Set Install Target");
            child.transform.SetParent(categoryMenuRoot.transform, false);

            var targetComponent = child.AddComponent<ModularAvatarMenuInstallTarget>();
            targetComponent.installer = installer;
        }

        // 排他グループ化の対象候補: Toggleタイプ + ObjectToggleを持ち、EditorOnlyでないオブジェクト
        public static List<GameObject> FindToggleCandidates(GameObject scopeRoot)
        {
            var result = new List<GameObject>();
            var menuItems = scopeRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true);
            foreach (var item in menuItems)
            {
                if (item.gameObject.CompareTag("EditorOnly")) continue;
                if (item.Control.type != VRCExpressionsMenu.Control.ControlType.Toggle) continue;
                if (item.GetComponent<ModularAvatarObjectToggle>() == null) continue;
                result.Add(item.gameObject);
            }

            return result;
        }

        // 複数トグルに同じparameter名+異なるvalueを割り当て、VRC Expression Menuの排他選択仕様に乗せる
        public static void SetExclusiveGroup(IReadOnlyList<GameObject> toggles, string parameterName)
        {
            for (var i = 0; i < toggles.Count; i++)
            {
                var item = toggles[i].GetComponent<ModularAvatarMenuItem>();
                if (item == null) continue;

                Undo.RecordObject(item, "Set Exclusive Group");

                var control = item.Control;
                control.parameter = new VRCExpressionsMenu.Control.Parameter { name = parameterName };
                control.value = i + 1;
                item.Control = control;
                item.automaticValue = false;

                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }
    }
}
