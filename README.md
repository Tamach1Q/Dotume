# Dotume

Unity 製ゲームプロジェクトです。  
実際の Unity プロジェクト本体は `Dotumeゲーム/Dotumeゲーム/` にあります。

## 開発環境

- Unity: `2022.3.21f1`
- Render Pipeline: `Universal Render Pipeline`
- 主なパッケージ:
  - `com.unity.inputsystem`
  - `com.unity.textmeshpro`
  - `com.unity.cinemachine`
  - `com.unity.render-pipelines.universal`

## プロジェクトの開き方

Unity Hub から次のフォルダを開いてください。

```text
Dotumeゲーム/Dotumeゲーム
```

## フォルダ構成

```text
Dotume/
├─ .gitignore
├─ README.md
└─ Dotumeゲーム/
   └─ Dotumeゲーム/
      ├─ Assets/            # ゲーム本体のアセット、シーン、スクリプト
      ├─ Packages/          # Unity Package Manager 設定
      ├─ ProjectSettings/   # Unity プロジェクト設定
      ├─ UserSettings/      # ローカル環境依存。通常は共有しない
      ├─ Library/           # Unity 自動生成。Git 管理しない
      ├─ Logs/              # Unity 自動生成。Git 管理しない
      ├─ .vs/               # IDE 自動生成。Git 管理しない
      ├─ *.csproj / *.sln   # IDE 自動生成。Git 管理しない
      └─ *.app              # ビルド成果物。通常は Git 管理しない
```

## Assets の主な内容

- `Assets/New Scripts/`: ゲームロジック用スクリプト
- `Assets/Textures/`: 各ステージやプレイヤー用テクスチャ
- `Assets/Sounds/`: 効果音などの音声素材
- `Assets/Required Prefabs/`: 必須プレハブ
- `Assets/FieldBones/`: ステージ関連アセット
- `Assets/TextMesh Pro/`: TextMesh Pro 関連アセット

## シーン構成

`ProjectSettings/EditorBuildSettings.asset` に登録されているシーン:

1. `Assets/Title.unity`
2. `Assets/Persistent Scene.unity`
3. `Assets/Dotume-1.unity`
4. `Assets/Stage_Field2.unity`
5. `Assets/Stage_Field3.unity`
6. `Assets/Stage_Field4.unity`
7. `Assets/Stage_Field5.unity`
8. `Assets/ClearScene.unity`

補足:

- `Assets/Stage_Field1.unity` は存在しますが、現状の Build Settings には入っていません。
- `Assets/Dotume-1 1.unity` も存在しますが、Build Settings には入っていません。

## Git 管理の方針

GitHub に上げる対象は、基本的に次の 3 つです。

- `Assets/`
- `Packages/`
- `ProjectSettings/`

次のような生成物やローカル設定は通常コミットしません。

- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `.vs/`
- `*.csproj`
- `*.sln`
- `*.app`

## メモ

このリポジトリは Unity 生成物やビルド成果物が混ざりやすい構成になっているため、GitHub に push する前に `.gitignore` とステージ内容の確認を推奨します。
