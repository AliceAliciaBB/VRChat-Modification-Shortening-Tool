using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Vrcmst
{
    // Google翻訳のAPIキー不要な無料エンドポイント(client=gtx)を使ったメニュー名翻訳用の共通処理。
    // 公式APIではないため将来動かなくなる可能性がある点に留意。
    internal static class TranslationOps
    {
        private const string ApiBaseUrl = "https://translate.googleapis.com/translate_a/single";

        // "_"や"-"などの区切り文字や、camelCase/PascalCaseの単語境界をスペースに変換する。
        // "Long_Skirt"や"ShortSkirt"のような名前は翻訳APIが正しく訳せない場合があるため、
        // 翻訳前にできるだけ自然な単語の並びに近づける。
        public static string NormalizeForTranslation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var normalized = Regex.Replace(text, @"[_\-\.]+", " ");
            normalized = Regex.Replace(normalized, @"(?<=[a-z0-9])(?=[A-Z])", " ");
            return Regex.Replace(normalized, @"\s+", " ").Trim();
        }

        public static string[] Translate(string[] texts, string from, string to)
        {
            if (texts == null || texts.Length == 0) return Array.Empty<string>();

            var results = new string[texts.Length];
            for (var i = 0; i < texts.Length; i++)
            {
                results[i] = TranslateSingle(texts[i], from, to);
            }

            return results;
        }

        private static string TranslateSingle(string text, string from, string to)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var url = $"{ApiBaseUrl}?client=gtx&sl={from}&tl={to}&dt=t&q={Uri.EscapeDataString(text)}";

            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add(
                    "user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");

                var response = client.DownloadString(url);
                return ParseResponse(response) ?? text;
            }
        }

        // レスポンスは [[["訳文","元文",null,null,3], ...], null, "検出言語", ...] という不規則な配列で、
        // UnityのJsonUtilityでは型付けして扱えないため、先頭(訳文)要素を正規表現で抜き出して連結する。
        private static string ParseResponse(string json)
        {
            var matches = Regex.Matches(json, "\\[\"((?:[^\"\\\\]|\\\\.)*)\",\"(?:[^\"\\\\]|\\\\.)*\",");
            if (matches.Count == 0) return null;

            var sb = new StringBuilder();
            foreach (Match match in matches)
            {
                sb.Append(Regex.Unescape(match.Groups[1].Value));
            }

            return sb.ToString();
        }
    }
}
