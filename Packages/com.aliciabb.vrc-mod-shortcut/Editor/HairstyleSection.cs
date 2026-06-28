using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // 手順書の「④ 髪型: 既存の他の髪型との切り替えを提供する」に対応。
    // 既存のトグル済みオブジェクト(EditorOnlyは除外)を検出し、
    // 同一parameter名+異なるvalueを割り当てることでVRC Expression Menu上で排他選択にする。
    internal class HairstyleSection
    {
        private enum Scope
        {
            Category,
            WholeAvatar,
        }

        private static readonly string[] ScopeLabels = { "カテゴリ内", "アバター全体" };
        private static readonly Scope[] ScopeValues = { Scope.Category, Scope.WholeAvatar };

        private Scope _scope = Scope.Category;
        private int _categoryIndex;
        private string _parameterName = "";
        private readonly HashSet<GameObject> _selected = new HashSet<GameObject>();

        public void DrawGUI(GameObject avatarRoot)
        {
            EditorGUILayout.LabelField("④ 髪型トグルの排他グループ化", EditorStyles.boldLabel);

            if (avatarRoot == null)
            {
                EditorGUILayout.HelpBox("対象アバターを指定してください。", MessageType.Info);
                return;
            }

            var scopeIndex = System.Array.IndexOf(ScopeValues, _scope);
            scopeIndex = EditorGUILayout.Popup("検索範囲", scopeIndex, ScopeLabels);
            _scope = ScopeValues[scopeIndex];

            var scopeRoot = avatarRoot;
            var defaultParamName = "Hairstyle";

            if (_scope == Scope.Category)
            {
                var categories = MenuSetupSection.GetCategoryNames(avatarRoot);
                if (categories.Count == 0)
                {
                    EditorGUILayout.HelpBox("先に②でカテゴリを作成してください。", MessageType.Info);
                    return;
                }

                _categoryIndex = Mathf.Clamp(_categoryIndex, 0, categories.Count - 1);
                _categoryIndex = EditorGUILayout.Popup("カテゴリ", _categoryIndex, categories.ToArray());
                var categoryName = categories[_categoryIndex];
                scopeRoot = MenuSetupSection.GetObjectCategoryRoot(avatarRoot, categoryName);
                defaultParamName = categoryName;
            }

            if (scopeRoot == null) return;

            var candidates = ModularAvatarOps.FindToggleCandidates(scopeRoot);
            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("トグル化済みのオブジェクトが見つかりません(EditorOnlyは除外されます)。", MessageType.Info);
                _selected.Clear();
                return;
            }

            _selected.RemoveWhere(go => go == null || !candidates.Contains(go));

            foreach (var candidate in candidates)
            {
                var isSelected = _selected.Contains(candidate);
                var newValue = EditorGUILayout.ToggleLeft(candidate.name, isSelected);
                if (newValue && !isSelected) _selected.Add(candidate);
                else if (!newValue && isSelected) _selected.Remove(candidate);
            }

            if (string.IsNullOrEmpty(_parameterName))
            {
                _parameterName = defaultParamName;
            }

            _parameterName = EditorGUILayout.TextField("共有パラメータ名", _parameterName);

            using (new EditorGUI.DisabledScope(_selected.Count < 2 || string.IsNullOrWhiteSpace(_parameterName)))
            {
                if (GUILayout.Button("排他グループとして設定"))
                {
                    ModularAvatarOps.SetExclusiveGroup(new List<GameObject>(_selected), _parameterName);
                    _selected.Clear();
                }
            }

            EditorGUILayout.HelpBox(
                "この機能はMAの非公開挙動(同一parameter名+異なるvalueによる排他選択)に依存します。設定後は実際にビルドしてVRChat上での動作を確認してください。",
                MessageType.Warning);
        }
    }
}
