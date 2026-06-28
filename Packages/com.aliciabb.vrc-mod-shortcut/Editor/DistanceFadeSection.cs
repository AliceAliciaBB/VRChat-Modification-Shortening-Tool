using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vrcmst
{
    // ユーザー提供のlilToon距離フェード一括適用スクリプトを移植したセクション。
    // 単独のボタン操作としても、CostumeSection等のアイテム追加フローからも呼び出す。
    internal class DistanceFadeSection
    {
        public Color FadeColor = Color.black;
        public float FadeStart = 0.15f;
        public float FadeEnd = 0.01f;
        public float Strength = 1.1f;

        public void DrawGUI()
        {
            EditorGUILayout.LabelField("距離フェード一括適用 (lilToon)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                FadeColor = EditorGUILayout.ColorField("フェードカラー", FadeColor);
                FadeStart = EditorGUILayout.FloatField("フェード開始距離", FadeStart);
                FadeEnd = EditorGUILayout.FloatField("フェード終了距離", FadeEnd);
                Strength = EditorGUILayout.FloatField("強度", Strength);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("選択オブジェクト以下に適用", GUILayout.Height(28)))
                {
                    Apply(Selection.activeGameObject);
                }
            }
        }

        public void Apply(GameObject root)
        {
            if (root == null)
            {
                Debug.LogWarning("[VRCMST] 距離フェード適用対象が選択されていません。");
                return;
            }

            var processed = new HashSet<Material>();
            var rendererCount = 0;
            var materialChangedCount = 0;

            ProcessRecursively(root.transform, processed, ref rendererCount, ref materialChangedCount);

            Debug.Log(
                "[VRCMST 距離フェード適用完了]\n" +
                $"対象オブジェクト: {root.name}\n" +
                $"処理レンダラー数: {rendererCount}\n" +
                $"変更マテリアル数: {materialChangedCount}");
        }

        private void ProcessRecursively(Transform current, HashSet<Material> processed, ref int rendererCount, ref int materialChangedCount)
        {
            var r = current.GetComponent<Renderer>();
            if (r != null)
            {
                rendererCount++;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != null && IsLilToon(mat) && !processed.Contains(mat))
                    {
                        SetDistanceFade(mat);
                        processed.Add(mat);
                        materialChangedCount++;
                    }
                }
            }

            foreach (Transform child in current)
            {
                ProcessRecursively(child, processed, ref rendererCount, ref materialChangedCount);
            }
        }

        private static bool IsLilToon(Material mat)
        {
            return mat.shader != null && mat.shader.name.ToLower().Contains("liltoon");
        }

        private void SetDistanceFade(Material mat)
        {
            if (mat.HasProperty("_UseDistanceFade"))
            {
                mat.SetFloat("_UseDistanceFade", 1f);
            }

            if (mat.HasProperty("_DistanceFadeColor"))
            {
                mat.SetColor("_DistanceFadeColor", FadeColor);
            }

            if (mat.HasProperty("_DistanceFade"))
            {
                var distFade = mat.GetVector("_DistanceFade");
                distFade.x = FadeStart;
                distFade.y = FadeEnd;
                distFade.z = Strength;
                mat.SetVector("_DistanceFade", distFade);
            }

            EditorUtility.SetDirty(mat);
        }
    }
}
