using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.SampleApp;

/// <summary>
/// Catalog screen for all components.
/// Tooltip/Wrap/RichText/Badge/CooldownButton/SlotButton/VirtualJoystick/Dpad/ButtonStyle etc.) in a list
/// touch. <c>UseState</c>) and the toggle/slider/selection actually moves.
/// </summary>
public sealed class ComponentGalleryApp : HookWidget
{
    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;

        HookState<bool> check = hooks.UseState(true);
        HookState<bool> toggle = hooks.UseState(false);
        HookState<int> radio = hooks.UseState(1);
        HookState<string> seg = hooks.UseState("mid");
        HookState<float> slider = hooks.UseState(0.5f);
        HookState<int> drop = hooks.UseState(2);
        HookState<int> selectedSlot = hooks.UseState(0);

        return new ScrollView
        {
            Child = new Padding
            {
                Insets = EdgeInsets.All(16f),
                Child = new Column
                {
                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                    Spacing = 18f,
                    Children = new Widget[]
                    {
                        Section(theme, "Buttons / ButtonStyle", new Wrap
                        {
                            Spacing = 8f,
                            RunSpacing = 8f,
                            Children = new Widget[]
                            {
                                new Button { Node = new FocusNode(), Background = theme.SurfaceVariant, Radius = theme.Radius, Padding = EdgeInsets.Symmetric(16f, 10f), OnPressed = () => { }, Child = new Text("Default") { FontSize = 15f, Color = theme.OnSurface } },
                                new Button { Node = new FocusNode(), Background = theme.Primary, Radius = theme.Radius, Padding = EdgeInsets.Symmetric(16f, 10f), OnPressed = () => { }, Child = new Text("Primary") { FontSize = 15f, Color = theme.OnPrimary } },
                                new Button { Node = new FocusNode(), Enabled = false, Background = theme.SurfaceVariant, Radius = theme.Radius, Padding = EdgeInsets.Symmetric(16f, 10f), OnPressed = () => { }, Child = new Text("Disabled") { FontSize = 15f, Color = theme.OnSurface } },
                                new Button { Node = new FocusNode(), Style = Button.StyleFrom(background: new Color(70, 50, 110), radius: 18f, side: new BorderSide(theme.Primary, 2f), padding: EdgeInsets.Symmetric(16f, 10f)), OnPressed = () => { }, Child = new Text("Styled") { FontSize = 15f, Color = theme.OnSurface } },
                                new Badge { Label = "9", Child = new Button { Node = new FocusNode(), Background = theme.SurfaceVariant, Radius = theme.Radius, Padding = EdgeInsets.Symmetric(16f, 10f), OnPressed = () => { }, Child = new Text("Inbox") { FontSize = 15f, Color = theme.OnSurface } } },
                            },
                        }),

                        Section(theme, "Toggles", new Column
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Start,
                            Spacing = 12f,
                            Children = new Widget[]
                            {
                                new Row
                                {
                                    CrossAxisAlignment = CrossAxisAlignment.Center,
                                    Spacing = 16f,
                                    Children = new Widget[]
                                    {
                                        new Checkbox { Node = new FocusNode(), Value = check.Value, OnChanged = v => check.Value = v },
                                        new Text(check.Value ? "Checked" : "Unchecked") { FontSize = 15f, Color = theme.OnSurface },
                                        new Switch { Node = new FocusNode(), Value = toggle.Value, OnChanged = v => toggle.Value = v },
                                        new Text(toggle.Value ? "On" : "Off") { FontSize = 15f, Color = theme.OnSurface },
                                    },
                                },
                                new Row
                                {
                                    CrossAxisAlignment = CrossAxisAlignment.Center,
                                    Spacing = 10f,
                                    Children = new Widget[]
                                    {
                                        Radio(theme, 1, radio), new Text("A") { FontSize = 14f, Color = theme.OnSurface },
                                        Radio(theme, 2, radio), new Text("B") { FontSize = 14f, Color = theme.OnSurface },
                                        Radio(theme, 3, radio), new Text("C") { FontSize = 14f, Color = theme.OnSurface },
                                    },
                                },
                                new SegmentedControl<string>
                                {
                                    Value = seg.Value,
                                    OnChanged = v => seg.Value = v,
                                    Segments = new[]
                                    {
                                        new SegmentItem<string>("low", new Text("Low") { FontSize = 14f, Color = theme.OnSurface }),
                                        new SegmentItem<string>("mid", new Text("Mid") { FontSize = 14f, Color = theme.OnSurface }),
                                        new SegmentItem<string>("high", new Text("High") { FontSize = 14f, Color = theme.OnSurface }),
                                    },
                                },
                            },
                        }),

                        Section(theme, "Selection / Slider", new Column
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Start,
                            Spacing = 14f,
                            Children = new Widget[]
                            {
                                new Dropdown<int>
                                {
                                    Value = drop.Value,
                                    OnChanged = v => drop.Value = v,
                                    Items = new[]
                                    {
                                        new DropdownItem<int>(1, new Text("One") { FontSize = 14f, Color = theme.OnSurface }),
                                        new DropdownItem<int>(2, new Text("Two") { FontSize = 14f, Color = theme.OnSurface }),
                                        new DropdownItem<int>(3, new Text("Three") { FontSize = 14f, Color = theme.OnSurface }),
                                    },
                                },
                                new Row
                                {
                                    CrossAxisAlignment = CrossAxisAlignment.Center,
                                    Spacing = 12f,
                                    Children = new Widget[]
                                    {
                                        new Expanded { Child = new Slider { Node = new FocusNode(), Value = slider.Value, OnChanged = v => slider.Value = v } },
                                        new Text($"{(int)(slider.Value * 100)}%") { FontSize = 14f, Color = theme.OnSurfaceVariant },
                                    },
                                },
                                new ProgressBar { Value = slider.Value, Height = 8f },
                            },
                        }),

                        Section(theme, "RichText / Tooltip", new Column
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Start,
                            Spacing = 10f,
                            Children = new Widget[]
                            {
                                new RichText
                                {
                                    FontSize = 16f,
                                    Spans = new[]
                                    {
                                        new TextSpan("Deal ") { Color = theme.OnSurface },
                                        new TextSpan("120 ") { Color = new Color(240, 110, 128), FontSize = 18f },
                                        new TextSpan("fire damage to ") { Color = theme.OnSurface },
                                        new TextSpan("all ") { Color = new Color(134, 186, 255) },
                                        new TextSpan("enemies.") { Color = theme.OnSurface },
                                    },
                                },
                                new Tooltip
                                {
                                    Message = "ホバーで説明が出ます",
                                    WaitDuration = 0.4f,
                                    Child = new Container { Color = theme.SurfaceVariant, Radius = theme.Radius, Padding = EdgeInsets.Symmetric(14f, 8f), Child = new Text("Hover me") { FontSize = 14f, Color = theme.OnSurface } },
                                },
                            },
                        }),

                        Section(theme, "Game: slots / cooldown / dpad", new Row
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Center,
                            Spacing = 14f,
                            Children = new Widget[]
                            {
                                Slot(theme, 0, 1, selectedSlot),
                                Slot(theme, 1, 12, selectedSlot),
                                Slot(theme, 2, 3, selectedSlot),
                                new CooldownButton { Node = new FocusNode(), Progress = 0.45f, OnPressed = () => { }, Child = new Text("Q") { FontSize = 20f, Color = theme.OnSurface } },
                                new Dpad { Size = 84f },
                                new VirtualJoystick { Size = 84f, KnobSize = 34f },
                            },
                        }),
                    },
                },
            },
        };
    }

    private static Widget Section(HamonTheme theme, string title, Widget body) => new Column
    {
        CrossAxisAlignment = CrossAxisAlignment.Stretch,
        Spacing = 8f,
        Children = new Widget[]
        {
            new Text(title) { FontSize = 16f, Color = theme.OnSurfaceVariant },
            new Container { Color = theme.Surface, Radius = theme.Radius, Padding = EdgeInsets.All(14f), Child = body },
        },
    };

    private static Widget Radio(HamonTheme theme, int value, HookState<int> group) =>
        new Radio<int> { Node = new FocusNode(), Value = value, GroupValue = group.Value, OnChanged = v => group.Value = v };

    private static Widget Slot(HamonTheme theme, int index, int count, HookState<int> selected) =>
        new SlotButton
        {
            Node = new FocusNode(),
            Size = 52f,
            Count = count,
            Selected = selected.Value == index,
            OnPressed = () => selected.Value = index,
            Icon = new Container { Width = Dimension.Px(24f), Height = Dimension.Px(24f), Radius = 4f, Color = index == 0 ? new Color(220, 110, 90) : index == 1 ? new Color(110, 200, 140) : new Color(150, 150, 230) },
        };
}
