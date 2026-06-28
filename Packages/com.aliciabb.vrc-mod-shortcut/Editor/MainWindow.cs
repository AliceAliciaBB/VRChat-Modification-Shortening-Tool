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
            Debug.Log("[VRCMST] ShowWindow() 呼び出し開始");
            try
            {
                var window = GetWindow<MainWindow>("改変ショートカット");
                Debug.Log($"[VRCMST] GetWindow完了: window={(window != null ? window.ToString() : "null")}, instanceID={(window != null ? window.GetInstanceID() : 0)}");

                window.minSize = new Vector2(420, 500);
                // 以前のレイアウト保存が壊れている(幅/高さが極端に小さい)場合は既定サイズに戻す。
                if (window.position.width < 100 || window.position.height < 100)
                {
                    window.position = new Rect(100, 100, 600, 700);
                }

                window.Show();
                window.Focus();
                Debug.Log($"[VRCMST] Show()/Focus()完了: position={window.position}");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VRCMST] ShowWindow()で例外発生: " + e);
            }
        }

        private void OnEnable()
        {
            Debug.Log("[VRCMST] MainWindow.OnEnable instanceID=" + GetInstanceID());
        }

        private void OnDisable()
        {
            Debug.Log("[VRCMST] MainWindow.OnDisable instanceID=" + GetInstanceID());
        }

        private void OnDestroy()
        {
            Debug.Log("[VRCMST] MainWindow.OnDestroy instanceID=" + GetInstanceID());
        }

        private void OnGUI()
        {
            try
            {
                DrawAvatarField();

                var avatarRoot = _avatar != null ? _avatar.gameObject : null;

                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                EditorGUILayout.Space();
                _menuSetupSection.DrawGUI(avatarRoot);

                DrawSeparator();
                _costumeSection.DrawGUI(avatarRoot, _distanceFadeSection);

                if (_costumeSection.IsHairstyleTypeSelected)
                {
                    DrawSeparator();
                    _hairstyleSection.DrawGUI(avatarRoot);
                }

                DrawSeparator();
                _distanceFadeSection.DrawGUI();

                EditorGUILayout.EndScrollView();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[VRCMST] OnGUI()で例外発生: " + e);
                throw;
            }
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
