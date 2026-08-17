#if GODOT_PC
#nullable enable
using Godot;
using ImGuiNET;

namespace ImGuiGodot;

public partial class ImGuiController : Node
{
    private Window _window = null!;
    public static ImGuiController Instance { get; private set; } = null!;
    private ImGuiControllerHelper _helper = null!;
    public Node Signaler { get; private set; } = null!;
    private readonly StringName _signalName = "imgui_layout";

    private sealed partial class ImGuiControllerHelper : Node
    {
        private bool[] _prevMouseButtons = new bool[3];
        private bool _didLogTransform = false;

        public override void _Ready()
        {
            Name = "ImGuiControllerHelper";
            ProcessPriority = int.MinValue;
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _Process(double delta)
        {
            Internal.State.Instance.InProcessFrame = true;

            // Poll mouse position from the Input singleton every frame as a
            // fallback for editor embedded mode where _Input events may not be
            // delivered to CanvasLayer or even to regular Node _Input.
            // This is the most reliable way to keep ImGui's mouse position
            // in sync regardless of the execution mode.
            var io = ImGuiNET.ImGui.GetIO();
            if (!io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
            {
                // The CanvasItem displaying the SubViewport texture uses
                // transform = FinalTransform.AffineInverse(), so a point P in
                // SubViewport space appears at FinalTransform.AffineInverse()*P
                // on the parent canvas.  Inverting: parent mouse M maps to
                // FinalTransform * M in SubViewport/ImGui space.
                var ft = GetViewport().GetFinalTransform();
                Vector2 rawPos = GetViewport().GetMousePosition();
                Vector2 mousePos = ft * rawPos;

                if (!_didLogTransform)
                {
                    _didLogTransform = true;
                    GD.Print($"[ImGuiControllerHelper] ft={ft} rawPos={rawPos} xformed={mousePos} vpSize={GetViewport().GetVisibleRect().Size}");
                }

                io.AddMousePosEvent((float)mousePos.X, (float)mousePos.Y);

                // Poll mouse button state via frame comparison to detect
                // press/release transitions without relying on _Input events.
                for (int i = 0; i < 3; i++)
                {
                    bool pressed = Input.IsMouseButtonPressed(
                        (MouseButton)((int)MouseButton.Left + i));
                    if (pressed != _prevMouseButtons[i])
                    {
                        io.AddMouseButtonEvent(i, pressed);
                        _prevMouseButtons[i] = pressed;
                    }
                }
            }

            var vpSize = Internal.State.Instance.Layer.UpdateViewport();
            Internal.State.Instance.Update(delta, new(vpSize.X, vpSize.Y));
        }
    }

    public override void _EnterTree()
    {
        Instance = this;
        _window = GetWindow();
        _window.WindowInput += OnWindowInput;

        CheckContentScale();

        string cfgPath = (string)ProjectSettings.GetSetting("addons/imgui/config", "");
        Resource? cfg = null;
        if (ResourceLoader.Exists(cfgPath))
        {
            cfg = ResourceLoader.Load(cfgPath);
            float scale = (float)cfg.Get("Scale");
            bool cfgok = scale > 0.0f;

            if (!cfgok)
            {
                GD.PushError($"imgui-godot: config not a valid ImGuiConfig resource: {cfgPath}");
                cfg = null;
            }
        }
        else if (cfgPath.Length > 0)
        {
            GD.PushError($"imgui-godot: config does not exist: {cfgPath}");
        }

        Internal.State.Init(cfg ?? (Resource)((GDScript)GD.Load(
                "res://addons/imgui-godot/scripts/ImGuiConfig.gd")).New());

        _helper = new ImGuiControllerHelper();
        AddChild(_helper);

        Signaler = GetParent();
        SetMainViewport(_window);
    }

    public override void _Ready()
    {
        ProcessPriority = int.MaxValue;
        ProcessMode = ProcessModeEnum.Always;
        SetProcessInput(true);
    }

    public override void _ExitTree()
    {
        _window.WindowInput -= OnWindowInput;
        Internal.State.Instance.Dispose();
    }

    public override void _Input(InputEvent @event)
    {
        if (TransformMouseEvent(@event, out var transformed))
        {
            if (Internal.State.Instance.Input.ProcessInput(transformed))
            {
                GetViewport().SetInputAsHandled();
            }
            transformed.Dispose();
        }
        else if (Internal.State.Instance.Input.ProcessInput(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnWindowInput(InputEvent evt)
    {
        if (TransformMouseEvent(evt, out var transformed))
        {
            if (Internal.State.Instance.Input.ProcessInput(transformed))
            {
                _window.SetInputAsHandled();
            }
            transformed.Dispose();
        }
        else if (Internal.State.Instance.Input.ProcessInput(evt))
        {
            _window.SetInputAsHandled();
        }
    }

    /// <summary>
    /// Transform the mouse event position through the viewport's FinalTransform
    /// to convert from parent viewport space to SubViewport/ImGui space.
    /// Returns true and sets <paramref name="transformed"/> when the event is a
    /// mouse event and the transform is not identity.
    /// </summary>
    private bool TransformMouseEvent(InputEvent evt, out InputEvent? transformed)
    {
        transformed = null;
        if (evt is InputEventMouse me)
        {
            var ft = GetViewport().GetFinalTransform();
            if (ft != Transform2D.Identity)
            {
                var dup = (InputEventMouse)me.Duplicate();
                dup.Position = ft * me.Position;
                transformed = dup;
                return true;
            }
        }
        return false;
    }

    public override void _Process(double delta)
    {
        Signaler.EmitSignal(_signalName);
        Internal.State.Instance.Render();
        Internal.State.Instance.InProcessFrame = false;
    }

    public override void _Notification(int what)
    {
        Internal.Input.ProcessNotification(what);
    }

    public void OnLayerExiting()
    {
        // an ImGuiLayer is being destroyed without calling SetMainViewport
        if (Internal.State.Instance.Layer.GetViewport() != _window)
        {
            // revert to main window
            SetMainViewport(_window);
        }
    }

    public void SetMainViewport(Viewport vp)
    {
        ImGuiLayer? oldLayer = Internal.State.Instance.Layer;
        if (oldLayer != null)
        {
            oldLayer.TreeExiting -= OnLayerExiting;
            oldLayer.QueueFree();
        }

        var newLayer = new ImGuiLayer();
        newLayer.TreeExiting += OnLayerExiting;

        if (vp is Window window)
        {
            Internal.State.Instance.Input = new Internal.Input();
            if (window == _window)
                AddChild(newLayer);
            else
                window.AddChild(newLayer);
            ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.PlatformHasViewports
                | ImGuiBackendFlags.HasMouseHoveredViewport;
        }
        else if (vp is SubViewport svp)
        {
            Internal.State.Instance.Input = new Internal.InputLocal();
            svp.AddChild(newLayer);
            ImGui.GetIO().BackendFlags &= ~ImGuiBackendFlags.PlatformHasViewports;
            ImGui.GetIO().BackendFlags &= ~ImGuiBackendFlags.HasMouseHoveredViewport;
        }
        else
        {
            throw new System.ArgumentException("secret third kind of viewport??", nameof(vp));
        }
        Internal.State.Instance.Layer = newLayer;
    }

    private void CheckContentScale()
    {
        if (_window.ContentScaleMode == Window.ContentScaleModeEnum.Viewport)
        {
            GD.PrintErr("imgui-godot: scale mode `viewport` is unsupported");
        }
    }

    public static void WindowInputCallback(InputEvent evt)
    {
        Internal.State.Instance.Input.ProcessInput(evt);
    }
}
#endif
