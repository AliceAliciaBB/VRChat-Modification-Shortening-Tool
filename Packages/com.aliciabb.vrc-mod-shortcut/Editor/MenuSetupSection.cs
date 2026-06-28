using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Vrcmst
{
    // 手順書の「①メニュー初期セットアップ」「②格納先作成」に対応するUIと、
    // 他セクションが格納先構造(O_<name> / M_<name>)を参照するための静的ヘルパー。
    internal class MenuSetupSection
    {
        private string _categoryName = "";

        public void DrawGUI(GameObject avatarRoot)
        {
            EditorGUILayout.LabelField("① メニュー初期セットアップ", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
            {
                if (GetMenuObjRoot(avatarRoot) == null)
                {
                    using (new EditorGUI.DisabledScope(avatarRoot == null))
                    {
                        if (GUILayout.Button("M_menuObj を用意"))
                        {
                            ModularAvatarOps.EnsureMenuObjRoot(avatarRoot);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("M_menuObj は作成済みです。", VrcmstStyles.WrappedMiniLabel);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("② 格納先作成 (O_<name> / M_<name>)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(VrcmstStyles.Box))
            {
                EditorGUILayout.LabelField("例) 衣装, 髪型, アクセサリ, ギミック など", VrcmstStyles.WrappedMiniLabel);

                _categoryName = EditorGUILayout.TextField("格納先名", _categoryName);

                using (new EditorGUI.DisabledScope(avatarRoot == null || string.IsNullOrWhiteSpace(_categoryName)))
                {
                    if (GUILayout.Button("格納先を作成"))
                    {
                        var menuObjRoot = ModularAvatarOps.EnsureMenuObjRoot(avatarRoot);
                        ModularAvatarOps.EnsureObjectCategoryRoot(avatarRoot, _categoryName);
                        ModularAvatarOps.EnsureMenuCategoryRoot(avatarRoot, menuObjRoot, _categoryName);
                        _categoryName = "";
                    }
                }
            }
        }

        public static List<string> GetCategoryNames(GameObject avatarRoot)
        {
            var result = new List<string>();
            if (avatarRoot == null) return result;

            foreach (Transform child in avatarRoot.transform)
            {
                if (child.name.StartsWith("O_"))
                {
                    result.Add(child.name.Substring(2));
                }
            }

            return result;
        }

        public static GameObject GetMenuObjRoot(GameObject avatarRoot)
        {
            if (avatarRoot == null) return null;
            var found = avatarRoot.transform.Find("M_menuObj");
            return found != null ? found.gameObject : null;
        }

        public static GameObject GetObjectCategoryRoot(GameObject avatarRoot, string categoryName)
        {
            if (avatarRoot == null) return null;
            var found = avatarRoot.transform.Find("O_" + categoryName);
            return found != null ? found.gameObject : null;
        }

        public static GameObject GetMenuCategoryRoot(GameObject avatarRoot, string categoryName)
        {
            var menuObjRoot = GetMenuObjRoot(avatarRoot);
            if (menuObjRoot == null) return null;
            var found = menuObjRoot.transform.Find("M_" + categoryName);
            return found != null ? found.gameObject : null;
        }

        public static VRCExpressionsMenu GetMenuAsset(GameObject avatarRoot, string categoryName)
        {
            var menuCategoryRoot = GetMenuCategoryRoot(avatarRoot, categoryName);
            return menuCategoryRoot != null ? ModularAvatarOps.GetCategoryMenuAsset(menuCategoryRoot) : null;
        }
    }
}
