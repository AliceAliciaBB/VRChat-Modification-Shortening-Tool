# プロジェクト名: VRChat-Modification-Shortening-Tool

## このプロジェクトについて
- 種別: Unity (UPMパッケージ / Editor拡張)
- 開発環境: デスクトップPC / ノーパソ どちらでも開発可能(Git管理下)
- 関連する横断メモ: @C:\git\claude.global\domains\Unity.md
- 概要: Modular Avatar (MA) を使ったVRChatアバター改変の定型作業(メニュー構築・衣装/髪型セットアップ・トグル化・距離フェード一括適用・髪型排他グループ化)を1つのEditorWindowに集約するツール。加えてパッケージ移動ユーティリティ(PackageMover)も同梱。

## このプロジェクト固有のルール
- 本体は `Packages/com.aliciabb.vrc-mod-shortcut` というUPMパッケージとして実装する。リポジトリ直下ではなくこのパッケージフォルダの中にEditorスクリプトを置く。
- asmdefは `Editor/Vrcmst.asmdef` のひとつ。
- 主要ファイル構成:
  - `MainWindow.cs` — メインウィンドウ(①〜⑥のセクションをまとめる)
  - `MenuSetupSection.cs` — ①メニュー初期セットアップ
  - `CostumeSection.cs` — ③アイテム追加(衣装/髪型/作成しない)
  - `HairstyleSection.cs` — ④髪型トグルの排他グループ化
  - `DistanceFadeSection.cs` — ⑤⑥距離フェード一括適用
  - `ColorChangeWindow.cs` — マテリアルチェンジ系ウィンドウ
  - `ModularAvatarOps.cs` — MA本体がinternal実装のため独自に再実装した処理(Create Toggle for Selection相当など)
  - `TranslationOps.cs` — メニュー名自動翻訳(Google翻訳非公式エンドポイント使用)
  - `VrcmstStyles.cs` — GUIStyle共通定義(薄文字色は白(0.8,0.8,0.8)/黒(0.2,0.2,0.2)の80%ルール)
  - `Editor/PackageMover/` — パッケージ・フォルダ移動ユーティリティ(`PackageMoverWindow.cs`, `FolderMoveWindow.cs`, `PackageMoverFileOps.cs`(共通処理), `PackageMoverConfig.cs`, `PackageMoverWatcher.cs`, `destinations.json`)
- Unityメニューは **ALICILIA** 配下にまとまる(VRChat改変ショートカット、PackageMover系)。
- `Website/` はVPMリポジトリ用の静的ページ(GitHub Pages, `index.json` 経由でVCC/ALCOMに登録)。READMEのインストール手順と対応しているので、配布URLや手順を変える場合はここも更新する。
- ルート直下の `いつもの改変手順.md` がこのツールが自動化しようとしている手作業フローの原典。仕様変更時はここと実装の対応関係を確認する。
- `改修予定.md` に進行中/予定の改修メモ(PackageMoverの移動ルール例など)がある。実装前に確認する。

## ビルド・実行コマンド
- Unityプロジェクト本体ではなくUnityプロジェクト内のサブフォルダ(`Assets/VRChat-Modification-Shortening-Tool`)としてGit管理されている。ビルド・コンパイルはUnityエディタ側で行われるため、専用のCLIビルドコマンドはなし。
- 動作確認はUnityエディタを開いた状態で **ALICILIA** メニューからウィンドウを開いて手動テストする。

## 注意が必要な箇所
- ④の排他グループ化はMA公式ドキュメントに記載のない挙動(同一パラメータ名+異なるvalueによる排他選択)に依存している。変更後は実機(VRChatアップロード)での動作確認を推奨。
- `ModularAvatarOps.cs` はMAのinternal実装を独自再現しているため、MAのバージョンアップで挙動が変わる可能性がある。
- `TranslationOps.cs` の自動翻訳は非公式エンドポイントのため、将来動作しなくなる可能性がある。
- IMGUIのテキストエリアの高さはpx固定値ではなく `GUIStyle.CalcHeight` を使う([[feedback_imgui_sizing]])。
- 薄文字色は白(0.8,0.8,0.8)/黒(0.2,0.2,0.2)が限界([[feedback_muted_text_color]])。

---

## 同じ失敗を繰り返さないためのメモ
記録形式はグローバルCLAUDE.mdの「記録フォーマット」に従う。
pain_count が3に達したものは、プロジェクト固有のままで良いか、
Unity.mdなど横断メモへ昇格すべきか判断する。

(まだ記録なし。横断メモ側に [[feedback_imgui_sizing]] / [[feedback_muted_text_color]] が既存)

## うまくいった進め方
success_count が概ね20に達したら、スラッシュコマンド化や
横断メモへの昇格を検討する。

(まだ記録なし)

---

## Git運用メモ
- このプロジェクトはGit管理: はい
- 更新後は必ずコミット・プッシュを行う
- デスクトップPCとノーパソ間で作業を行き来する場合、
  作業開始前に必ず `git pull`、作業終了後に必ず `git push` を行う
