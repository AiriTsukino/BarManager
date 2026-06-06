using System.Numerics;
using BarManager.Services;
using BarManager.UI.Components;
using BarManager.UI.Tabs;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BarManager.UI;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly PersistenceService persistence;
    private readonly Action openSettings;
    private readonly AuditTab auditTab;
    private readonly GambaDrinkTab gambaTab;
    private readonly SessionsTab sessionsTab;
    private readonly ReportTab reportTab;

    public MainWindow(Configuration config, PersistenceService persistence, Action openSettings)
        : base("BarManager###BarManagerMain")
    {
        Size = new Vector2(980, 720);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 620),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.config = config;
        this.persistence = persistence;
        this.openSettings = openSettings;
        auditTab = new AuditTab(config, persistence);
        gambaTab = new GambaDrinkTab(config, persistence);
        sessionsTab = new SessionsTab(config, persistence);
        reportTab = new ReportTab(config, persistence);
    }

    public void Dispose() => gambaTab.Dispose();

    public override void PreDraw() => BarManagerTheme.Push();
    public override void PostDraw() => BarManagerTheme.Pop();

    public override void Draw()
    {
        ImGui.TextColored(BarManagerTheme.Gold, "BarManager");
        ImGui.SameLine();
        UiHelpers.TextMuted($"  {config.ActiveVenue.Name}");

        const float settingsWidth = 94f;
        var supportWidth = UiHelpers.GetSupportButtonWidth();
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var toolbarWidth = settingsWidth + spacing + supportWidth;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > toolbarWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - toolbarWidth);

        if (ImGui.Button("Settings", new Vector2(settingsWidth, 0))) openSettings();
        ImGui.SameLine();
        UiHelpers.DrawSupportButton("main-support", supportWidth);

        ImGui.Separator();
        if (ImGui.BeginTabBar("##BarManagerTabs"))
        {
            if (ImGui.BeginTabItem("Audit")) { auditTab.Draw(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(config.ActiveVenue.Gamba.DrinkName)) { gambaTab.Draw(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Sessions")) { sessionsTab.Draw(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Report")) { reportTab.Draw(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }
}
