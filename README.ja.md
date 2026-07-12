# Hamon

C#向けのリアクティブな宣言的UIライブラリです。**UI = f(state)**。

状態変化に追従しながら、ゲームのホットパスで毎フレームのヒープアロケーションを避けることを目指しています。コアは描画エンジンから独立しており、MonoGameバックエンドを同梱しています。

## 特徴

- 宣言的なretained UI（Widget → Element → Render）
- 状態変化時だけの差分再構築と、定常フレームのゼロアロケーション描画
- Flexboxサブセットのレイアウトと絶対配置アンカー
- タッチ、マウス、キーボード、ゲームパッド、フォーカス移動への対応
- 日本語を含むTTF動的グリフアトラスによるテキスト描画
- グラデーション、ソフトシャドウ、線、円、弧、回転などの描画プリミティブ
- Scaffold、フォーム、ナビゲーション、ダイアログ、DatePicker、プログレス表示などの高レベルウィジェット
- `SceneView`によるゲーム世界のUIツリーへの埋め込み
- DesktopGLおよびKNI/Web向けの移植可能なコア設計

## はじめに

```csharp
using Hamon.MonoGame;

using var game = new HamonGame(new CounterApp());
game.Run();
```

サンプルの起動方法や詳細な構成は、英語版の[README.md](README.md)を参照してください。

## ライセンス

Hamonは[MPL 2.0](LICENSE)でライセンスされています。同梱フォントNoto Sans JPはSIL Open Font License 1.1です。詳細は[第三者ライセンス表記](THIRD-PARTY-NOTICES.ja.md)を参照してください。
