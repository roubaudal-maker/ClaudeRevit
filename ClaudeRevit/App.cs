using System;
using System.Reflection;
using Autodesk.Revit.UI;
using ClaudeRevit.Services;
using ClaudeRevit.Tools;
using ClaudeRevit.UI;

namespace ClaudeRevit;

public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            ToolRegistry.Instance.Register(new GetActiveViewInfo());
            ToolRegistry.Instance.Register(new GetLevels());
            ToolRegistry.Instance.Register(new GetSelection());
            ToolRegistry.Instance.Register(new QueryElements());
            ToolRegistry.Instance.Register(new AnalyzeWarnings());
            ToolRegistry.Instance.Register(new CreateWall());
            ToolRegistry.Instance.Register(new CreateFloor());
            ToolRegistry.Instance.Register(new CreateLevel());
            ToolRegistry.Instance.Register(new CreateGrid());
            ToolRegistry.Instance.Register(new CreateRoof());
            ToolRegistry.Instance.Register(new CreateRoom());
            ToolRegistry.Instance.Register(new CreateView());
            ToolRegistry.Instance.Register(new CreateSection());
            ToolRegistry.Instance.Register(new CreateElevation());
            ToolRegistry.Instance.Register(new CreateSheet());
            ToolRegistry.Instance.Register(new PlaceViewOnSheet());
            ToolRegistry.Instance.Register(new PlaceDoor());
            ToolRegistry.Instance.Register(new PlaceWindow());
            ToolRegistry.Instance.Register(new MoveElements());
            ToolRegistry.Instance.Register(new RotateElements());
            ToolRegistry.Instance.Register(new CopyElements());
            ToolRegistry.Instance.Register(new DeleteElements());
            ToolRegistry.Instance.Register(new SetParameter());
            ToolRegistry.Instance.Register(new SetColorOverride());
            ToolRegistry.Instance.Register(new TagElements());
            ToolRegistry.Instance.Register(new PickPointInView());
            ToolRegistry.Instance.Register(new CreateDimension());
            ToolRegistry.Instance.Register(new GetElementParameters());
            ToolRegistry.Instance.Register(new GetElementBoundingBox());
            ToolRegistry.Instance.Register(new MeasureDistance());
            ToolRegistry.Instance.Register(new GetProjectInfo());
            ToolRegistry.Instance.Register(new ListFamilyTypes());
            ToolRegistry.Instance.Register(new ListMaterials());
            ToolRegistry.Instance.Register(new GetPhases());
            ToolRegistry.Instance.Register(new SetActiveView());
            ToolRegistry.Instance.Register(new HideElementsInView());
            ToolRegistry.Instance.Register(new IsolateElementsInView());
            ToolRegistry.Instance.Register(new MirrorElements());
            ToolRegistry.Instance.Register(new ArrayElements());
            ToolRegistry.Instance.Register(new PinElements());
            ToolRegistry.Instance.Register(new UnpinElements());
            ToolRegistry.Instance.Register(new CreateTextNote());
            ToolRegistry.Instance.Register(new CreateDetailLine());
            ToolRegistry.Instance.Register(new CreateFilledRegion());
            ToolRegistry.Instance.Register(new CreateReferencePlane());
            ToolRegistry.Instance.Register(new CreateSchedule());
            ToolRegistry.Instance.Register(new Create3DView());
            ToolRegistry.Instance.Register(new DuplicateView());
            ToolRegistry.Instance.Register(new ApplyViewTemplate());
            ToolRegistry.Instance.Register(new SetViewScale());
            ToolRegistry.Instance.Register(new PlaceFamilyInstance());
            ToolRegistry.Instance.Register(new LoadFamily());
            ToolRegistry.Instance.Register(new ListLoadedFamilies());
            ToolRegistry.Instance.Register(new CreateStructuralColumn());
            ToolRegistry.Instance.Register(new CreateBeam());
            ToolRegistry.Instance.Register(new CreateOpeningInWall());
            ToolRegistry.Instance.Register(new CreateGroup());
            ToolRegistry.Instance.Register(new PlaceGroup());
            ToolRegistry.Instance.Register(new CreateViewFilter());
            ToolRegistry.Instance.Register(new ApplyFilterToView());
            ToolRegistry.Instance.Register(new ExportImage());
            ToolRegistry.Instance.Register(new SaveDocument());
            ToolRegistry.Instance.Register(new SelectSimilar());
            ToolRegistry.Instance.Register(new DuplicateSheet());
            ToolRegistry.Instance.Register(new GetSheetViews());
            ToolRegistry.Instance.Register(new MoveViewportOnSheet());
            ToolRegistry.Instance.Register(new ExportScheduleCsv());
            ToolRegistry.Instance.Register(new TagAllInView());
            ToolRegistry.Instance.Register(new PlaceRoomTag());
            ToolRegistry.Instance.Register(new LinkDwg());
            ToolRegistry.Instance.Register(new ListLinks());
            ToolRegistry.Instance.Register(new ReloadLink());
            ToolRegistry.Instance.Register(new HideCategoryInView());
            ToolRegistry.Instance.Register(new GetModelStatistics());
            ToolRegistry.Instance.Register(new CreateDraftingView());
            ToolRegistry.Instance.Register(new ExportPdf());
            ToolRegistry.Instance.Register(new ExportDwg());
            ToolRegistry.Instance.Register(new CreateCurtainWall());
            ToolRegistry.Instance.Register(new CreateTextWithLeader());
            ToolRegistry.Instance.Register(new DuplicateFamilyType());
            ToolRegistry.Instance.Register(new SetTypeParameter());
            ToolRegistry.Instance.Register(new UnloadFamily());
            ToolRegistry.Instance.Register(new CreateMaterial());
            ToolRegistry.Instance.Register(new SetElementMaterial());
            ToolRegistry.Instance.Register(new CreateCallout());
            ToolRegistry.Instance.Register(new CreateRevisionCloud());
            ToolRegistry.Instance.Register(new SetViewDetailLevel());
            ToolRegistry.Instance.Register(new DeleteView());
            ToolRegistry.Instance.Register(new ChangeElementType());
            ToolRegistry.Instance.Register(new CreateSelectionFilter());
            ToolRegistry.Instance.Register(new CreateShaftOpening());
            ToolRegistry.Instance.Register(new CreateDuct());
            ToolRegistry.Instance.Register(new CreatePipe());
            ToolRegistry.Instance.Register(new LinkRevitModel());
            ToolRegistry.Instance.Register(new SetProjectInfo());
            ToolRegistry.Instance.Register(new LoadFamily());
            ToolRegistry.Instance.Register(new DuplicateFamilyType());
            ToolDispatcher.Initialize(ToolRegistry.Instance);

            SelectionService.Initialize(application);

            var view = new ChatPaneView();
            application.RegisterDockablePane(PaneIds.Chat, "Claude Chat", new ChatPaneProvider(view));

            const string tabName = "Claude";
            application.CreateRibbonTab(tabName);
            var panel = application.CreateRibbonPanel(tabName, "Claude AI");

            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var chatButton = new PushButtonData(
                name: "ShowChatButton",
                text: "Chat",
                assemblyName: assemblyPath,
                className: "ClaudeRevit.Commands.ShowChatPaneCommand"
            )
            {
                ToolTip = "Toggle the Claude chat pane.",
                LongDescription = "Opens or closes the dockable Claude AI chat panel."
            };
            panel.AddItem(chatButton);

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("Claude Add-in", $"Failed to start: {ex.Message}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
