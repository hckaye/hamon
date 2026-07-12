# Hamon IME and Text Input

[日本語版](ime.ja.md)

Hamon's IME support consists of an engine-independent core abstraction and platform-specific backends.

## Core API (`Hamon`)

- `FocusNode.OnComposition(string preedit, int caret)` receives composing text.
- `FocusNode.OnTextInput(char)` receives committed characters.
- `TextEditingController` exposes `Composition`, `CompositionCaret`, and `SetComposition`; composition is cleared when text is committed with `Insert`.
- `TextField` displays composing text inline with an underline and reports its caret rectangle to the IME for candidate-window placement.
- `IHamonHost.BeginTextInput()`, `EndTextInput()`, and `SetTextInputCaret(Rect)` notify the host when the focused field starts or stops editing and when its caret moves.
- Inject a backend through `HamonRoot.TextInput` (`ITextInput`). Feed events back through `HamonRoot.DispatchComposition` and `DispatchText`.
- While `HamonRoot.IsImeActive` is true, the application input loop must leave directional keys and Enter to the OS/IME instead of routing them to focus movement or submit handling.
- `HamonRoot.SoftKeyboardHeight` reports the mobile soft-keyboard height so the UI can avoid being covered.

`ITextInput` backends implement `Start()`, `Stop()`, and `SetCaretRect(Rect)`.

## Platform backends

| Platform | Backend | Commit | Composition | Candidate window | Build requirement |
|---|---|---|---|---|---|
| **macOS/Windows/Linux (DesktopGL/SDL2)** | `Hamon.MonoGame.SdlTextInput` | MonoGame `Window.TextInput` | SDL `SDL_TEXTEDITING` via `SDL_AddEventWatch` | SDL `SDL_SetTextInputRect` | DesktopGL (default) |
| **iOS** | `Hamon.MonoGame.iOS.IosTextInput` | `IUIKeyInput.InsertText` | Provided by the OS keyboard | OS keyboard | `dotnet workload install ios` (`net10.0-ios`) |
| **Android** | `Hamon.MonoGame.Android.AndroidTextInput` | `InputConnection.CommitText` | `InputConnection.SetComposingText` → `DispatchComposition` | OS keyboard | `dotnet workload install android` (`net10.0-android`) and **JDK 17** |

The mobile backends are API-compliant reference implementations. Validate commit, composition, and keyboard-height behavior on real devices. Android requires JDK 17; set `JavaSdkDirectory` or `JAVA_HOME` when the JDK is not discoverable automatically.

Mobile operating systems normally display the candidate UI through the system keyboard. Inline preedit is optional; Android can also provide it through `InputConnection`.

## Desktop integration

```csharp
_ui.TextInput = new SdlTextInput((preedit, caret) => _ui.DispatchComposition(preedit, caret));
Window.TextInput += (_, e) => _ui.DispatchText(e.Character); // committed character
// Skip application handling of directional keys and Enter while _ui.IsImeActive.
```

If SDL2 cannot be resolved, `SdlTextInput` safely becomes a no-op and committed input continues through `Window.TextInput`.

## Mobile integration

`Hamon.MonoGame.iOS` and `Hamon.MonoGame.Android` are not included in `Hamon.sln` so desktop builds remain independent of mobile workloads. Add the relevant project to the mobile application's solution.

- **iOS**: assign `_ui.TextInput = new IosTextInput(_ui);` after startup on the UI thread.
- **Android**: override `OnCheckIsTextEditor() => true` and `OnCreateInputConnection` on the game view, return `AndroidTextInput.CreateInputConnection(...)`, and assign `_ui.TextInput = new AndroidTextInput(_ui, activity, view)`. Attach `AttachKeyboardHeightWatcher(root)` to report the keyboard height.

## Known considerations

- **HiDPI**: if the candidate window is misplaced, scale the caret rectangle passed to `SetCaretRect`.
- **Commit Enter ordering**: IME and physical-key event order varies by OS. Adjust the `IsImeActive` guard-frame count (default: 3) if needed.
- **KNI/Web**: these backends depend on SDL or native APIs and are not supported. Web requires a separate HTML-input backend.
