using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace VrcKaihenLibrary.Controls;

public sealed class HorizontalResizeCursorGrid : Grid
{
    public HorizontalResizeCursorGrid()
    {
        PointerEntered += (_, _) =>
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        PointerExited += (_, _) => ProtectedCursor = null;
    }
}
