using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Vrcmst
{
    // いつもの改変手順.md の定型作業を1つのウィンドウに集約するエントリポイント。
    public class MainWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avatar;
        private Vector2 _scroll;

        private readonly MenuSetupSection _menuSetupSection = new MenuSetupSection();
        private readonly CostumeSection _costumeSection = new CostumeSection();
        private readonly HairstyleSection _hairstyleSection = new HairstyleSection();
        private readonly DistanceFadeSection _distanceFadeSection = new DistanceFadeSection();

        [MenuItem("ALICILIA/VRChat改変ショートカット")]
        public static void ShowWindow()
        {
            GetWindow<MainWindow>("改変ショートカット");
        }

        private void OnGUI()
        {
            DrawAvatarField();

            var avatarRoot = _avatar != null ? _avatar.gameObject : null;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space();
            _menuSetupSection.DrawGUI(avatarRoot);

            DrawSeparator();
            _costumeSection.DrawGUI(avatarRoot, _distanceFadeSection);

            DrawSeparator();
            _hairstyleSection.DrawGUI(avatarRoot);

            DrawSeparator();
            _distanceFadeSection.DrawGUI();

            EditorGUILayout.EndScrollView();
        }

        private void DrawAvatarField()
        {
            EditorGUILayout.LabelField("対象アバター", EditorStyles.boldLabel);
            _avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", _avatar, typeof(VRCAvatarDescriptor), true);

            if (_avatar == null && Selection.activeGameObject != null)
            {
                var fromSelection = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
                if (fromSelection != null && GUILayout.Button($"選択中のオブジェクトから自動設定 ({fromSelection.gameObject.name})"))
                {
                    _avatar = fromSelection;
                }
            }
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space();
            GUILayout.Box(GUIContent.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUILayout.Space();
        }
    }
}
