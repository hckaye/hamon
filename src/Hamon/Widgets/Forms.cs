using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>Standard field validation (null = valid, string = error message).<see cref="TextFormField.Validator"/>give it to</summary>
public static class Validators
{
    /// <summary>Non-empty (excluding leading and trailing whitespace).</summary>
    public static Func<string, string?> Required(string message = "必須項目です") =>
        s => string.IsNullOrWhiteSpace(s) ? message : null;

    /// <summary>Minimum number of characters.</summary>
    public static Func<string, string?> MinLength(int min, string? message = null) =>
        s => (s?.Length ?? 0) < min ? (message ?? $"{min}文字以上で入力してください") : null;

    /// <summary>Simple email format ("x@y.z").</summary>
    public static Func<string, string?> Email(string message = "メールアドレスの形式が正しくありません") =>
        s =>
        {
            if (string.IsNullOrEmpty(s))
            {
                return null; // 空は Required に任せる
            }

            int at = s.IndexOf('@');
            int dot = s.LastIndexOf('.');
            return at > 0 && dot > at + 1 && dot < s.Length - 1 ? null : message;
        };

    /// <summary>Apply multiple validations in sequence and return the first error.</summary>
    public static Func<string, string?> Compose(params Func<string, string?>[] validators) =>
        s =>
        {
            for (int i = 0; i < validators.Length; i++)
            {
                string? e = validators[i](s);
                if (e is not null)
                {
                    return e;
                }
            }

            return null;
        };
}

/// <summary>
/// Form field aggregation and bulk validation (Flutter<c>Form</c>/<c>FormState</c>A fairly lightweight version).<see cref="TextFormField"/>but
/// register itself on mount,<see cref="Validate"/>Validate all fields and reflect errors to each field.
/// </summary>
public sealed class FormController
{
    private sealed class FieldReg
    {
        public required string Name { get; init; }

        public required Func<string> Value { get; init; }

        public Func<string, string?>? Validator { get; init; }

        public required Action<string?> SetError { get; init; }
    }

    private readonly List<FieldReg> _fields = new();

    internal void Register(string name, Func<string> value, Func<string, string?>? validator, Action<string?> setError)
    {
        Unregister(name); // 同名の再登録（再マウント）に備える
        _fields.Add(new FieldReg { Name = name, Value = value, Validator = validator, SetError = setError });
    }

    internal void Unregister(string name)
    {
        for (int i = _fields.Count - 1; i >= 0; i--)
        {
            if (_fields[i].Name == name)
            {
                _fields.RemoveAt(i);
            }
        }
    }

    /// <summary>Validate all fields and reflect errors in each field. </summary>
    public bool Validate()
    {
        bool ok = true;
        for (int i = 0; i < _fields.Count; i++)
        {
            FieldReg f = _fields[i];
            string? error = f.Validator?.Invoke(f.Value());
            f.SetError(error);
            if (error is not null)
            {
                ok = false;
            }
        }

        return ok;
    }

    /// <summary>Clear error display for all fields.</summary>
    public void Reset()
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            _fields[i].SetError(null);
        }
    }

    /// <summary>A snapshot of each current field value (name→value).</summary>
    public IReadOnlyDictionary<string, string> Values()
    {
        var map = new Dictionary<string, string>(_fields.Count);
        for (int i = 0; i < _fields.Count; i++)
        {
            map[_fields[i].Name] = _fields[i].Value();
        }

        return map;
    }
}

/// <summary>
/// Text field with validation (Flutter<c>TextFormField</c>equivalent).<see cref="Form"/>to<see cref="Name"/>Registered with
/// <see cref="FormController.Validate"/>sometimes<see cref="Validator"/>will be verified, and if there are any errors, they will be displayed in red below the input field.
/// Value retention is provided by the app.<see cref="Controller"/>（<see cref="TextEditingController"/>）。
/// </summary>
public sealed class TextFormField : HookWidget
{
    public required FormController Form { get; init; }

    public required string Name { get; init; }

    public required TextEditingController Controller { get; init; }

    public string? Label { get; init; }

    public string Placeholder { get; init; } = string.Empty;

    public Func<string, string?>? Validator { get; init; }

    public bool Autofocus { get; init; }

    public Dimension Width { get; init; }

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<string?> error = hooks.UseState<string?>(null);
        FocusNode node = hooks.UseFocusNode();

        hooks.UseEffect(
            () =>
            {
                Form.Register(Name, () => Controller.Text, Validator, e => error.Value = e);
                return () => Form.Unregister(Name);
            },
            Name);

        var children = new List<Widget>(3);
        if (Label is not null)
        {
            children.Add(new Text(Label) { FontSize = theme.TextLabel, Color = theme.OnSurfaceVariant });
        }

        children.Add(new TextField
        {
            Controller = Controller,
            Node = node,
            Autofocus = Autofocus,
            Placeholder = Placeholder,
            Width = Width,
        });

        if (error.Value is string message)
        {
            children.Add(new Text(message) { FontSize = theme.TextCaption, Color = theme.Danger });
        }

        return new Column
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            MainAxisSize = MainAxisSize.Min,
            Spacing = theme.SpacingXs,
            Children = children,
        };
    }
}
