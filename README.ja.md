# Hamon

[![NuGet](https://img.shields.io/nuget/vpre/Hamon.svg)](https://www.nuget.org/packages/Hamon)

C#向けのリアクティブな宣言的UIライブラリです。特に、MonoGame向けライブラリをファーストターゲットとして開発しています。**UI = f(state)**。

状態変化に追従しながら、ゲームのホットパスで毎フレームのヒープアロケーションを避けることを目指しています。コアは描画エンジンから独立しており、MonoGameバックエンドを同梱しています。

## 特徴

- 宣言的なretained UI（Widget → Element → Render）
- 状態変化時だけの差分再構築。定常フレームのアロケーションはほぼゼロで、CI が1フレームあたりのアロケーション予算（例：単純なウィジェットで512B未満、フルスクリーンのダッシュボードで4KB未満）を守っているかを検証しています
- Flexboxサブセットのレイアウトと絶対配置アンカー
- タッチ、マウス、キーボード、ゲームパッド、フォーカス移動への対応
- 日本語を含むTTF動的グリフアトラスによるテキスト描画
- グラデーション、ソフトシャドウ、線、円、弧、回転などの描画プリミティブ
- Scaffold、フォーム、ナビゲーション、ダイアログ、DatePicker、プログレス表示などの高レベルウィジェット
- `SceneView`によるゲーム世界のUIツリーへの埋め込み
- DesktopGLおよびKNI/Web向けの移植可能なコア設計

## はじめに

MonoGameバックエンド一式をインストールします（pre-1.0リリースではバージョンを明示してください）。

```shell
dotnet add package Hamon.MonoGame --version 0.1.0-alpha.1
```

エンジン非依存で組み込む場合は`Hamon`を、FontStashSharpテキストバックエンドだけが必要な場合は`Hamon.Fonts`を利用できます。pre-1.0の間は、マイナーバージョン間で破壊的変更が入る可能性があります。

```csharp
using Hamon.MonoGame;

using var game = new HamonGame(new CounterApp());
game.Run();
```

サンプルの起動方法や詳細な構成は、英語版の[README.md](README.md)を参照してください。

## ライセンス

Hamonは[MPL 2.0](LICENSE)でライセンスされています。同梱フォントNoto Sans JPはSIL Open Font License 1.1です。詳細は[第三者ライセンス表記](THIRD-PARTY-NOTICES.ja.md)を参照してください。
