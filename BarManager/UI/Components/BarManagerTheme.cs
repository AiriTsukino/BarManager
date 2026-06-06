using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BarManager.UI.Components;

internal static class BarManagerTheme
{
    public static readonly Vector4 Gold = new(0.96f, 0.78f, 0.25f, 1f);
    public static readonly Vector4 Text = new(0.88f, 0.82f, 0.92f, 1f);
    public static readonly Vector4 Muted = new(0.66f, 0.58f, 0.74f, 1f);
    public static readonly Vector4 Green = new(0.48f, 0.83f, 0.62f, 1f);
    public static readonly Vector4 Red = new(0.86f, 0.32f, 0.42f, 1f);
    public static readonly Vector4 PanelBg = new(0.075f, 0.055f, 0.115f, 0.96f);
    public static readonly Vector4 Border = new(0.34f, 0.20f, 0.54f, 1f);
    public static readonly Vector4 PurpleActive = new(0.50f, 0.20f, 0.82f, 1f);
    public static readonly Vector4 PurpleHovered = new(0.55f, 0.23f, 0.96f, 1f);

    private static int pushedColors;
    private static int pushedVars;

    public static void Push()
    {
        pushedColors = 0;
        pushedVars = 0;
        PushColor(ImGuiCol.WindowBg, new Vector4(0.035f, 0.027f, 0.060f, 0.98f));
        PushColor(ImGuiCol.ChildBg, new Vector4(0.055f, 0.044f, 0.09f, 0.94f));
        PushColor(ImGuiCol.PopupBg, new Vector4(0.06f, 0.045f, 0.09f, 0.98f));
        PushColor(ImGuiCol.Border, Border);
        PushColor(ImGuiCol.FrameBg, new Vector4(0.11f, 0.085f, 0.16f, 1f));
        PushColor(ImGuiCol.FrameBgHovered, new Vector4(0.20f, 0.13f, 0.30f, 1f));
        PushColor(ImGuiCol.FrameBgActive, new Vector4(0.28f, 0.16f, 0.45f, 1f));
        PushColor(ImGuiCol.TitleBg, new Vector4(0.08f, 0.055f, 0.13f, 1f));
        PushColor(ImGuiCol.TitleBgActive, new Vector4(0.17f, 0.10f, 0.28f, 1f));
        PushColor(ImGuiCol.CheckMark, Gold);
        PushColor(ImGuiCol.SliderGrab, PurpleActive);
        PushColor(ImGuiCol.SliderGrabActive, PurpleHovered);
        PushColor(ImGuiCol.Button, new Vector4(0.17f, 0.12f, 0.25f, 1f));
        PushColor(ImGuiCol.ButtonHovered, new Vector4(0.30f, 0.20f, 0.48f, 1f));
        PushColor(ImGuiCol.ButtonActive, PurpleActive);
        PushColor(ImGuiCol.Header, new Vector4(0.16f, 0.10f, 0.25f, 1f));
        PushColor(ImGuiCol.HeaderHovered, new Vector4(0.28f, 0.16f, 0.43f, 1f));
        PushColor(ImGuiCol.HeaderActive, PurpleActive);
        PushColor(ImGuiCol.Tab, new Vector4(0.10f, 0.08f, 0.15f, 1f));
        PushColor(ImGuiCol.TabHovered, new Vector4(0.35f, 0.17f, 0.55f, 1f));
        PushColor(ImGuiCol.TabActive, new Vector4(0.22f, 0.13f, 0.35f, 1f));
        PushColor(ImGuiCol.Text, Text);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f); pushedVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f); pushedVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 6f); pushedVars++;
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 6f); pushedVars++;
    }

    public static void Pop()
    {
        if (pushedVars > 0) ImGui.PopStyleVar(pushedVars);
        if (pushedColors > 0) ImGui.PopStyleColor(pushedColors);
        pushedVars = 0;
        pushedColors = 0;
    }

    private static void PushColor(ImGuiCol col, Vector4 color)
    {
        ImGui.PushStyleColor(col, color);
        pushedColors++;
    }

    public static void PushKofiButton()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.42f, 0.15f, 0.78f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.55f, 0.23f, 0.96f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.30f, 0.10f, 0.58f, 1f));
    }

    public static void PopKofiButton() => ImGui.PopStyleColor(3);
}
