#pragma once
#include "Renderer.h"
#include <godot_cpp/variant/rid.hpp>
#include <imgui.h>
#include <memory>

using godot::RID;

namespace ImGui::Godot {

class CanvasRenderer;

class RdRenderer : public Renderer
{
public:
    RdRenderer();
    virtual ~RdRenderer();

    virtual const char* Name() override { return "godot4_rd"; }

    bool Init() override;
    void InitViewport(RID vprid) override;
    void CloseViewport(RID vprid) override;
    virtual void Render() override;
    void OnHide() override;

protected:
    void Render(RID fb, ImDrawData* drawData);
    static void ReplaceTextureRIDs(ImDrawData* drawData);
    RID GetFramebuffer(RID vprid);
    void FreeUnusedTextures();
    bool IsFallbackActive() const;
    void RenderFallback();

private:
    void EnableFallback();
    void ReplayInitViewportForFallback();

    struct Impl;
    std::unique_ptr<Impl> impl;
    std::unique_ptr<CanvasRenderer> fallbackCanvas;
    bool fallbackWarned = false;
};

} // namespace ImGui::Godot
