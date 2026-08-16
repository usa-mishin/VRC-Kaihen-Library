using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace VrcKaihenManager.Controls;

public class HandCursorButton : Button
{
    public HandCursorButton()
    {
        PointerEntered += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        PointerExited += (_, _) => ProtectedCursor = null;
    }
}
