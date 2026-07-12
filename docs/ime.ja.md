# Hamon IME / テキスト入力

Hamon の IME（日本語等）入力は、**エンジン非依存のコア抽象**＋**プラットフォーム別バックエンド**で構成する。

## コア抽象（`Hamon`）

- `FocusNode.OnComposition(string preedit, int caret)` … 変換中テキスト（preedit）を受ける。
- `FocusNode.OnTextInput(char)` … 確定文字（commit）。
- `TextEditingController` … `Composition`/`CompositionCaret`（変換中状態。確定 `Insert` で消える）、`SetComposition`。
- `TextField` … 変換中を下線付きでインライン表示し、キャレット矩形を IME（候補ウィンドウ位置）へ報告。
- `IHamonHost.BeginTextInput()/EndTextInput()/SetTextInputCaret(Rect)` … フォーカス中フィールドが入力開始/終了・キャレット位置を通知。
- `HamonRoot.TextInput`（`ITextInput`）にバックエンドを注入。`HamonRoot.DispatchComposition`/`DispatchText` で逆向きに供給。
- `HamonRoot.IsImeActive` … 変換中（＋確定直後の数フレーム）。**アプリの入力ループはこの間、方向キー＝候補選択・Enter＝確定を OS/IME に委ね、フォーカス移動/Submit に流さない**こと（横取り防止）。
- `HamonRoot.SoftKeyboardHeight` … モバイルのソフトキーボード高さ（UI を上げて隠れ防止に使う）。

`ITextInput`（バックエンド実装）：`Start()`/`Stop()`/`SetCaretRect(Rect)`。

## プラットフォーム別バックエンド

| プラットフォーム | バックエンド | 確定(commit) | 変換中(preedit) | 候補ウィンドウ | ビルド要件 |
|---|---|---|---|---|---|
| **Mac/Win/Linux（DesktopGL=SDL2）** | `Hamon.MonoGame.SdlTextInput` | MonoGame `Window.TextInput` | **SDL `SDL_TEXTEDITING` を `SDL_AddEventWatch` で取得** | SDL `SDL_SetTextInputRect` で caret 位置へ | 既定（DesktopGL） |
| **iOS** | `Hamon.MonoGame.iOS.IosTextInput` | `IUIKeyInput.InsertText` | OS キーボードが表示 | OS キーボード | `dotnet workload install ios`（`net10.0-ios`） |
| **Android** | `Hamon.MonoGame.Android.AndroidTextInput` | InputConnection `CommitText` | InputConnection `SetComposingText`→`DispatchComposition` | OS キーボード | `dotnet workload install android`（`net10.0-android`）＋**JDK 17** |

> ビルド検証済（コンパイル）：iOS `net10.0-ios` 0エラー、Android `net10.0-android` 0エラー（JDK 17）。動作（確定/変換/キーボード高さ）は実機で要確認。
> Android は JDK 17 が必要。keg-only な Homebrew openjdk@17 等を使う場合は `JavaSdkDirectory` を JDK Home に指す
> （例 `dotnet build -p:JavaSdkDirectory=/opt/homebrew/Cellar/openjdk@17/<ver>/libexec/openjdk.jdk/Contents/Home`）、
> もしくは `JAVA_HOME` 設定／`/Library/Java/JavaVirtualMachines` へシンボリックリンク。

> モバイルは変換候補 UI を OS キーボードが出すのが通常で、アプリ内インライン preedit は必須ではない（Android は InputConnection 経由で preedit も供給可）。

## 統合（デスクトップ）

```csharp
_ui.TextInput = new SdlTextInput((preedit, caret) => _ui.DispatchComposition(preedit, caret));
Window.TextInput += (_, e) => _ui.DispatchText(e.Character); // 確定文字
// 入力ループでは _ui.IsImeActive の間、方向キー/Enter のアプリ処理をスキップする。
```

SDL2 が解決できない環境では `SdlTextInput` は**安全に no-op へ縮退**（確定入力は `Window.TextInput` で継続）。

## 統合（モバイル・リファレンス／実機検証前提）

`Hamon.MonoGame.iOS` / `Hamon.MonoGame.Android` は **`Hamon.sln` には含めない**（デスクトップのビルドを壊さないため）。
モバイルアプリ側のソリューションへ追加し、対応ワークロードを入れてビルドする。

- **iOS**：`_ui.TextInput = new IosTextInput(_ui);`（起動後・UI スレッド）。
- **Android**：ゲームビューで `OnCheckIsTextEditor()=>true` と `OnCreateInputConnection` をオーバーライドして
  `AndroidTextInput.CreateInputConnection(...)` を返す。`_ui.TextInput = new AndroidTextInput(_ui, activity, view);`、
  ルートビューに `AttachKeyboardHeightWatcher(root)` でキーボード高さを反映。

## 既知の留意点

- **HiDPI（Retina/高 DPI）**：候補ウィンドウ位置がズレる場合、`SetCaretRect` に渡すキャレット矩形へスケールを掛ける。
- **確定 Enter のフレーム順**：OS により IME イベントと物理キーの順が異なる。誤動作時は `IsImeActive` のガードフレーム数（既定3）を調整。
- **KNI/Web**：上記バックエンドは対象外（SDL/ネイティブ依存）。Web では HTML input 連携の別バックエンドが必要（後続）。
- モバイルバックエンドは API 準拠の**リファレンス実装**であり、実機での確定/変換/キーボード高さの検証が必要。
