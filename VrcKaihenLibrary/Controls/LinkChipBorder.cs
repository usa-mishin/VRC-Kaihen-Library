using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Text;

namespace VrcKaihenLibrary.Controls;

public sealed class LinkChipBorder : HandCursorButton
{
    public LinkChipBorder()
    {
        PointerEntered += OnLinkPointerEntered;
        PointerExited += OnLinkPointerExited;
    }

    private void OnLinkPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Content is TextBlock text) text.TextDecorations = TextDecorations.Underline;
    }

    private void OnLinkPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (Content is TextBlock text) text.TextDecorations = TextDecorations.None;
    }
}
