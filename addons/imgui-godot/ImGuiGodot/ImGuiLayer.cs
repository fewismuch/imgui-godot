using Godot;
#if GODOT_PC
#nullable enable

namespace ImGuiGodot;

public partial class ImGuiLayer : CanvasLayer
{
    private Rid _subViewportRid;
    private Vector2I _subViewportSize = Vector2I.Zero;
    private Rid _canvasItem;
    private Transform2D _finalTransform = Transform2D.Identity;
    private bool _visible = true;
    private Viewport _parentViewport = null!;

    public override void _EnterTree()
    {
        Name = "ImGuiLayer";
        Layer = Internal.State.Instance.LayerNum;

        _parentViewport = GetViewport();
        _subViewportRid = AddLayerSubViewport(this);
        _canvasItem = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_canvasItem, GetCanvas());

        Internal.State.Instance.Renderer.InitViewport(_subViewportRid);
        Internal.State.Instance.Viewports.SetMainWindow(GetWindow(), _subViewportRid);
    }

    public override void _Ready()
    {
        VisibilityChanged += OnChangeVisibility;
        OnChangeVisibility();
    }

    public override void _ExitTree()
    {
        RenderingServer.FreeRid(_canvasItem);
        RenderingServer.FreeRid(_subViewportRid);
    }

    private void OnChangeVisibility()
    {
        _visible = Visible;
        if (_visible)
        {
            SetProcessInput(true);
        }
        else
        {
            SetProcessInput(false);
            Internal.State.Instance.Renderer.OnHide();
            _subViewportSize = Vector2I.Zero;
            RenderingServer.CanvasItemClear(_canvasItem);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouse mouseEvent)
        {
            // In editor embedded mode, the viewport is sometimes wrapped in a
            // transform so the displayed mouse position and the SubViewport
            // draw space don't match. Always try the raw position first. If
            // this still yields wrong results, we'll switch to FinalTransform
            // below.
            var io = ImGuiNET.ImGui.GetIO();
            bool wasCapturing = io.WantCaptureMouse;

            // TEMP diagnostic: log event so we can see the exact position
            // values coming in and what ImGui thinks about capture.
            // Use GD.PushWarning once per second max to avoid spamming.
            if (!wasCapturing && mouseEvent is InputEventMouseButton mb && mb.Pressed)
            {
                GD.PushWarning($"[ImGuiLayer] ButtonClick rawPos={mouseEvent.Position} vpSize={_subViewportSize} FinalTransform={_finalTransform} DisplaySize={io.DisplaySize} WantCaptureMouse_BEFORE={io.WantCaptureMouse}");
            }

            if (Internal.State.Instance.Input.ProcessInput(mouseEvent))
            {
                if (mouseEvent is InputEventMouseButton mb2 && mb2.Pressed)
                    GD.Print($"[ImGuiLayer] Click CONSUMED pos={mouseEvent.Position} WantCaptureMouse_AFTER={io.WantCaptureMouse}");
                _parentViewport.SetInputAsHandled();
            }
            else if (mouseEvent is InputEventMouseButton mb3 && mb3.Pressed)
            {
                GD.Print($"[ImGuiLayer] Click PASSED THROUGH pos={mouseEvent.Position} WantCaptureMouse_AFTER={io.WantCaptureMouse}");
            }
            return;
        }

        if (Internal.State.Instance.Input.ProcessInput(@event))
        {
            _parentViewport.SetInputAsHandled();
        }
    }

    public Vector2I UpdateViewport()
    {
        Vector2I vpSize = _parentViewport is Window w ? w.Size
            : (_parentViewport as SubViewport)?.Size
            ?? throw new System.InvalidOperationException();

        if (_visible)
        {
            var ft = _parentViewport.GetFinalTransform();
            if (_subViewportSize != vpSize || _finalTransform != ft)
            {
                // this is more or less how SubViewportContainer works
                _subViewportSize = vpSize;
                _finalTransform = ft;
                RenderingServer.ViewportSetSize(
                    _subViewportRid,
                    _subViewportSize.X,
                    _subViewportSize.Y);
                Rid vptex = RenderingServer.ViewportGetTexture(_subViewportRid);
                RenderingServer.CanvasItemClear(_canvasItem);
                RenderingServer.CanvasItemSetTransform(_canvasItem, ft.AffineInverse());
                RenderingServer.CanvasItemAddTextureRect(
                    _canvasItem,
                    new(0, 0, _subViewportSize.X, _subViewportSize.Y),
                    vptex);
            }
        }

        return vpSize;
    }

    private static Rid AddLayerSubViewport(Node parent)
    {
        Rid svp = RenderingServer.ViewportCreate();
        RenderingServer.ViewportSetTransparentBackground(svp, true);
        RenderingServer.ViewportSetUpdateMode(svp, RenderingServer.ViewportUpdateMode.Always);
        RenderingServer.ViewportSetClearMode(svp, RenderingServer.ViewportClearMode.Always);
        RenderingServer.ViewportSetActive(svp, true);
        RenderingServer.ViewportSetParentViewport(svp, parent.GetWindow().GetViewportRid());
        return svp;
    }
}
#else
namespace ImGuiNET
{
}

namespace ImGuiGodot
{
}
#endif
