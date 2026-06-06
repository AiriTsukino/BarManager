using BarManager.Models;
using BarManager.Services;
using BarManager.UI.Components;
using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace BarManager.UI.Tabs;

internal sealed class AuditTab
{
    private readonly Configuration config;
    private readonly PersistenceService persistence;

    public AuditTab(Configuration config, PersistenceService persistence)
    {
        this.config = config;
        this.persistence = persistence;
    }

    public void Draw()
    {
        config.EnsureDefaults();
        var venue = config.ActiveVenue;
        var audit = config.CurrentAudit;

        if (ImGui.BeginChild("##AuditScroll", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            UiHelpers.SectionTitle("Starting Gil");
            var bartender = audit.BartenderName;
            if (ImGui.InputText("Bartender name", ref bartender, 128)) { audit.BartenderName = bartender; persistence.SaveNow(); }
            var myGil = audit.MyStartingGil;
            if (UiHelpers.InputIntGil("My starting gil", ref myGil, 10000)) { audit.MyStartingGil = myGil; persistence.SaveNow(); }
            var venueGil = audit.VenuePrizeGil;
            if (UiHelpers.InputIntGil("Venue prize gil", ref venueGil, 10000)) { audit.VenuePrizeGil = venueGil; persistence.SaveNow(); }
            UiHelpers.TooltipOnHover("Gil given to bartender from venue management to use for prize payouts.");
            ImGui.TextColored(BarManagerTheme.Gold, $"Total starting gil: {UiHelpers.Gil(audit.MyStartingGil + audit.VenuePrizeGil)}");

            ImGui.Spacing();
            UiHelpers.SectionTitle("Drinks Sold");
            if (venue.Drinks.Any(d => d.Enabled))
            {
                var enabledDrinks = venue.Drinks.Where(d => d.Enabled).ToList();
                var drinkColumnWidth = enabledDrinks
                    .Select(d => ImGui.CalcTextSize(d.Name).X)
                    .DefaultIfEmpty(160f)
                    .Max() + 24f;
                drinkColumnWidth = Math.Clamp(drinkColumnWidth, 150f, 300f);

                const float quantityColumnWidth = 410f;
                const float totalColumnWidth = 120f;

                if (ImGui.BeginTable("##AuditDrinkSales", 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("Drink", ImGuiTableColumnFlags.WidthFixed, drinkColumnWidth);
                    ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, quantityColumnWidth);
                    ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, totalColumnWidth);
                    ImGui.TableHeadersRow();

                    foreach (var drink in enabledDrinks)
                    {
                        var sale = GetSale(audit, drink.Id);
                        var billable = ReportService.BillableDrinkCount(audit, sale);
                        ImGui.PushID(drink.Id.ToString());
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(drink.Name);

                        ImGui.TableNextColumn();
                        DrawQuantityControls(sale);

                        ImGui.TableNextColumn();
                        ImGui.TextColored(BarManagerTheme.Green, UiHelpers.Gil(billable * drink.Price));
                        ImGui.PopID();
                    }

                    ImGui.EndTable();
                }
            }
            else
            {
                UiHelpers.TextWrappedMuted("No enabled drinks are in this venue menu yet. Add drinks from Settings > Drink Menu.");
            }
            ImGui.TextColored(BarManagerTheme.Green, $"Drink sales total: {UiHelpers.Gil(ReportService.DrinkSales(venue, audit))}");

            ImGui.Spacing();
            UiHelpers.SectionTitle("Tips & Bar Buyout");
            var tips = audit.Tips;
            if (UiHelpers.InputIntGil("Tips", ref tips, 10000)) { audit.Tips = tips; persistence.SaveNow(); }
            var active = audit.BarBuyoutActive;
            if (ImGui.Checkbox("Bar buyout active", ref active))
            {
                audit.BarBuyoutActive = active;
                if (active)
                {
                    foreach (var sale in audit.DrinkSales)
                        sale.CountBeforeBuyout = sale.Count;
                }
                persistence.SaveNow();
            }
            if (audit.BarBuyoutActive)
            {
                var typeIndex = audit.BarBuyoutType switch { "hourly" => 1, "custom" => 2, _ => 0 };
                if (ImGui.Combo("Buyout type", ref typeIndex, new[] { "Full night", "Hourly", "Custom" }, 3))
                {
                    audit.BarBuyoutType = typeIndex switch { 1 => "hourly", 2 => "custom", _ => "full" };
                    persistence.SaveNow();
                }
                var buyer = audit.BarBuyoutBuyer;
                if (ImGui.InputText("Buyer", ref buyer, 128)) { audit.BarBuyoutBuyer = buyer; persistence.SaveNow(); }
                if (audit.BarBuyoutType == "hourly")
                {
                    var hours = audit.BarBuyoutHours;
                    if (ImGui.InputFloat("Hours", ref hours, 1f, 4f)) { audit.BarBuyoutHours = Math.Max(0, hours); persistence.SaveNow(); }
                }
                if (audit.BarBuyoutType == "custom")
                {
                    var custom = audit.BarBuyoutCustomPrice;
                    if (UiHelpers.InputIntGil("Custom buyout price", ref custom, 100000)) { audit.BarBuyoutCustomPrice = custom; persistence.SaveNow(); }
                }

                var currentBuyoutValue = ReportService.CurrentBuyoutValue(venue, audit);
                ImGui.TextColored(BarManagerTheme.Green, $"Current buyout value: {UiHelpers.Gil(currentBuyoutValue)}");
                if (ImGui.Button("Submit buyout"))
                {
                    SubmitCurrentBuyout(venue, audit);
                    persistence.SaveNow();
                }
                UiHelpers.TooltipOnHover("Adds this buyout to the night so multiple buyouts can be recorded in one audit. Drinks sold during the active buyout are marked as covered and will not be counted as normal billable drink sales.");
            }

            if (audit.SubmittedBuyouts.Count > 0)
            {
                ImGui.Spacing();
                UiHelpers.TextMuted("Submitted buyouts this night:");
                for (var i = 0; i < audit.SubmittedBuyouts.Count; i++)
                {
                    var buyout = audit.SubmittedBuyouts[i];
                    ImGui.PushID($"submitted-buyout-{i}");
                    ImGui.TextWrapped($"{buyout.SubmittedAt:HH:mm} - {(string.IsNullOrWhiteSpace(buyout.Buyer) ? "Unknown buyer" : buyout.Buyer)} - {buyout.DisplayType} - {UiHelpers.Gil(buyout.Total)}");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Delete"))
                    {
                        audit.SubmittedBuyouts.RemoveAt(i);
                        persistence.SaveNow();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopID();
                }
            }
            ImGui.TextColored(BarManagerTheme.Green, $"Buyout sales total: {UiHelpers.Gil(ReportService.BuyoutSales(venue, audit))}");

            ImGui.Spacing();
            UiHelpers.SectionTitle("Payouts & Ending");
            var prizes = audit.PrizesPaidOut;
            if (UiHelpers.InputIntGil("Prizes paid out", ref prizes, 10000)) { audit.PrizesPaidOut = prizes; persistence.SaveNow(); }
            var ending = audit.EndingGilEntered;
            if (UiHelpers.InputIntGil("Ending gil entered", ref ending, 10000)) { audit.EndingGilEntered = ending; persistence.SaveNow(); }
            ImGui.TextColored(BarManagerTheme.Green, $"Total gil in: {UiHelpers.Gil(ReportService.DrinkSales(venue, audit) + ReportService.BuyoutSales(venue, audit) + audit.Tips)}");
            ImGui.TextColored(BarManagerTheme.Red, $"Total gil out: {UiHelpers.Gil(audit.PrizesPaidOut)}");
            ImGui.TextColored(BarManagerTheme.Gold, $"Current jackpot: {UiHelpers.Gil(audit.JackpotCurrent)}");
            var profitLoss = ReportService.NightlyProfitLoss(audit);
            ImGui.TextColored(profitLoss >= 0 ? BarManagerTheme.Green : BarManagerTheme.Red, $"Nightly profit/loss: {UiHelpers.Gil(profitLoss)}");
            UiHelpers.TextWrappedMuted("Calculated as ending gil minus personal starting gil, venue prize gil, and tips. This replaces the old expected delta check.");

            if (ImGui.Button("Reset night")) ImGui.OpenPopup("Confirm reset night");
            if (ImGui.BeginPopupModal("Confirm reset night", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 380f);
                ImGui.TextUnformatted("This clears the current audit, drink counts, gamba sessions, and resets the current jackpot to the active venue jackpot base.");
                ImGui.PopTextWrapPos();
                ImGui.Spacing();
                if (ImGui.Button("Confirm reset", new Vector2(125f, 0f)))
                {
                    config.CurrentAudit = new BarAuditState { JackpotCurrent = venue.JackpotBase };
                    config.EnsureDefaults();
                    persistence.SaveNow();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(95f, 0f))) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }
        ImGui.EndChild();
    }

    private void DrawQuantityControls(DrinkSale sale)
    {
        var buttonSize = new Vector2(36f, 0f);
        var count = sale.Count;

        if (ImGui.Button("-20", buttonSize)) { sale.Count = Math.Max(0, sale.Count - 20); persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("-10", buttonSize)) { sale.Count = Math.Max(0, sale.Count - 10); persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("-5", buttonSize)) { sale.Count = Math.Max(0, sale.Count - 5); persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("-1", buttonSize)) { sale.Count = Math.Max(0, sale.Count - 1); persistence.SaveNow(); }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60f);
        if (ImGui.InputInt("##count", ref count)) { sale.Count = Math.Max(0, count); persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("+1", buttonSize)) { sale.Count += 1; persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("+5", buttonSize)) { sale.Count += 5; persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("+10", buttonSize)) { sale.Count += 10; persistence.SaveNow(); }
        ImGui.SameLine();
        if (ImGui.Button("+20", buttonSize)) { sale.Count += 20; persistence.SaveNow(); }
    }

