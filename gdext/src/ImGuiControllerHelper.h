#pragma once
#include <godot_cpp/classes/input.hpp>
#include <godot_cpp/classes/node.hpp>
#include <array>

using godot::Node;

namespace ImGui::Godot {

class ImGuiControllerHelper : public Node
{
    GDCLASS(ImGuiControllerHelper, Node);

protected:
    static void _bind_methods();

public:
    void _ready() override;
    void _enter_tree() override;
    void _exit_tree() override;
    void _process(double delta) override;

    ImGuiControllerHelper();
    ~ImGuiControllerHelper();

private:
    std::array<bool, 3> _prevMouseButtons{};
};

} // namespace ImGui::Godot
