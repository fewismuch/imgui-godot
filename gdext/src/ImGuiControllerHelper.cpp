#include "ImGuiControllerHelper.h"
#include "Context.h"
#include <godot_cpp/classes/viewport.hpp>
#include <imgui.h>
using namespace godot;

namespace ImGui::Godot {

ImGuiControllerHelper::ImGuiControllerHelper()
{
}

ImGuiControllerHelper::~ImGuiControllerHelper()
{
}

void ImGuiControllerHelper::_bind_methods()
{
}

void ImGuiControllerHelper::_enter_tree()
{
}

void ImGuiControllerHelper::_ready()
{
    set_name("ImGuiControllerHelper");
    set_process_priority(std::numeric_limits<int32_t>::min());
    set_process_mode(Node::PROCESS_MODE_ALWAYS);
}

void ImGuiControllerHelper::_exit_tree()
{
}

void ImGuiControllerHelper::_process(double delta)
{
    Context* ctx = GetContext();
    ctx->inProcessFrame = true;

    // Poll mouse position and button state every frame as a fallback for
    // editor embedded mode where _Input events may not be delivered.
    if (!(ImGui::GetIO().ConfigFlags & ImGuiConfigFlags_ViewportsEnable))
    {
        const Vector2 mousePos = get_viewport()->get_mouse_position();
        ImGui::GetIO().AddMousePosEvent((float)mousePos.x, (float)mousePos.y);

        godot::Input* gdinput = godot::Input::get_singleton();
        static const MouseButton buttons[3] = {
            MouseButton::MOUSE_BUTTON_LEFT,
            MouseButton::MOUSE_BUTTON_RIGHT,
            MouseButton::MOUSE_BUTTON_MIDDLE,
        };
        for (int i = 0; i < 3; i++)
        {
            const bool pressed = gdinput->is_mouse_button_pressed(buttons[i]);
            if (pressed != _prevMouseButtons[i])
            {
                ImGui::GetIO().AddMouseButtonEvent(i, pressed);
                _prevMouseButtons[i] = pressed;
            }
        }
    }

    ctx->Update(delta, ctx->layer->UpdateViewport());
}

} // namespace ImGui::Godot
