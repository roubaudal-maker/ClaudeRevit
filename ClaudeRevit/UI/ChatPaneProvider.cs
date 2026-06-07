using Autodesk.Revit.UI;

namespace ClaudeRevit.UI;

public class ChatPaneProvider : IDockablePaneProvider
{
    private readonly ChatPaneView _view;

    public ChatPaneProvider(ChatPaneView view) => _view = view;

    public void SetupDockablePane(DockablePaneProviderData data)
    {
        data.FrameworkElement = _view;
        data.InitialState = new DockablePaneState
        {
            DockPosition = DockPosition.Right
        };
    }
}
