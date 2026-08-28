using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VrcKaihenLibrary.Controls;

public sealed class HandCursorGridView : GridView
{
    protected override DependencyObject GetContainerForItemOverride() => new HandCursorGridViewItem();
}

public sealed class HandCursorGridViewItem : GridViewItem
{
    public HandCursorGridViewItem()
    {
        PointerEntered += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        PointerExited += (_, _) => ProtectedCursor = null;
    }
}
