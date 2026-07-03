using Avalonia;
using Avalonia.Controls;

namespace OscarWatch.Controls;

public static class UppercaseTextInput
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("IsEnabled", typeof(UppercaseTextInput));

    static UppercaseTextInput()
    {
        IsEnabledProperty.Changed.AddClassHandler<TextBox>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(TextBox element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(TextBox element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            textBox.TextChanged += OnTextChanged;
        else
            textBox.TextChanged -= OnTextChanged;
    }

    private static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
            return;

        var text = textBox.Text;
        if (string.IsNullOrEmpty(text))
            return;

        var upper = text.ToUpperInvariant();
        if (string.Equals(upper, text, StringComparison.Ordinal))
            return;

        var caret = textBox.CaretIndex;
        var selectionStart = textBox.SelectionStart;
        var selectionEnd = textBox.SelectionEnd;

        textBox.Text = upper;
        textBox.CaretIndex = Math.Clamp(caret, 0, upper.Length);
        textBox.SelectionStart = Math.Clamp(selectionStart, 0, upper.Length);
        textBox.SelectionEnd = Math.Clamp(selectionEnd, 0, upper.Length);
    }
}
