# VRChat-Modification-Shortening-Tool

Modular Avatar (MA) を使ったVRChatアバター改変の定型作業を、1つのEditorWindowに集約するUnityエディタ拡張です。`いつもの改変手順.md` にまとめられている「メニュー構築 → 衣装/髪型の追加 → トグル化 → 距離フェード一括適用 → 髪型の排他切り替え」という毎回手作業で行う流れを、ボタン操作で済ませられるようにします。

## 前提条件

- Unity 2022.3
- [VRChat SDK3 Avatars](https://vrchat.com/home/download) `>=3.5.0`
- [Modular Avatar](https://modular-avatar.nadena.dev/) `>=1.10.0`

## インストール

https://alicealiciabb.github.io/VRChat-Modification-Shortening-Tool/index.json
をvccか、ALCOMのVPMリポジトリに追加する！
VRChat Modification Shortening Tool　をプロジェクトに追加！！！！

導入後、Unityメニューの **ALICILIA > VRChat改変ショートカット** からウィンドウを開けます！！！！！

↓堅い説明
-----------------------------------------------
UPMパッケージ `Packages/com.aliciabb.vrc-mod-shortcut` として提供しています。VCC (VRChat Creator Companion) 管理下のプロジェクトであれば、このフォルダをパッケージとして追加してください(`vpmDependencies` に Modular Avatar と VRCSDK3 Avatars を指定済み)。

導入後、Unityメニューの **ALICILIA > VRChat改変ショートカット** からウィンドウを開けます。

## 使い方

ウィンドウ上部で対象アバター(`VRCAvatarDescriptor`)を指定します。Hierarchyで選択中のアバターがあれば「現在選択中のアバターを改変する」、元データを残したい場合は「複製して改変する」(Pipeline Managerのblueprint IDを外した複製を作成)で設定できます。

以降のセクションは番号順に行うことを想定しています。

1. **① メニュー初期セットアップ** — アバター直下に `M_menuObj` を作成します。
2. **② 格納先作成** — カテゴリ名(例: 衣装、髪型、アクセサリ)を入力して、アバター直下に `O_<name>`(オブジェクト格納先)、`M_menuObj` 配下に `M_<name>`(サブメニュー、実体の `VRCExpressionsMenu` アセットは `Assets/VMST/GeneratedMenus` に生成)を作成します。
3. **③ アイテム追加** — 格納先を選んでプレハブをドロップし、「メニュー作成タイプ」(衣装/髪型/作成しない)を選んで「追加」を押します。
   - **衣装**: `O_<name>` 配下にプレハブをインスタンス化 → Modular Avatarの `SetupOutfit` を実行 → Armature以外の子オブジェクトそれぞれにトグルを作成(複数あれば「`<衣装名> Toggles`」サブメニューにまとめる)→ 生成したインストーラーを `M_<name>` の実体メニューに接続。
   - **髪型**: インスタンス全体に単独トグルを作成して `M_<name>` に接続します。既存の他の髪型との排他切り替えは④で設定します。
   - **作成しない**: 配置のみ行います。
   - 追加オプションとして、プレハブ割り当て時にアバター名へ反映する機能と、追加後にメニュー名の翻訳区画(Google翻訳の無料エンドポイントを使った自動翻訳、または外部サービスへのコピペ用)を表示する機能があります。
4. **④ 髪型トグルの排他グループ化**(③で「髪型」を選んだ場合に表示) — 格納先内/アバター全体から既存のトグル済みオブジェクト(EditorOnlyは除外)を検出し、選択した2つ以上に同一パラメータ名+連番のvalueを割り当てて、VRC Expression Menu上で排他選択になるようにします。
5. **⑤ 詳細設定 / ⑥ 距離フェード一括適用** — 選択オブジェクト以下のlilToonマテリアルに対し、`_UseDistanceFade` / `_DistanceFadeColor` / `_DistanceFade` を一括設定します。③のアイテム追加時に自動適用するかどうかも切り替えられます。

## 既知の制約

- ④の排他グループ化は、MAの公式ドキュメントに記載のない挙動(同一パラメータ名+異なるvalueによる排他選択)に依存しています。設定後は実際にビルドしてVRChat上での動作確認を推奨します。
- メニュー名の自動翻訳はGoogle翻訳の非公式エンドポイント(`translate.googleapis.com`、APIキー不要)を使用しているため、将来動作しなくなる可能性があります。
- 「Create Toggle for Selection」相当の処理はMA本体が `internal` 実装のため、`ModularAvatarOps.cs` 内で同等の処理を独自に再実装しています。

## ライセンス

MIT License (`Packages/com.aliciabb.vrc-mod-shortcut/LICENSE` 参照)
