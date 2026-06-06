using System.Numerics;
using System.Text.RegularExpressions;
using BarManager.Models;
using BarManager.Services;
using BarManager.UI.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

namespace BarManager.UI.Tabs;

internal sealed class GambaDrinkTab : IDisposable
{
    private static readonly Regex PartyRandomRegex = new(@"^(?:(?<name>.+?)[@＠](?<world>[^:]+):?\s*)?Random!\s*(?:\((?<rangeMin>\d+)\s*-\s*(?<rangeMax>\d+)\)\s*)?(?<roll>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CombinedRandomRegex = new(@"^(?<name>.+?)[@＠](?<world>[^:]+):?\s*Random!\s*(?:\((?<rangeMin>\d+)\s*-\s*(?<rangeMax>\d+)\)\s*)?(?<roll>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ParenthesizedPartyRandomRegex = new(@"^\((?<name>[^)]+)\)\s*Random!\s*(?:\((?<rangeMin>\d+)\s*-\s*(?<rangeMax>\d+)\)\s*)?(?<roll>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NamedRandomRegex = new(@"^(?<name>[^:]+):\s*Random!\s*(?:\((?<rangeMin>\d+)\s*-\s*(?<rangeMax>\d+)\)\s*)?(?<roll>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RandomRollRegex = new(@"Random!\s*(?:\((?<rangeMin>\d+)\s*-\s*(?<rangeMax>\d+)\)\s*)?(?<roll>\d{1,4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LeadingParenthesizedNameRegex = new(@"^\((?<name>[^)]+)\)", RegexOptions.Compiled);
    private static readonly object AnnouncementLock = new();
    private static string lastGlobalAnnouncement = string.Empty;
    private static DateTime lastGlobalAnnouncementAt = DateTime.MinValue;
    private static readonly HashSet<string> PendingGlobalAnnouncements = new(StringComparer.Ordinal);

    private readonly record struct ParsedPartyRoll(string Name, string World, int Roll, int? RangeMin, int? RangeMax);

    private readonly Configuration config;
    private readonly PersistenceService persistence;
    private readonly ChatCommandService chatCommands = new();
    private readonly Queue<string> pendingPartyAnnouncements = new();
    private readonly Queue<string> pendingChatCommands = new();
    private DateTime nextPartyAnnouncementAt = DateTime.MinValue;
    private GambaSessionRecord? current;
    private string customerName = string.Empty;
    private string customerWorld = string.Empty;
    private int drinks = 1;
    private int rollInput;
    private string pasteRolls = string.Empty;
    private int rollsRemaining;
    private string status = "Live party-chat roll tracking is ready.";
    private string lastPartyRollFingerprint = string.Empty;
    private DateTime lastPartyRollAt = DateTime.MinValue;
    private int lastAnnouncedRollsRemaining = int.MinValue;
    private bool awaitingBartenderBonusRoll;
    private DateTime awaitingBartenderBonusRollSince = DateTime.MinValue;
    private int queuedBartenderBonusRolls;
    private int pendingBartenderBonusResults;
    private DateTime nextBartenderBonusCommandAt = DateTime.MinValue;
    private DateTime lastAutomaticBartenderBonusCommandAt = DateTime.MinValue;
    private bool autoEndAfterBartenderBonusRoll;
    private bool resolvingCustomerRollWasLocalPlayer;

    public GambaDrinkTab(Configuration config, PersistenceService persistence)
    {
        this.config = config;
        this.persistence = persistence;
        DalamudServices.ChatGui.ChatMessage += OnChatMessage;
        DalamudServices.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        DalamudServices.ChatGui.ChatMessage -= OnChatMessage;
        DalamudServices.Framework.Update -= OnFrameworkUpdate;
    }

    public void Draw()
    {
        var venue = config.ActiveVenue;
        var audit = config.CurrentAudit;
        var gamba = venue.Gamba;

        if (ImGui.BeginChild("##GambaScroll", new Vector2(0, 0), false, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            UiHelpers.Header(gamba.DrinkName, $"Start a live session, track paid rolls, and resolve /dice {gamba.MaxRoll} party-chat results for the selected customer only. Plain /dice is only accepted when max roll is 999.");
            ImGui.Columns(2, "##GambaColumns", true);

            if (UiHelpers.BeginCard("##GambaSessionCard", new Vector2(0, current is null ? 245f : 330f)))
            {
                UiHelpers.SectionTitle(current is null ? "New Session" : "Live Session");
                ImGui.InputText("Customer", ref customerName, 128);
                ImGui.InputText("World", ref customerWorld, 64);
                ImGui.SameLine();
                if (ImGui.Button("Use target")) UseCurrentTarget();
                ImGui.InputInt($"{gamba.DrinkName}s", ref drinks);
                drinks = Math.Clamp(drinks, 1, 500);
                UiHelpers.TextWrappedMuted($"Rolls purchased: {drinks * gamba.RollsPerDrink:N0}");
                if (current is null)
                {
                    if (ImGui.Button("Start Session", new Vector2(160, 0))) StartSession(gamba);
                }
                else
                {
                    if (ImGui.BeginTable("##liveSessionSummary", 2, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("left", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthStretch);

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextColored(BarManagerTheme.Muted, "Player");
                        ImGui.TextColored(BarManagerTheme.Gold, current.CustomerDisplay);
                        ImGui.TableNextColumn();
                        ImGui.TextColored(BarManagerTheme.Muted, "Rolls remaining");
                        ImGui.Text($"{rollsRemaining:N0} / {current.RollsAllowed:N0}");

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TextColored(BarManagerTheme.Muted, "Session payout");
                        ImGui.TextColored(BarManagerTheme.Green, UiHelpers.Gil(current.TotalPayout));
                        ImGui.TableNextColumn();
                        ImGui.TextColored(BarManagerTheme.Muted, "Jackpot");
                        ImGui.TextColored(BarManagerTheme.Gold, UiHelpers.Gil(audit.JackpotCurrent));

                        ImGui.EndTable();
                    }

                    DrawActiveBonusStatus(gamba);
                    if (ImGui.Button("End Session & Save")) EndSession();
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel Active Session")) { current = null; rollsRemaining = 0; lastAnnouncedRollsRemaining = int.MinValue; lastPartyRollFingerprint = string.Empty; awaitingBartenderBonusRoll = false; queuedBartenderBonusRolls = 0; pendingBartenderBonusResults = 0; nextBartenderBonusCommandAt = DateTime.MinValue; lastAutomaticBartenderBonusCommandAt = DateTime.MinValue; autoEndAfterBartenderBonusRoll = false; status = "Active session cancelled."; }
                }
            }
            UiHelpers.EndCard();

            if (UiHelpers.BeginCard("##ManualRollCard", new Vector2(0, 0)))
            {
                UiHelpers.SectionTitle("Manual / Paste Entry");
                ImGui.BeginDisabled(current is null);
                ImGui.SetNextItemWidth(120);
                ImGui.InputInt("Roll", ref rollInput);
                ImGui.SameLine();
                if (ImGui.Button("Resolve Roll")) ResolveRoll(rollInput, "manual");
                if (gamba.AllowPasteImport)
                {
                    ImGui.InputTextMultiline("Paste rolls", ref pasteRolls, 4096, new Vector2(-1, 72));
                    if (ImGui.Button("Import Paste"))
                    {
                        foreach (Match m in Regex.Matches(pasteRolls, @"\d+"))
                        {
                            if (int.TryParse(m.Value, out var roll) && current is not null && rollsRemaining > 0)
                                ResolveRoll(roll, "paste");
                        }
                        pasteRolls = string.Empty;
                    }
                }
                ImGui.EndDisabled();
                UiHelpers.TextWrappedMuted($"Rolls are auto tracked in party chat for the active customer when they use /dice {gamba.MaxRoll}. Plain /dice only counts when max roll is 999.");
                UiHelpers.TextWrappedMuted(status);
            }
            UiHelpers.EndCard();

            ImGui.NextColumn();

            if (UiHelpers.BeginCard("##GambaHistoryCard", new Vector2(0, 0)))
            {
                UiHelpers.SectionTitle("Roll History");
                if (current is null || current.Rolls.Count == 0)
                {
                    UiHelpers.TextWrappedMuted("Live roll results appear here while a session is active.");
                }
                else if (ImGui.BeginTable("##liveRolls", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("#");
                    ImGui.TableSetupColumn("Roll");
                    ImGui.TableSetupColumn("Tier");
                    ImGui.TableSetupColumn("Payout");
                    ImGui.TableSetupColumn("Jackpot +");
                    ImGui.TableHeadersRow();
                    foreach (var (record, index) in current.Rolls.Select((r, i) => (r, i + 1)))
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn(); ImGui.Text(index.ToString());
                        ImGui.TableNextColumn(); ImGui.Text(record.Roll.ToString());
                        ImGui.TableNextColumn(); ImGui.TextColored(GetTierColor(record.Tier), record.Tier);
                        ImGui.TableNextColumn();
                        var payoutText = UiHelpers.Gil(record.Payout);
                        if (!string.IsNullOrWhiteSpace(record.BonusName) && record.BonusMultiplier > 1f)
                            payoutText += $" ({record.BonusName} x{record.BonusMultiplier:0.##})";
                        ImGui.TextColored(BarManagerTheme.Green, payoutText);
                        ImGui.TableNextColumn(); ImGui.TextColored(BarManagerTheme.Gold, UiHelpers.Gil(record.JackpotContribution));
                    }
                    ImGui.EndTable();
                }
            }
            UiHelpers.EndCard();
            ImGui.Columns(1);
        }
        ImGui.EndChild();
    }

    private void StartSession(GambaSettings gamba)
    {
        current = new GambaSessionRecord
        {
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Unknown" : customerName.Trim(),
            CustomerWorld = customerWorld.Trim(),
            DrinksPurchased = drinks,
            RollsAllowed = drinks * gamba.RollsPerDrink,
        };
        rollsRemaining = current.RollsAllowed;
        lastPartyRollFingerprint = string.Empty;
        lastPartyRollAt = DateTime.MinValue;
        lastAnnouncedRollsRemaining = int.MinValue;
        awaitingBartenderBonusRoll = false;
        queuedBartenderBonusRolls = 0;
        pendingBartenderBonusResults = 0;
        nextBartenderBonusCommandAt = DateTime.MinValue;
        lastAutomaticBartenderBonusCommandAt = DateTime.MinValue;
        autoEndAfterBartenderBonusRoll = false;
        status = $"Tracking party-chat /dice {gamba.MaxRoll} rolls for {current.CustomerDisplay}. Plain /dice only counts when max roll is 999. Bartender bonus checks start after the customer rolls.";
    }

    private void ResolveRoll(int roll, string source)
    {
        if (current is null) return;
        var venue = config.ActiveVenue;
        var gamba = venue.Gamba;
        if (roll < gamba.MinRoll || roll > gamba.MaxRoll)
        {
            status = $"Ignored {source} roll {roll}; expected {gamba.MinRoll}-{gamba.MaxRoll}.";
            return;
        }
        if (rollsRemaining <= 0)
        {
            status = "Ignored roll; the active session has no paid rolls remaining.";
            return;
        }

        var jackpotBefore = config.CurrentAudit.JackpotCurrent;
        rollsRemaining--;
        var result = GambaEngine.Resolve(roll, jackpotBefore, gamba);
        var basePayout = result.Payout;
        var payout = basePayout;
        var appliedBonusName = string.Empty;
        var appliedMultiplier = 1f;
        var isWin = result.JackpotWin || basePayout > 0;

        if (isWin)
        {
            if (current.LossStreakBonusActive)
            {
                appliedBonusName = string.IsNullOrWhiteSpace(gamba.LossStreakBonusName) ? "Loss Streak Bonus" : gamba.LossStreakBonusName.Trim();
                appliedMultiplier = MathF.Max(1f, gamba.LossStreakBonusMultiplier);
                if (!result.JackpotWin || gamba.LossStreakBonusAppliesToJackpot)
                    payout = ApplyBonusMultiplier(basePayout, appliedMultiplier);
                current.LossStreakBonusActive = false;
                current.LossStreakBonusTurnsRemaining = 0;
                current.ConsecutiveLosses = 0;
            }
            else if (current.BartenderRollBonusActive)
            {
                appliedBonusName = string.IsNullOrWhiteSpace(gamba.BartenderRollBonusName) ? "Bartender Bonus" : gamba.BartenderRollBonusName.Trim();
                appliedMultiplier = MathF.Max(1f, gamba.BartenderRollBonusMultiplier);
                if (!result.JackpotWin || gamba.BartenderRollBonusAppliesToJackpot)
                    payout = ApplyBonusMultiplier(basePayout, appliedMultiplier);
                current.BartenderRollBonusActive = false;
                current.BartenderRollBonusTurnsRemaining = 0;
            }
        }

        var contribution = CalculateJackpotContribution(venue, gamba);

        current.Rolls.Add(new GambaRollRecord
        {
            Roll = roll,
            Tier = result.Tier,
            Payout = payout,
            JackpotWin = result.JackpotWin,
            FreeRoll = result.FreeRoll,
            JackpotContribution = contribution,
            BonusName = appliedBonusName,
            BonusMultiplier = appliedMultiplier,
            BasePayout = basePayout,
        });

        config.CurrentAudit.PrizesPaidOut += payout;
        if (result.JackpotWin)
            config.CurrentAudit.JackpotCurrent = venue.JackpotBase;
        else if (contribution > 0)
            config.CurrentAudit.JackpotCurrent += contribution;

        if (result.FreeRoll)
            rollsRemaining++;

        UpdateBonusStateAfterRoll(gamba, isWin);

        rollInput = 0;
        var bonusText = string.IsNullOrWhiteSpace(appliedBonusName) ? string.Empty : $" with {appliedBonusName} x{appliedMultiplier:0.##}";
        status = $"Resolved {roll} from {source}: {result.Tier}, {UiHelpers.Gil(payout)}{bonusText}. Rolls left: {rollsRemaining:N0}.";
        persistence.SaveNow();

        if (result.JackpotWin)
        {
            HandleJackpotWin(gamba, jackpotBefore, payout);
            return;
        }

        AnnounceRollsLeftIfNeeded(gamba);

        var shouldAutoEnd = gamba.AutoEndWhenRollsUsed && rollsRemaining <= 0;
        QueueAutomaticBartenderBonusRoll(gamba);

        if (shouldAutoEnd)
        {
            if (queuedBartenderBonusRolls > 0 || pendingBartenderBonusResults > 0 || awaitingBartenderBonusRoll)
                autoEndAfterBartenderBonusRoll = true;
            else
                EndSession();
        }
    }

    private static int ApplyBonusMultiplier(int payout, float multiplier)
    {
        if (payout <= 0)
            return payout;
        return Math.Max(0, (int)MathF.Round(payout * MathF.Max(1f, multiplier)));
    }

    private void UpdateBonusStateAfterRoll(GambaSettings gamba, bool isWin)
    {
        if (current is null) return;

        if (isWin)
        {
            current.ConsecutiveLosses = 0;
            return;
        }

        if (current.LossStreakBonusActive && gamba.LossStreakBonusDurationTurns.HasValue)
        {
            current.LossStreakBonusTurnsRemaining--;
            if (current.LossStreakBonusTurnsRemaining <= 0)
            {
                status = $"{gamba.LossStreakBonusName} expired after {FormatTurnCount(gamba.LossStreakBonusDurationTurns)} without a win.";
                current.LossStreakBonusActive = false;
                current.LossStreakBonusTurnsRemaining = 0;
                current.ConsecutiveLosses = 0;
            }
        }

        current.ConsecutiveLosses++;

        if (current.BartenderRollBonusActive && gamba.BartenderRollBonusDurationTurns.HasValue)
        {
            current.BartenderRollBonusTurnsRemaining--;
            if (current.BartenderRollBonusTurnsRemaining <= 0)
            {
                status = $"{gamba.BartenderRollBonusName} expired after {FormatTurnCount(gamba.BartenderRollBonusDurationTurns)} without a win.";
                current.BartenderRollBonusActive = false;
                current.BartenderRollBonusTurnsRemaining = 0;
            }
        }

        if (gamba.LossStreakBonusEnabled && !current.LossStreakBonusActive && !current.BartenderRollBonusActive)
        {
            var threshold = Math.Max(1, gamba.LossStreakThreshold);
            if (current.ConsecutiveLosses >= threshold)
            {
                current.LossStreakBonusActive = true;
                current.LossStreakBonusTurnsRemaining = Math.Max(0, gamba.LossStreakBonusDurationTurns ?? 0);
                AnnounceBonusActivated(gamba.LossStreakBonusName, gamba.LossStreakBonusMultiplier, gamba.LossStreakBonusAnnouncement, FormatBonusDuration(gamba.LossStreakBonusDurationTurns));
            }
        }
    }

    private void DrawActiveBonusStatus(GambaSettings gamba)
    {
        if (current is null) return;

        if (current.LossStreakBonusActive)
            ImGui.TextColored(BarManagerTheme.Gold, $"Active bonus: {SafeBonusName(gamba.LossStreakBonusName, "Loss Streak Bonus")} x{MathF.Max(1f, gamba.LossStreakBonusMultiplier):0.##} {FormatBonusDuration(gamba.LossStreakBonusDurationTurns)}");
        else if (current.BartenderRollBonusActive)
            ImGui.TextColored(BarManagerTheme.Gold, $"Active bonus: {SafeBonusName(gamba.BartenderRollBonusName, "Bartender Bonus")} x{MathF.Max(1f, gamba.BartenderRollBonusMultiplier):0.##} {FormatBonusDuration(gamba.BartenderRollBonusDurationTurns)}");
        else if (awaitingBartenderBonusRoll)
            ImGui.TextColored(BarManagerTheme.Muted, "Waiting for bartender bonus roll...");
        else if (gamba.LossStreakBonusEnabled)
            UiHelpers.TextWrappedMuted($"Loss streak: {current.ConsecutiveLosses:N0}/{Math.Max(1, gamba.LossStreakThreshold):N0}");
    }

    private void QueueAutomaticBartenderBonusRoll(GambaSettings gamba)
    {
        if (current is null || !gamba.BartenderRollBonusEnabled) return;

        queuedBartenderBonusRolls++;

        var now = DateTime.Now;
        // If the bartender is self-testing as the customer, the same local client has
        // just sent the customer /dice command. FFXIV can silently ignore an immediate
        // second /dice from the same client, so wait just long enough for the game dice
        // cooldown instead of marking the roll as sent too early. Real-customer rolls can
        // be answered almost immediately because the bartender did not just roll.
        var firstAllowedSend = resolvingCustomerRollWasLocalPlayer
            ? now.AddMilliseconds(1100)
            : now.AddMilliseconds(75);

        if (lastAutomaticBartenderBonusCommandAt != DateTime.MinValue)
            firstAllowedSend = MaxDate(firstAllowedSend, lastAutomaticBartenderBonusCommandAt.AddMilliseconds(1100));

        if (nextBartenderBonusCommandAt == DateTime.MinValue || nextBartenderBonusCommandAt < now)
            nextBartenderBonusCommandAt = firstAllowedSend;
        else
            nextBartenderBonusCommandAt = MinDate(nextBartenderBonusCommandAt, firstAllowedSend);

        var delaySeconds = Math.Max(0, (nextBartenderBonusCommandAt - now).TotalSeconds);
        status = delaySeconds >= 0.5
            ? $"Queued bartender bonus roll {queuedBartenderBonusRolls:N0}. BarManager will send /dice party {Math.Max(2, gamba.BartenderRollMax):N0} in about {delaySeconds:0.0}s."
            : $"Queued bartender bonus roll {queuedBartenderBonusRolls:N0}. BarManager will send /dice party {Math.Max(2, gamba.BartenderRollMax):N0} now.";
    }

    private static DateTime MaxDate(DateTime a, DateTime b) => a >= b ? a : b;
    private static DateTime MinDate(DateTime a, DateTime b) => a <= b ? a : b;

    private void TrySendQueuedBartenderBonusRoll(GambaSettings gamba)
    {
        if (current is null || !gamba.BartenderRollBonusEnabled)
        {
            queuedBartenderBonusRolls = 0;
            pendingBartenderBonusResults = 0;
            awaitingBartenderBonusRoll = false;
            return;
        }

        if (queuedBartenderBonusRolls <= 0 || DateTime.Now < nextBartenderBonusCommandAt)
            return;

        var max = Math.Max(2, gamba.BartenderRollMax);
        queuedBartenderBonusRolls--;
        pendingBartenderBonusResults++;
        awaitingBartenderBonusRoll = pendingBartenderBonusResults > 0;
        awaitingBartenderBonusRollSince = DateTime.Now;
        lastAutomaticBartenderBonusCommandAt = DateTime.Now;
        nextBartenderBonusCommandAt = DateTime.Now.AddMilliseconds(1100);
        status = $"Automatically sending /dice party {max} for bartender bonus. A roll of 1 activates {SafeBonusName(gamba.BartenderRollBonusName, "Bartender Bonus")}.";
        if (!chatCommands.Send($"/dice party {max}"))
        {
            pendingBartenderBonusResults = Math.Max(0, pendingBartenderBonusResults - 1);
            awaitingBartenderBonusRoll = pendingBartenderBonusResults > 0;
            nextBartenderBonusCommandAt = DateTime.Now.AddMilliseconds(1100);
            status = $"Could not send bartender bonus roll: {chatCommands.LastError}";
        }
        else
        {
            status = pendingBartenderBonusResults > 1
                ? $"Sent /dice party {max} for bartender bonus. Waiting for {pendingBartenderBonusResults:N0} bartender roll results."
                : $"Sent /dice party {max} for bartender bonus. Waiting for the bartender roll result.";
        }
    }

    private void ActivateBartenderBonus(GambaSettings gamba)
    {
        if (current is null || current.LossStreakBonusActive || current.BartenderRollBonusActive) return;

        current.BartenderRollBonusActive = true;
        current.BartenderRollBonusTurnsRemaining = Math.Max(0, gamba.BartenderRollBonusDurationTurns ?? 0);
        AnnounceBonusActivated(gamba.BartenderRollBonusName, gamba.BartenderRollBonusMultiplier, gamba.BartenderRollBonusAnnouncement, FormatBonusDuration(gamba.BartenderRollBonusDurationTurns));
        persistence.SaveNow();
    }


    private static string FormatBonusDuration(int? turns)
    {
        return turns.HasValue && turns.Value > 0
            ? $"for the next {turns.Value:N0} {(turns.Value == 1 ? "roll" : "rolls")} or until the next win"
            : "until your next win";
    }

    private static string FormatTurnCount(int? turns)
    {
        if (!turns.HasValue || turns.Value <= 0)
            return "unlimited turns";

        return $"{turns.Value:N0} {(turns.Value == 1 ? "turn" : "turns")}";
    }

    private void AnnounceBonusActivated(string configuredName, float multiplier, string template, string durationText = "until your next win")
    {
        if (current is null) return;
        var name = SafeBonusName(configuredName, "Bonus");
        var text = BuildBonusAnnouncement(template, current.CustomerName, name, MathF.Max(1f, multiplier), durationText);
        TryQueueAnnouncement(text);
    }

    private static string BuildBonusAnnouncement(string template, string player, string bonus, float multiplier, string durationText)
    {
        var text = string.IsNullOrWhiteSpace(template)
            ? "{player}, {bonus} is active! Your next win is multiplied by x{multiplier} {duration}."
            : template.Trim();

        return text
            .Replace("{player}", player)
            .Replace("{bonus}", bonus)
            .Replace("{multiplier}", multiplier.ToString("0.##"))
            .Replace("{duration}", durationText);
    }

    private static string SafeBonusName(string configuredName, string fallback) => string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName.Trim();

    private void HandleJackpotWin(GambaSettings gamba, int jackpotBefore, int payout)
    {
        if (current is null)
            return;

        if (gamba.JackpotShoutoutEnabled)
        {
            var shoutText = BuildJackpotShoutout(gamba, current.CustomerName, payout, jackpotBefore, config.ActiveVenue.Name);
            QueueChatCommand(BuildJackpotChatCommand(gamba.JackpotShoutoutChannel, shoutText));
        }

        if (gamba.AutoEndOnJackpotWin)
        {
            TryQueueAnnouncement($"{current.CustomerName}, stop rolling! You have won the jackpot!");
            rollsRemaining = 0;
            queuedBartenderBonusRolls = 0;
            pendingBartenderBonusResults = 0;
            awaitingBartenderBonusRoll = false;
            autoEndAfterBartenderBonusRoll = false;
            status = $"Jackpot won by {current.CustomerDisplay}. Session auto-ended and saved.";
            EndSession();
        }
    }

    private static string BuildJackpotShoutout(GambaSettings gamba, string player, int payout, int jackpot, string venue)
    {
        var template = string.IsNullOrWhiteSpace(gamba.JackpotShoutoutMessage)
            ? "Congratulations {player}! They just won the jackpot for {payout} gil!"
            : gamba.JackpotShoutoutMessage.Trim();

        return template
            .Replace("{player}", player)
            .Replace("{payout}", UiHelpers.Gil(Math.Max(0, payout)))
            .Replace("{jackpot}", UiHelpers.Gil(Math.Max(0, jackpot)))
            .Replace("{venue}", string.IsNullOrWhiteSpace(venue) ? "the venue" : venue.Trim());
    }

    private static string BuildJackpotChatCommand(string channel, string text)
    {
        var normalized = (channel ?? string.Empty).Trim().TrimStart('/').ToLowerInvariant() switch
        {
            "s" or "say" => "say",
            "sh" or "shout" => "shout",
            "y" or "yell" => "yell",
            _ => "yell",
        };

        return $"/{normalized} {text}";
    }

    private int CalculateJackpotContribution(VenueProfile venue, GambaSettings gamba)
    {
        if (!gamba.AddRollPricePercentToJackpot || gamba.JackpotContributionPercent <= 0)
            return 0;

        var gambaDrink = venue.Drinks.FirstOrDefault(d => d.IsGambaDrink) ?? venue.Drinks.FirstOrDefault(d => d.Name.Equals(gamba.DrinkName, StringComparison.OrdinalIgnoreCase));
        var drinkPrice = Math.Max(0, gambaDrink?.Price ?? 0);
        var rollPrice = gamba.RollsPerDrink <= 0 ? drinkPrice : drinkPrice / (float)gamba.RollsPerDrink;
        return Math.Max(0, (int)MathF.Round(rollPrice * (gamba.JackpotContributionPercent / 100f)));
    }

    private void AnnounceRollsLeftIfNeeded(GambaSettings gamba)
    {
        if (current is null || !gamba.AnnounceRollsLeft)
            return;

        if (rollsRemaining == lastAnnouncedRollsRemaining)
            return;

        var interval = Math.Clamp(gamba.AnnounceEveryRolls, 1, 50);
        if (rollsRemaining > 0 && rollsRemaining % interval != 0)
            return;

        var text = rollsRemaining <= 0
            ? $"{current.CustomerName}, you have no rolls remaining."
            : $"{current.CustomerName}, you have {rollsRemaining:N0} roll{(rollsRemaining == 1 ? string.Empty : "s")} remaining.";

        if (!TryQueueAnnouncement(text))
            return;

        lastAnnouncedRollsRemaining = rollsRemaining;
    }

    private bool TryQueueAnnouncement(string text)
    {
        lock (AnnouncementLock)
        {
            var now = DateTime.Now;
            if (text == lastGlobalAnnouncement && (now - lastGlobalAnnouncementAt).TotalSeconds < 10)
                return false;

            if (PendingGlobalAnnouncements.Contains(text))
                return false;

            PendingGlobalAnnouncements.Add(text);
            pendingPartyAnnouncements.Enqueue(text);
            return true;
        }
    }

    private bool QueueChatCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        lock (AnnouncementLock)
        {
            var now = DateTime.Now;
            if (command == lastGlobalAnnouncement && (now - lastGlobalAnnouncementAt).TotalSeconds < 10)
                return false;

            if (PendingGlobalAnnouncements.Contains(command))
                return false;

            PendingGlobalAnnouncements.Add(command);
            pendingChatCommands.Enqueue(command);
            return true;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (awaitingBartenderBonusRoll && pendingBartenderBonusResults > 0 && (DateTime.Now - awaitingBartenderBonusRollSince).TotalSeconds > 30)
        {
            pendingBartenderBonusResults = 0;
            awaitingBartenderBonusRoll = false;
            status = "Bartender bonus roll timed out.";
            if (autoEndAfterBartenderBonusRoll && queuedBartenderBonusRolls <= 0)
                EndSession();
        }

        TrySendQueuedBartenderBonusRoll(config.ActiveVenue.Gamba);

        if ((pendingPartyAnnouncements.Count == 0 && pendingChatCommands.Count == 0) || DateTime.Now < nextPartyAnnouncementAt)
            return;

        if (pendingChatCommands.Count > 0)
        {
            var command = pendingChatCommands.Dequeue();
            lock (AnnouncementLock)
            {
                PendingGlobalAnnouncements.Remove(command);
                var now = DateTime.Now;
                if (command == lastGlobalAnnouncement && (now - lastGlobalAnnouncementAt).TotalSeconds < 10)
                    return;

                lastGlobalAnnouncement = command;
                lastGlobalAnnouncementAt = now;
            }

            chatCommands.Send(command);
            nextPartyAnnouncementAt = DateTime.Now.AddMilliseconds(750);
            return;
        }

        var text = pendingPartyAnnouncements.Dequeue();
        lock (AnnouncementLock)
        {
            PendingGlobalAnnouncements.Remove(text);
            var now = DateTime.Now;
            if (text == lastGlobalAnnouncement && (now - lastGlobalAnnouncementAt).TotalSeconds < 10)
                return;

            lastGlobalAnnouncement = text;
            lastGlobalAnnouncementAt = now;
        }

        chatCommands.Send($"/p {text}");
        nextPartyAnnouncementAt = DateTime.Now.AddMilliseconds(750);
    }

    private void EndSession()
    {
        if (current is null) return;
        current.EndedAt = DateTime.Now;
        config.CurrentAudit.GambaSessions.Add(current);
        QueueSessionPayoutAnnouncement(current, config.ActiveVenue);
        status = $"Saved session for {current.CustomerDisplay}.";
        current = null;
        rollsRemaining = 0;
        lastAnnouncedRollsRemaining = int.MinValue;
        awaitingBartenderBonusRoll = false;
        queuedBartenderBonusRolls = 0;
        pendingBartenderBonusResults = 0;
        nextBartenderBonusCommandAt = DateTime.MinValue;
        lastAutomaticBartenderBonusCommandAt = DateTime.MinValue;
        autoEndAfterBartenderBonusRoll = false;
        lastPartyRollFingerprint = string.Empty;
        lastPartyRollAt = DateTime.MinValue;
        customerName = string.Empty;
        customerWorld = string.Empty;
        drinks = 1;
        persistence.SaveNow();
    }

    private void QueueSessionPayoutAnnouncement(GambaSessionRecord session, VenueProfile venue)
    {
        var payout = Math.Max(0, session.TotalPayout);
        var rollPrice = CalculateGambaRollPrice(venue);
        var totalBuyIn = CalculateSessionBuyIn(session, venue, rollPrice);
        var extraRolls = rollPrice > 0 ? payout / rollPrice : 0;
        var extraCashout = rollPrice > 0 ? payout % rollPrice : payout;
        var sameSessionCashout = Math.Max(0, payout - totalBuyIn);
        var buyBackGil = Math.Max(0, totalBuyIn - payout);
        var sameRollText = session.RollsAllowed == 1 ? "roll" : "rolls";
        var extraRollText = extraRolls == 1 ? "roll" : "rolls";

        string text;
        if (rollPrice <= 0)
        {
            text = $"{session.CustomerName}, your session payout is {UiHelpers.Gil(payout)}!";
        }
        else if (payout >= totalBuyIn && session.RollsAllowed > 0)
        {
            text = $"{session.CustomerName}, your session payout is {UiHelpers.Gil(payout)}! That is enough for another {session.RollsAllowed:N0} {sameRollText} plus {UiHelpers.Gil(sameSessionCashout)} cashout.";
        }
        else
        {
            text = $"{session.CustomerName}, your session payout is {UiHelpers.Gil(payout)}! That is enough for {extraRolls:N0} more {extraRollText} plus {UiHelpers.Gil(extraCashout)} cashout, or {UiHelpers.Gil(buyBackGil)} more gil to buy another {session.RollsAllowed:N0} {sameRollText}.";
        }

        TryQueueAnnouncement(text);
    }

    private static int CalculateSessionBuyIn(GambaSessionRecord session, VenueProfile venue, int rollPrice)
    {
        var gambaDrink = FindGambaDrink(venue);
        if (gambaDrink is not null && gambaDrink.Price > 0 && session.DrinksPurchased > 0)
            return Math.Max(0, gambaDrink.Price * session.DrinksPurchased);

        return Math.Max(0, rollPrice * session.RollsAllowed);
    }

    private static int CalculateGambaRollPrice(VenueProfile venue)
    {
        var gambaDrink = FindGambaDrink(venue);
        if (gambaDrink is null || gambaDrink.Price <= 0)
            return 0;

        var rollsPerDrink = Math.Max(1, venue.Gamba.RollsPerDrink);
        return Math.Max(1, (int)MathF.Ceiling(gambaDrink.Price / (float)rollsPerDrink));
    }

    private static DrinkDefinition? FindGambaDrink(VenueProfile venue)
    {
        return venue.Drinks.FirstOrDefault(d => d.IsGambaDrink)
            ?? venue.Drinks.FirstOrDefault(d => d.Name.Equals(venue.Gamba.DrinkName, StringComparison.OrdinalIgnoreCase));
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.IsHandled || current is null)
            return;

        var gamba = config.ActiveVenue.Gamba;
        var sender = StripChatNoise(message.Sender.ToString());
        var body = StripChatNoise(message.Message.ToString());

        // Dice lines can arrive through different Dalamud chat kinds depending on
        // whether they were sent with /dice, /dice party, cross-world party, or the
        // local chat filters. Do not hard-require XivChatType.Party here; instead,
        // only continue if the message is an actual Random! dice line and the
        // sender matches the active customer or local bartender below. This fixes
        // real-customer rolls that show in party chat visually but are not tagged
        // as XivChatType.Party by the chat event.
        if (!body.Contains("Random!", StringComparison.OrdinalIgnoreCase)
            && !sender.Contains("Random!", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryParsePartyRandom(sender, body, out var parsed))
            return;

        var isCurrentCustomer = MatchesCurrentCustomer(parsed.Name, parsed.World);
        var isLocalPlayer = MatchesLocalPlayer(parsed.Name, parsed.World);

        // Self-test support: when the bartender puts their own character in the
        // customer box, both the customer roll and the bartender bonus roll come
        // from the same sender. Separate them by dice range before the customer
        // path, so /dice 999 still counts as the customer roll while
        // /dice party <bartender max> counts as the bartender bonus check.
        if (isLocalPlayer && gamba.BartenderRollBonusEnabled && IsPotentialBartenderBonusRoll(parsed, gamba))
        {
            var bartenderSettings = new GambaSettings { MinRoll = gamba.MinRoll, MaxRoll = Math.Max(gamba.MinRoll + 1, gamba.BartenderRollMax) };
            var bartenderRejection = ValidateDiceRange(parsed, bartenderSettings, parsed.Name);
            TryConsumeBartenderBonusRoll(parsed.Name, parsed.World, parsed.Roll, bartenderRejection, gamba);
            return;
        }

        // Normal customer roll path. This works for real customers and for
        // self-testing when the local player rolls the venue gamba dice range.
        if (isCurrentCustomer)
        {
            if (rollsRemaining <= 0)
                return;

            var rejectionReason = ValidateDiceRange(parsed, gamba, parsed.Name);
            if (!string.IsNullOrWhiteSpace(rejectionReason))
            {
                status = rejectionReason;
                WarnInvalidCustomerRoll(parsed.Name, rejectionReason);
                return;
            }

            if (parsed.Roll < gamba.MinRoll || parsed.Roll > gamba.MaxRoll)
            {
                status = $"Ignored party-chat roll {parsed.Roll}; expected {gamba.MinRoll}-{gamba.MaxRoll}.";
                return;
            }

            if (IsDuplicatePartyRoll(parsed.Name, parsed.World, parsed.Roll, body))
                return;

            resolvingCustomerRollWasLocalPlayer = isLocalPlayer;
            try
            {
                ResolveRoll(parsed.Roll, "party chat");
            }
            finally
            {
                resolvingCustomerRollWasLocalPlayer = false;
            }
            return;
        }

        // Bartender bonus checks for the normal case where the bartender and
        // customer are different players. Ignore non-matching bartender dice
        // ranges here so a bad manual /dice party value does not consume the
        // pending bonus check.
        if (!gamba.BartenderRollBonusEnabled || !isLocalPlayer || !IsPotentialBartenderBonusRoll(parsed, gamba))
            return;

        var normalBartenderSettings = new GambaSettings { MinRoll = gamba.MinRoll, MaxRoll = Math.Max(gamba.MinRoll + 1, gamba.BartenderRollMax) };
        var normalBartenderRejection = ValidateDiceRange(parsed, normalBartenderSettings, parsed.Name);
        TryConsumeBartenderBonusRoll(parsed.Name, parsed.World, parsed.Roll, normalBartenderRejection, gamba);
    }


    private bool IsPotentialBartenderBonusRoll(ParsedPartyRoll parsed, GambaSettings gamba)
    {
        var requiredMax = Math.Max(gamba.MinRoll + 1, gamba.BartenderRollMax);

        if (parsed.RangeMax.HasValue)
        {
            var rangeMin = parsed.RangeMin ?? gamba.MinRoll;
            return rangeMin == gamba.MinRoll && parsed.RangeMax.Value == requiredMax;
        }

        // Some dice outputs, especially party-targeted dice, may not include the
        // (1-#) range in the received chat line. When BarManager is already waiting
        // for a bartender bonus check, treat a local un-ranged roll within the
        // bartender bonus max as that pending bartender roll. This also fixes
        // self-testing where the bartender and customer are the same character.
        return (awaitingBartenderBonusRoll || pendingBartenderBonusResults > 0 || queuedBartenderBonusRolls > 0)
            && parsed.Roll >= gamba.MinRoll
            && parsed.Roll <= requiredMax;
    }

    private bool TryConsumeBartenderBonusRoll(string name, string world, int roll, string rejectionReason, GambaSettings gamba)
    {
        var wasAwaitingBartenderRoll = awaitingBartenderBonusRoll || pendingBartenderBonusResults > 0;
        if (!wasAwaitingBartenderRoll && queuedBartenderBonusRolls <= 0)
        {
            // Let the bartender manually run /dice party <configured max> after a customer roll
            // even if the automatic command failed to fire. It should only count when the
            // bartender bonus feature is enabled and a session is live.
            if (current is null || !gamba.BartenderRollBonusEnabled)
                return false;
        }

        if (wasAwaitingBartenderRoll && pendingBartenderBonusResults > 0 && (DateTime.Now - awaitingBartenderBonusRollSince).TotalSeconds > 30)
        {
            pendingBartenderBonusResults = 0;
            awaitingBartenderBonusRoll = false;
            wasAwaitingBartenderRoll = false;
            status = "Bartender bonus roll timed out.";
        }

        if (!MatchesLocalPlayer(name, world))
            return false;

        if (pendingBartenderBonusResults > 0)
            pendingBartenderBonusResults--;
        else if (!wasAwaitingBartenderRoll && queuedBartenderBonusRolls > 0)
            queuedBartenderBonusRolls--;
        awaitingBartenderBonusRoll = pendingBartenderBonusResults > 0;
        nextBartenderBonusCommandAt = DateTime.Now.AddMilliseconds(1100);

        if (!string.IsNullOrWhiteSpace(rejectionReason))
        {
            status = rejectionReason;
            return true;
        }

        if (roll == 1)
        {
            ActivateBartenderBonus(gamba);
            status = $"Bartender rolled 1. {SafeBonusName(gamba.BartenderRollBonusName, "Bartender Bonus")} activated.";
        }
        else
        {
            status = $"Bartender rolled {roll}; no bartender bonus activated.";
        }

        if (autoEndAfterBartenderBonusRoll && queuedBartenderBonusRolls <= 0 && pendingBartenderBonusResults <= 0)
            EndSession();

        return true;
    }

    private static bool MatchesLocalPlayer(string name, string world)
    {
        try
        {
            if (!DalamudServices.PlayerState.IsLoaded)
                return false;

            var localName = CleanName(DalamudServices.PlayerState.CharacterName);
            if (string.IsNullOrWhiteSpace(localName))
                return false;

            var expectedWorld = string.Empty;
            try { expectedWorld = DalamudServices.PlayerState.HomeWorld.Value.Name.ToString(); } catch { }
            return MatchesCharacter(localName, expectedWorld, name, world);
        }
        catch
        {
            return false;
        }
    }

    private bool IsDuplicatePartyRoll(string name, string world, int roll, string body)
    {
        var fingerprint = $"{CleanName(name).ToLowerInvariant()}|{CleanName(world).ToLowerInvariant()}|{roll}|{body}";
        var now = DateTime.Now;
        if (fingerprint == lastPartyRollFingerprint && (now - lastPartyRollAt).TotalSeconds < 2)
            return true;

        lastPartyRollFingerprint = fingerprint;
        lastPartyRollAt = now;
        return false;
    }

    private bool MatchesCurrentCustomer(string name, string world)
    {
        if (current is null)
            return false;

        return MatchesCharacter(current.CustomerName, current.CustomerWorld, name, world);
    }

    private static bool MatchesCharacter(string expectedName, string expectedWorld, string actualName, string actualWorld)
    {
        var expected = NormalizeCharacterPart(expectedName);
        var expectedHomeWorld = NormalizeCharacterPart(expectedWorld);
        var actual = NormalizeCharacterPart(actualName);
        var actualHomeWorld = NormalizeCharacterPart(actualWorld);

        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;

        // Party dice names can be delivered with a leading job/class icon, and
        // cross-world party names can arrive as "First LastWorld" or
        // "First Last World" without the usual @ separator. When we know the
        // expected world, strip that suffix before comparing the character name.
        if (!string.IsNullOrWhiteSpace(expectedHomeWorld))
        {
            actual = StripTrailingWorld(actual, expectedHomeWorld);
            actualHomeWorld = StripTrailingWorld(actualHomeWorld, expectedHomeWorld);
        }

        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            // If no world was entered for the customer, still allow a party label
            // that has the world appended directly after the character name. This
            // keeps manually typed customer names working for cross-world players.
            if (!string.IsNullOrWhiteSpace(expectedHomeWorld)
                || actual.Length <= expected.Length
                || !actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return string.IsNullOrWhiteSpace(expectedHomeWorld)
            || string.IsNullOrWhiteSpace(actualHomeWorld)
            || actualHomeWorld.Equals(expectedHomeWorld, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripTrailingWorld(string value, string world)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(world))
            return value;

        var compactValue = RemoveSpaces(value);
        var compactWorld = RemoveSpaces(world);
        if (compactValue.Length <= compactWorld.Length || !compactValue.EndsWith(compactWorld, StringComparison.OrdinalIgnoreCase))
            return value;

        // Preserve the normal display spacing from the character name when the
        // world was appended directly after the surname, such as
        // "Vallon HartsbloodMaduin". Also handles "Vallon Hartsblood Maduin".
        var suffixStart = value.Length - world.Length;
        if (suffixStart >= 0 && value.EndsWith(world, StringComparison.OrdinalIgnoreCase))
            return value[..suffixStart].Trim();

        var withoutSpaces = compactValue[..^compactWorld.Length];
        return withoutSpaces.Trim();
    }

    private static string NormalizeCharacterPart(string text) => RemoveSpaces(CleanName(text)).Trim();

    private static string RemoveSpaces(string text) => Regex.Replace(text ?? string.Empty, @"\s+", string.Empty);

    private static bool TryParsePartyRandom(string sender, string body, out ParsedPartyRoll parsed)
    {
        parsed = default;

        if (TryMatchPartyRandom(ParenthesizedPartyRandomRegex.Match(body), sender, out parsed))
            return true;

        if (TryMatchPartyRandom(PartyRandomRegex.Match(body), sender, out parsed))
            return true;

        if (TryMatchPartyRandom(NamedRandomRegex.Match(body), sender, out parsed))
            return true;

        var combined = string.IsNullOrWhiteSpace(sender) ? body : $"{sender}: {body}";
        if (TryMatchPartyRandom(CombinedRandomRegex.Match(combined), sender, out parsed))
            return true;

        if (TryMatchPartyRandom(NamedRandomRegex.Match(combined), sender, out parsed))
            return true;

        // Fallback for Dalamud chat payloads where the dice sender is supplied in
        // message.Sender and the message body is only "Random! ...". Also handles
        // payloads with hidden icons or other text before Random!.
        if (!TryMatchPartyRandom(RandomRollRegex.Match(body), sender, out parsed))
            return false;

        var bodyName = LeadingParenthesizedNameRegex.Match(body);
        if (bodyName.Success)
            parsed = parsed with { Name = CleanName(bodyName.Groups["name"].Value) };

        return true;
    }

    private static bool TryMatchPartyRandom(Match match, string fallbackSender, out ParsedPartyRoll parsed)
    {
        parsed = default;
        if (!match.Success)
            return false;

        var name = match.Groups["name"].Success ? CleanName(match.Groups["name"].Value) : CleanName(fallbackSender);
        var world = match.Groups["world"].Success ? CleanName(match.Groups["world"].Value) : string.Empty;
        if (!int.TryParse(match.Groups["roll"].Value, out var roll))
            return false;

        int? rangeMin = null;
        int? rangeMax = null;
        if (match.Groups["rangeMax"].Success)
        {
            if (!int.TryParse(match.Groups["rangeMin"].Value, out var parsedMin) || !int.TryParse(match.Groups["rangeMax"].Value, out var parsedMax))
                return false;

            rangeMin = parsedMin;
            rangeMax = parsedMax;
        }

        parsed = new ParsedPartyRoll(name, world, roll, rangeMin, rangeMax);
        return true;
    }

    private static string ValidateDiceRange(ParsedPartyRoll parsed, GambaSettings gamba, string displayName)
    {
        var requiredMax = Math.Max(gamba.MinRoll + 1, gamba.MaxRoll);

        if (!parsed.RangeMax.HasValue)
        {
            // Plain /dice is the same as /dice 999 in FFXIV. Only accept an
            // un-ranged dice line when the venue actually requires 999. Venues
            // using /dice 100, /dice 400, etc. must require the bracketed range
            // so players cannot use the default 999 roll by mistake or to cheat.
            return requiredMax == 999
                ? string.Empty
                : $"Ignored party-chat roll from {displayName}; plain /dice is treated as /dice 999, but this venue requires /dice {requiredMax}.";
        }

        var rangeMin = parsed.RangeMin ?? gamba.MinRoll;
        var rangeMax = parsed.RangeMax.Value;
        if (rangeMin != gamba.MinRoll || rangeMax != requiredMax)
            return $"Ignored party-chat roll from {displayName}; expected /dice {requiredMax} range ({gamba.MinRoll}-{requiredMax}), but saw ({rangeMin}-{rangeMax}).";

        return string.Empty;
    }

    private void WarnInvalidCustomerRoll(string playerName, string rejectionReason)
    {
        if (current is null)
            return;

        var name = string.IsNullOrWhiteSpace(playerName) ? current.CustomerName : playerName;
        var message = $"{name}, that roll was not counted. {rejectionReason}";
        TryQueueAnnouncement(message);
    }

    private void UseCurrentTarget()
    {
        if (DalamudServices.TargetManager.Target is not IPlayerCharacter pc)
        {
            status = "No player target selected.";
            return;
        }

        customerName = pc.Name.ToString();
        try { customerWorld = pc.HomeWorld.Value.Name.ToString(); }
        catch { customerWorld = string.Empty; }
        status = string.IsNullOrWhiteSpace(customerWorld)
            ? $"Selected target {customerName}."
            : $"Selected target {customerName}@{customerWorld}.";
    }

    private static Vector4 GetTierColor(string tier) => tier switch
    {
        "JACKPOT" => BarManagerTheme.Gold,
        "HIGH" => new Vector4(0.88f, 0.48f, 0.94f, 1f),
        "MID" => new Vector4(0.49f, 0.72f, 0.94f, 1f),
        "LOW" => new Vector4(0.49f, 0.92f, 0.82f, 1f),
        "SO_CLOSE" => new Vector4(0.94f, 0.63f, 0.25f, 1f),
        _ => BarManagerTheme.Muted,
    };

    private static string StripChatNoise(string text) => string.Join(' ', text.Replace('＠', '@').Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static string CleanName(string text)
    {
        var cleaned = StripChatNoise(text).Trim().Trim(':').Trim();
        if (cleaned.StartsWith("(") && cleaned.EndsWith(")") && cleaned.Length > 2)
            cleaned = cleaned[1..^1].Trim();

        // FFXIV party dice names may start with private-use job/class icons like
        //  or . Remove those marker glyphs anywhere in the parsed label before
        // comparing names, then trim any remaining leading punctuation.
        cleaned = Regex.Replace(cleaned, @"[\uE000-\uF8FF]", string.Empty);
        cleaned = Regex.Replace(cleaned, @"^[^\p{L}\p{N}]+", string.Empty);
        return cleaned.Trim();
    }
}
