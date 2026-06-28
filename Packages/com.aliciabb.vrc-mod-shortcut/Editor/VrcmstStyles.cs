using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // ツール全体で使う共通のスタイル/余白ルール。各セクションで個別に定義していたものをここに統一する。
    internal static class VrcmstStyles
    {
        public const float MinContentWidth = 200f;

        private const int WindowPaddingSize = 10;
        private const int BoxPadding = 10;
        private const int LineSpacing = 5;

        private static GUIStyle _windowPadding;
        private static GUIStyle _box;
        private static GUIStyle _wrappedLabel;
        private static GUIStyle _wrappedMiniLabel;

        // ウィンドウ全体の内側に統一した余白を持たせるためのラッパースタイル。
        public static GUIStyle WindowPadding
        {
            get
            {
                if (_windowPadding == null)
                {
                    _windowPadding = new GUIStyle { padding = new RectOffset(WindowPaddingSize, WindowPaddingSize, WindowPaddingSize, WindowPaddingSize) };
                }

                return _windowPadding;
            }
        }

        // "box"より内側の余白を広げたバージョン。
        public static GUIStyle Box
        {
            get
            {
                if (_box == null)
                {
                    _box = new GUIStyle(GUI.skin.box)
                    {
                        padding = new RectOffset(BoxPadding, BoxPadding, BoxPadding, BoxPadding),
                    };
                }

                return _box;
            }
        }

        // 折り返し対応・上下5pxの行間を持つラベル用スタイル。
        public static GUIStyle WrappedLabel
        {
            get
            {
                if (_wrappedLabel == null)
                {
                    _wrappedLabel = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        margin = new RectOffset(2, 2, LineSpacing, LineSpacing),
                    };
                }

                return _wrappedLabel;
            }
        }

        // 補足説明・例示など、小さいフォントで折り返したいテキスト用。
        public static GUIStyle WrappedMiniLabel
        {
            get
            {
                if (_wrappedMiniLabel == null)
                {
                    _wrappedMiniLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap = true,
                        margin = new RectOffset(2, 2, LineSpacing, LineSpacing),
                    };
                }

                return _wrappedMiniLabel;
            }
        }

        // EditorGUILayout.ToggleLeftはwordWrap指定のGUIStyleを渡しても折り返し後の高さを
        // 正しくレイアウトに反映しないため、スタイルを渡せるGetRectオーバーロードで
        // Unity自身に高さとmargin込みのレイアウトを計算させる。
        public static bool WrappedToggleLeft(string label, bool value)
        {
            var content = new GUIContent(label);
            var rect = GUILayoutUtility.GetRect(content, WrappedLabel, GUILayout.ExpandWidth(true));
            return EditorGUI.ToggleLeft(rect, content, value, WrappedLabel);
        }
    }
}