    private static void SubmitCurrentBuyout(VenueProfile venue, BarAuditState audit)
    {
        var value = ReportService.CurrentBuyoutValue(venue, audit);
        audit.SubmittedBuyouts.Add(new BarBuyoutRecord
        {
            Buyer = audit.BarBuyoutBuyer,
            Type = audit.BarBuyoutType,
            Hours = audit.BarBuyoutHours,
            CustomPrice = audit.BarBuyoutCustomPrice,
            Total = value,
            SubmittedAt = DateTime.Now,
        });

        foreach (var sale in audit.DrinkSales)
        {
            var coveredThisPeriod = Math.Max(0, sale.Count - sale.CountBeforeBuyout);
            sale.CountCoveredByBuyout += coveredThisPeriod;
            sale.CountBeforeBuyout = sale.Count;
        }

        audit.BarBuyoutActive = false;
        audit.BarBuyoutBuyer = string.Empty;
        audit.BarBuyoutHours = 0;
        audit.BarBuyoutCustomPrice = 0;
    }

    private static DrinkSale GetSale(BarAuditState audit, Guid drinkId)
    {
        var sale = audit.DrinkSales.FirstOrDefault(s => s.DrinkId == drinkId);
        if (sale is not null) return sale;
        sale = new DrinkSale { DrinkId = drinkId };
        audit.DrinkSales.Add(sale);
        return sale;
    }
}
