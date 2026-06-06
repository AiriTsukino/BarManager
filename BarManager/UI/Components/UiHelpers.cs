using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BarManager.UI.Components;

internal static class UiHelpers
{
    public static void Header(string title, string? subtitle = null)
    {
        ImGui.TextColored(BarManagerTheme.Gold, title);
        if (!string.IsNullOrWhiteSpace(subtitle))
            TextMuted(subtitle);
        ImGui.Separator();
    }

    public static void SectionTitle(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(BarManagerTheme.Gold, title);
        ImGui.Separator();
    }

    public static bool BeginCard(string id, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, BarManagerTheme.PanelBg);
        ImGui.PushStyleColor(ImGuiCol.Border, BarManagerTheme.Border);
        return ImGui.BeginChild(id, size, true);
    }

    public static void EndCard()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor(2);
    }

    public static void TextMuted(string text) => ImGui.TextColored(BarManagerTheme.Muted, text);

    public static void TextWrappedMuted(string text)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(BarManagerTheme.Muted, text);
        ImGui.PopTextWrapPos();
    }

    public static string Gil(int amount) => $"{amount:N0} gil";

    public static bool InputIntGil(string label, ref int value, int step = 1000)
    {
        var changed = ImGui.InputInt(label, ref value, step, Math.Max(step * 10, step));
        if (value < 0) value = 0;
        return changed;
    }

    public static void DrawSupportButtonRightAligned(string id)
    {
        var supportWidth = GetSupportButtonWidth();
        var available = ImGui.GetContentRegionAvail().X;

        ImGui.SameLine();
        if (available > supportWidth + 8f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + available - supportWidth);

        DrawSupportButton(id, supportWidth);
    }

    public static bool DrawSupportButton(string id, float width = 116f)
    {
        var supportLabel = $"      Support##{id}";
        var supportWidth = MathF.Max(width, GetSupportButtonWidth());

        BarManagerTheme.PushKofiButton();
        var supportClicked = ImGui.Button(supportLabel, new Vector2(supportWidth, 0));
        BarManagerTheme.PopKofiButton();

        DrawKofiCupIcon(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Support me on Ko-Fi");

        if (supportClicked)
            OpenSupportLink();

        return supportClicked;
    }

    public static float GetSupportButtonWidth() => MathF.Max(116f, ImGui.CalcTextSize("Support").X + 52f);

    public static void TooltipOnHover(string text, float wrapWidth = 420f)
    {
        if (!ImGui.IsItemHovered() || string.IsNullOrWhiteSpace(text))
            return;

        DrawWrappedTooltip(text, wrapWidth);
    }

    public static void DrawWrappedTooltip(string text, float wrapWidth = 420f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        ImGui.BeginTooltip();
        var safeWrapWidth = Math.Clamp(wrapWidth, 220f, 520f);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + safeWrapWidth);
        ImGui.TextUnformatted(WrapTooltipText(text));
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void HelpMarker(string text, float wrapWidth = 420f)
    {
        ImGui.TextDisabled("(?)");
        TooltipOnHover(text, wrapWidth);
    }

    public static bool CheckboxWithHelp(string label, ref bool value, string tooltip, float wrapWidth = 420f)
    {
        var changed = ImGui.Checkbox(label, ref value);
        ImGui.SameLine();
        HelpMarker(tooltip, wrapWidth);
        return changed;
    }

    private static string WrapTooltipText(string text, int characterLimit = 200)
    {
        if (text.Length <= characterLimit || text.Contains('\n'))
            return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > characterLimit)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = current.Length == 0 ? word : $"{current} {word}";
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return string.Join(Environment.NewLine, lines);
    }

    public static void OpenSupportLink()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "https://ko-fi.com/airitsukino", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "BarManager failed to open Ko-Fi link.");
        }
    }

    private static void DrawSupportButtonText(Vector2 min, Vector2 max, string text)
    {
        var draw = ImGui.GetWindowDrawList();
        var textSize = ImGui.CalcTextSize(text);
        var textX = min.X + 40f;
        var textY = min.Y + ((max.Y - min.Y - textSize.Y) * 0.5f);
        var color = ImGui.GetColorU32(new Vector4(0.98f, 0.95f, 1.00f, 1f));
        draw.AddText(new Vector2(textX, textY), color, text);
    }

    private static void DrawKofiCupIcon(Vector2 min, Vector2 max)
    {
        var draw = ImGui.GetWindowDrawList();
        var centerY = (min.Y + max.Y) * 0.5f;
        var cupMin = new Vector2(min.X + 11f, centerY - 5f);
        var cupMax = new Vector2(min.X + 25f, centerY + 5f);
        var color = ImGui.GetColorU32(new Vector4(0.96f, 0.91f, 1.00f, 1f));
        var shadow = ImGui.GetColorU32(new Vector4(0.20f, 0.07f, 0.36f, 0.9f));
        var heart = ImGui.GetColorU32(new Vector4(0.78f, 0.28f, 1.00f, 1f));

        draw.AddRectFilled(cupMin + new Vector2(1f, 1f), cupMax + new Vector2(1f, 1f), shadow, 3f);
        draw.AddRectFilled(cupMin, cupMax, color, 3f);
        draw.AddRect(new Vector2(cupMax.X - 1f, centerY - 3.5f), new Vector2(cupMax.X + 5.5f, centerY + 3.5f), color, 4f, 0, 2f);
        draw.AddCircleFilled(new Vector2(cupMin.X + 4.7f, centerY - 0.8f), 1.8f, heart);
        draw.AddCircleFilled(new Vector2(cupMin.X + 7.9f, centerY - 0.8f), 1.8f, heart);
        draw.AddTriangleFilled(new Vector2(cupMin.X + 3.0f, centerY + 0.2f), new Vector2(cupMin.X + 9.6f, centerY + 0.2f), new Vector2(cupMin.X + 6.3f, centerY + 4.2f), heart);
    }
}
