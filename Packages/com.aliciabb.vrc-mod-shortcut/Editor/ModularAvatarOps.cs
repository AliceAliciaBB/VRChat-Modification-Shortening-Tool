using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using nadena.dev.modular_avatar.core;
using nadena.dev.modular_avatar.core.editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;
using PipelineManager = VRC.Core.PipelineManager;

namespace Vrcmst
{
    // Modular Avatarのpublic Runtime/Editor APIだけを叩く共通処理。
    // 「Create Toggle for Selection」はMA本体側がinternal実装のため、
    // MA本体(ToggleCreatorShortcut.cs)と同じ手順を直接GameObject操作で再現している。
    // メニューのインストール先は、MAのinternalな ModularAvatarMenuInstallTarget には依存せず、
    // 格納先ごとに実体のVRCExpressionsMenuアセットを作って installTargetMenu に直接割り当てる方式にしている。
    internal static class ModularAvatarOps
    {
        private const string ArmatureObjectName = "Armature";
        private const string GeneratedMenuFolder = "Assets/VMST/GeneratedMenus";

        // 「複製して改変する」で付与する仮名のマーカー。後でプレハブ名に置き換えられる。
        public const string DuplicateNameMarker = "複製";
        private static readonly Regex DuplicateSuffixPattern = new Regex(@"_" + DuplicateNameMarker + @"(\s\(\d+\))?$");

        public static GameObject EnsureMenuObjRoot(GameObject avatarRoot)
        {
            return FindOrCreateChild(avatarRoot, "M_menuObj");
        }

        public static GameObject EnsureObjectCategoryRoot(GameObject avatarRoot, string categoryName)
        {
            return FindOrCreateChild(avatarRoot, "O_" + categoryName);
        }

        public static GameObject EnsureMenuCategoryRoot(GameObject avatarRoot, GameObject menuObjRoot, string categoryName)
        {
            var objName = "M_" + categoryName;
            var existing = menuObjRoot.transform.Find(objName);
            if (existing != null) return existing.gameObject;

            var obj = new GameObject(objName);
            Undo.RegisterCreatedObjectUndo(obj, "Create Menu Category");
            obj.transform.SetParent(menuObjRoot.transform, false);

            var menuAsset = CreateMenuAsset(avatarRoot.name, categoryName);

            var menuItem = obj.AddComponent<ModularAvatarMenuItem>();
            menuItem.MenuSource = SubmenuSource.MenuAsset;
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = categoryName,
                subMenu = menuAsset,
            };

            var installer = obj.AddComponent<ModularAvatarMenuInstaller>();
            installer.installTargetMenu = null; // ルート(アバター直下)メニューへインストール

            return obj;
        }

        // M_<name>のサブメニュー先として使っている実体のVRCExpressionsMenuアセットを取得する
        public static VRCExpressionsMenu GetCategoryMenuAsset(GameObject menuCategoryRoot)
        {
            var item = menuCategoryRoot?.GetComponent<ModularAvatarMenuItem>();
            return item != null ? item.Control.subMenu : null;
        }

        private static VRCExpressionsMenu CreateMenuAsset(string avatarName, string categoryName)
        {
            EnsureFolder(GeneratedMenuFolder);

            var fileName = $"{MakeSafeFileName(avatarName)}_M_{MakeSafeFileName(categoryName)}.asset";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedMenuFolder}/{fileName}");

            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(menu, path);
            AssetDatabase.SaveAssets();
            return menu;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string MakeSafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid));
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

        // 元アバターを複製し、複製先を「<元の名前>_<nameSuffix>」にリネームしてPipeline Manager(blueprintId)を
        // デタッチした上で返す。元アバターは非表示にして残す(以降の改変は複製側に対して行う)。
        public static GameObject DuplicateAvatarForModification(GameObject original, string nameSuffix)
        {
            var copy = Object.Instantiate(original);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Avatar For Modification");
            copy.transform.SetParent(original.transform.parent, false);
            copy.transform.localPosition = original.transform.localPosition;
            copy.transform.localRotation = original.transform.localRotation;
            copy.transform.localScale = original.transform.localScale;
            copy.name = GameObjectUtility.GetUniqueNameForSibling(copy.transform.parent, original.name + "_" + nameSuffix);

            Undo.RecordObject(original, "Hide Original Avatar");
            original.SetActive(false);

            var pipelineManager = copy.GetComponent<PipelineManager>();
            if (pipelineManager != null)
            {
                Undo.RecordObject(pipelineManager, "Detach Pipeline Manager");
                pipelineManager.blueprintId = "";
                PrefabUtility.RecordPrefabInstancePropertyModifications(pipelineManager);
            }

            return copy;
        }

        // 名前が「..._複製」(複製直後の仮名)に見えるかどうかを判定する。
        // 「複製して改変する」を多重実行して"_複製_複製..."になるのを防ぐための確認に使う。
        public static bool LooksLikeUnfinishedDuplicate(GameObject avatarRoot)
        {
            return avatarRoot != null && DuplicateSuffixPattern.IsMatch(avatarRoot.name);
        }

        // 「..._複製」の仮名をプレハブ名に置き換える。仮名のパターンに一致しない場合は何もしない
        // (複製を経ていない元アバターを誤ってリネームしないため)。
        public static void RenameDuplicateForPrefabAssignment(GameObject avatarRoot, string prefabName)
        {
            var match = DuplicateSuffixPattern.Match(avatarRoot.name);
            if (!match.Success) return;

            var baseName = avatarRoot.name.Substring(0, match.Index);
            var desiredName = GameObjectUtility.GetUniqueNameForSibling(avatarRoot.transform.parent, baseName + "_" + prefabName);

            Undo.RecordObject(avatarRoot, "Rename Avatar");
            avatarRoot.name = desiredName;
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
                submenuItem.MenuSource = SubmenuSource.Children;
                submenuItem.Control = new VRCExpressionsMenu.Control
                {
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    name = submenuName,
                };

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
            var path = AnimationUtility.CalculateTransformPath(target.transform, avatarRoot.transform);
            objToggle.Objects.Add(new ToggledObject
            {
                Object = new AvatarObjectReference { referencePath = path },
                Active = !target.activeSelf,
            });

            var menuItem = toggle.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = name,
                value = 1,
            };

            if (!createInstaller) return null;
            return toggle.AddComponent<ModularAvatarMenuInstaller>();
        }

        // 生成したインストーラーの設置先を、格納先(M_<name>)の実体メニューアセットに直接設定する
        public static void WireInstallerToCategoryMenu(ModularAvatarMenuInstaller installer, VRCExpressionsMenu menuAsset)
        {
            if (installer == null || menuAsset == null) return;

            Undo.RecordObject(installer, "Set Install Target");
            installer.installTargetMenu = menuAsset;
            PrefabUtility.RecordPrefabInstancePropertyModifications(installer);
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
