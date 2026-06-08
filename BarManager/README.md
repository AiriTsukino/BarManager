# BarManager


## v0.1.42

- Restored plugin author metadata to Airi Tsukino.
- Kept real character/player names out of parser examples, comments, and runtime matching code.


## v0.1.7

- Added wrapped hover tooltips to the gamba settings and payout rule controls.
- Simplified payout rule roll entry to one `Winning roll(s)` box instead of separate exact/in-values/digit boxes.
- Reworked the main window header so the Ko-fi support button is drawn directly beside the Settings button.

BarManager is a generalized Dalamud plugin for Final Fantasy XIV venue bars. It tracks drink sales, tips, bar buyouts, configurable gamba drink rules, jackpots, sessions, and nightly reports without venue-specific branding.

## Commands

- `/barmanager` opens/closes the main window.
- `/barmanagersettings` opens/closes settings.

## Build

```bash
dotnet restore
dotnet build -c Release
```

## v0.1.2 patch notes

- Fixed Dalamud.Bindings.ImGui `InputText` / `InputTextMultiline` calls by using `int` buffer sizes instead of `uint` literals.
- Added gamba drink settings export/import in Settings -> Gamba Rules.
- Split saved data out of the main Dalamud plugin config:
  - Main config keeps basic settings only: window state, active venue id, and folder overrides.
  - Venue profiles, drink menus, gamba rules, and current audit state are saved in `BarManagerData/bar-data.json` under the plugin config folder by default.
  - Audit reports save to `BarManagerData/AuditReports` by default.
  - Gamba export/import files save to `BarManagerData/GambaSettings` by default.
- Added Settings -> Files & Export for custom data, audit report, and gamba settings folders.
- Added a Save Audit Report button on the Report tab.
- New venue drink menus start empty so each venue can build its own drink list and prices.

## v0.1.1 patch notes

- Switched from `ImGuiNET` to `Dalamud.Bindings.ImGui` for Dalamud.NET.Sdk 15 compatibility.
- Renamed the gamba rule exact-match property to avoid hiding `object.Equals`.

## v0.1.3
- Refined BarManager window styling, support button drawing, and settings page layout to better match AutoGreet/GambaAssistant.
- Fixed Ko-fi support button icon/text rendering by drawing the cup and label manually over an invisible button.
- Added -5, -1, +1, and +5 drink count controls.
- Changed hourly buyout step size to 1 hour while still allowing manual decimal entry.
- Changed new venue jackpot base to 1,000,000 gil.
- Changed default gamba settings/rules to empty.
- Added customer world field and Use Target button for gamba sessions.
- Added live party chat /dice 999 tracking for active sessions only, matching the selected customer and world when available.
- Added configurable party-chat announcements for rolls remaining, defaulting to every 5 rolls with range 1-50.
- Added configurable jackpot contribution percentage from each played roll price.



## v0.1.5

- Added minimum window size constraints to the main window and settings window.
- Main window minimum is 900x620, matching the GambaAssistant main window sizing pattern.
- Settings window minimum is 860x560, matching the GambaAssistant settings window sizing pattern.

## v0.1.4
- Restored the visible Ko-fi support button with a normal labeled button so it no longer disappears with layout changes.
- Added vertical scrollbars to Gamba settings and other long plugin pages.
- Wrapped long folder/status text to avoid right-side overflow.
- Replaced the old expected delta check with Nightly profit/loss: ending gil minus personal starting gil, venue prize gil, and tips.
## v0.1.6
- Restored the Ko-fi support button using the same visible button plus drawn cup icon pattern as GambaAssistant.
- Reworked Gamba Settings so the whole page has one parent scrollbar and payout rules are included in that scroll area at the minimum window size.
- Removed the nested scrollbar from Gamba Drink Basics by replacing the fixed-height child card with normal section content.




## v0.1.13

- Stopped the Exact only tooltip flicker by removing the tooltip from the checkbox row itself and attaching a single wrapped tooltip to a dedicated `(?)` help marker.
- Reworked gamba match option checkboxes into a vertical layout to prevent same-line hover overlap at smaller widths.
- Tooltip text is now read from the cached rule tooltip unless the cache is empty or the winning-roll value changes.

## v0.1.11
- Added dynamic gamba tooltips for Winning roll(s) and Exact only.
- Exact only help now uses the current values entered for each rule and shows examples based on those values.
- Tooltip wrapping now respects the requested wrap width so longer help text stays readable.

## v0.1.9
- Reworked the gamba payout rule editor into a vertical layout so payout and winning-roll values do not get cut off at minimum window size.
- Moved rule actions to a clearer row below each rule.
- Added submitted bar buyouts so multiple buyouts can be recorded during one night.
- Drinks sold during an active buyout are now tracked as buyout-covered instead of normal billable drink sales when the buyout is submitted.
- Audit report now lists submitted buyouts but no longer includes full gamba roll/session detail.
- Added a separate gamba report with its own generate/copy/save buttons and saved files under AuditReports/GambaReports.

### v0.1.11
- Cached gamba rule tooltip text on each rule instead of rebuilding dynamic/default text every frame.
- Winning-roll tooltips now update only when the Winning roll(s) field or Exact only checkbox changes.
- Added a persistent Winning roll(s) edit buffer so the field no longer flickers between parsed/default values while typing.
- Kept a single wrapped tooltip source for Exact only and Winning roll(s) to prevent competing tooltip text.

### v0.1.14
- Added full venue profile export/import for sharing a venue's menu, buyout/jackpot settings, and gamba setup.
- Gamba settings import/export now uses native file picker dialogs instead of typed paths.
- Venue profile import/export now uses native file picker dialogs.
- Audit and gamba report saving now opens a save dialog so bartenders can choose the destination file.
- Added confirmations for clearing all gamba rules, deleting drinks, and deleting venue profiles.
- Constrained the reset-night confirmation popup size so it fits the contents instead of opening oversized.

## v0.1.17

- Added a Venue prize gil tooltip explaining that it is gil given by venue management for prize payouts.
- Reworked the Audit > Drinks Sold section into aligned table columns so drink names no longer change where the quantity controls appear.
- Removed billable/buyout-covered text from the live Drinks Sold section and left only the per-drink total there; detailed buyout coverage remains in the generated audit report.

## v0.1.16

- Made confirmation popups auto-size to their content.
- Disabled resizing/saved sizing on confirmation popups.
- Added fixed text wrapping inside confirmation popups so long messages fit without oversized windows.

## v0.1.15
- Fixed drink menu delete confirmation so the Delete button opens the global confirmation popup correctly from table rows.
- Split venue import/export hover help into separate tooltips.
- Added a dedicated export venue tooltip and limited the import tooltip to import-only information.

## v0.1.18

- Changed the Audit > Drinks Sold name column to size from the largest drink name instead of stretching across the full window.
- Kept quantity controls and totals closer to the drink names while still aligned in columns.
- Aligned all quantity buttons on the same row using fixed-size buttons.
- Added -20, -10, +10, and +20 quantity buttons.

## v0.1.19
- Updated party-chat roll tracking to parse newer Dalamud dice message formats like `(First Last) Random! (1-999) 590` and `(First Last) Random! 440`.
- Ranged dice messages now only count when the range matches the venue gamba maximum roll setting, defaulting to 999.
- Plain `/dice` messages without a displayed range are still accepted for the active customer.
- Rolls from smaller displayed ranges, such as `(1-100)` while the venue requires 999, are ignored to prevent using an easier dice range.


## v0.1.21
- Added a global announcement debounce so rolls-remaining and no-rolls-remaining party messages cannot be sent twice by duplicate event delivery or overlapping tab instances.
- Fixed selected venue persistence on plugin reload by loading the external venue data before applying default venue selection.

## v0.1.20
- Prevented duplicate party-chat roll processing/remaining-roll announcements for the same dice message.
- Remaining-roll announcements now remember the last announced remaining count during a session.
- Clarified and enforced that Any triple and Adjacent doubles rules work even when Winning roll(s) is empty.


## v0.1.23
- Added total saved gamba rolls to the Gamba Sessions tab.
- Added global loss-streak bonus settings for gamba sessions.
- Added global bartender-roll bonus settings and live-session bonus roll button.
- Bonus multipliers can announce activation in party chat and apply to the next winning payout.
- Gamba report/session details now include bonus multiplier information when applied.

## v0.1.22
- Moved rolls remaining / no rolls remaining party-chat announcements out of the chat-message callback and into a framework-update send queue.
- Added pending and recently-sent announcement guards so the same remaining-roll message cannot be queued or sent twice.
- Added the Dalamud framework service used by the deferred announcement sender.


## v0.1.27
- Bartender roll bonus no longer rolls when a live session starts.
- Automated bartender bonus checks now begin only after each customer roll, when the bonus is eligible.
- Updated bartender bonus tooltips/status text to clarify the after-customer-roll behavior.

## v0.1.25
- Added configurable party-chat announcement text for loss streak and bartender roll bonuses.
- Live session summary now uses two columns and a taller card so active player, rolls remaining, session payout, jackpot, and bonus status fit without a nested scrollbar.
- Bartender roll bonus is now automated while enabled: BarManager sends the configured /dice command when the bonus is eligible instead of requiring a manual button press.
- Updated bartender bonus settings tooltips to explain the automated behavior.

## v0.1.24
- Fixed build failure from `IClientState.LocalPlayer` by using Dalamud player state for bartender bonus roll identity checks.
- Added separate off-by-default jackpot multiplier toggles for loss-streak and bartender-roll bonuses.
- Bonus multipliers now only multiply jackpot wins when the matching jackpot toggle is enabled.


## v0.1.27
- Changed default bonus announcement text to: `{player}, {bonus} is active! Your next win is multiplied by x{multiplier} {duration}.`
- Deferred automated bartender `/dice #` commands to framework update instead of sending from the party chat callback.
- Queues one bartender bonus roll after each eligible customer roll, preserving order if the customer rolls multiple times before the bartender roll result is received.


## v0.1.28
- Changed automated bartender bonus rolls to send `/dice party #` using the configured dice maximum.
- Added configurable bonus durations for Loss Streak and Bartender bonuses.
- Loss Streak duration defaults to unlimited; Bartender bonus duration defaults to 3 turns.
- Leaving a bonus duration blank keeps that bonus active until the next win.
- Updated bonus activation/status text and tooltips for configurable durations.

### v0.1.29
- Fixed bartender bonus roll handling so `/dice party <configured max>` is parsed against the bartender bonus dice max instead of the customer gamba roll max.
- Added a delay before automatic bartender bonus dice commands so BarManager sends `/dice party <configured max>` from the framework update loop after each customer roll, not directly in the chat callback.
- Manual bartender `/dice party <configured max>` rolls now count for the bonus check when the bonus is eligible, even if the automatic command did not fire.


### v0.1.30
- Reworked party-chat dice handling so customer rolls are parsed and resolved before bartender bonus rolls are considered.
- Fixed a regression where bartender bonus parsing could interfere with normal customer roll tracking and remaining-roll updates.
- Bartender bonus rolls now validate against the bartender bonus dice max only after confirming the roll came from the local bartender.


### v0.1.31
- Removed the bartender bonus auto-roll description and roll-tracking description from the live session card to reduce clutter.
- Reduced the manual paste box height and added a concise note that party rolls are auto-tracked there instead.
- Fixed bartender bonus roll queueing so `/dice party <configured max>` is queued after every resolved customer roll, including the final paid roll before auto-end.
- Delayed auto-ending a session until any pending final bartender bonus check is sent and consumed or times out.
- Removed remaining-roll guards from bartender bonus command dispatch so a bonus check can still be sent for the customer roll that used the last paid roll.


### v0.1.32
- Reduced the automated bartender bonus roll delay so `/dice party <configured max>` is sent on the next framework update as close to instantly after each customer roll as possible.
- Reduced the between-bartender-roll cooldown so queued bartender rolls process much faster while still staying outside the chat callback.
- Added a party-chat payout summary when a gamba session is saved, including total payout, how many more rolls the winnings could buy, and the remaining buy-in needed to buy the same amount of rolls again.
### v0.1.33
- Improved self-testing support for bartender bonus rolls.
- When the local bartender is also entered as the active customer, BarManager now separates rolls by dice range: the venue gamba max counts as the customer roll and the bartender bonus dice max counts as the bartender bonus roll.
- Real customer sessions continue to use the same customer-first party-chat tracking.



### v0.1.35
- Changed automated bartender bonus roll dispatch so BarManager no longer waits for the previous bartender result before sending the next queued `/dice party #`.
- One bartender bonus roll command is now sent as close as possible after each customer roll, with only a tiny command-spacing delay to preserve order.
- Pending bartender roll results are tracked separately from queued commands, so quick customer/self-test rolls do not pile up and fire only at the end of the session.

### v0.1.34
- Updated the end-of-session payout announcement to include rollover-roll and leftover-cashout math.
- If the payout covers the original buy-in, the summary now says it is enough for another matching session plus the remaining cashout instead of calling it a buy-in.
- Improved self-testing and party dice parsing for bartender bonus rolls when `/dice party #` output does not include a visible `(1-#)` range.
- Kept one automatic `/dice party #` queued for every customer roll, even when a bonus is already active, so queued rolls stay in order and are consumed correctly.


## v0.1.40

- Improved party dice name matching for real-player sessions.
- Strips FFXIV private-use job/class icons like `` and `` before comparing player names.
- Handles cross-world party dice labels where the world is appended to the character name, such as `(JobIconFirst LastWorld) Random! ...`, by matching against the selected customer name and world.
- Keeps same-home-world party dice working when only the first and last name are shown.

## v0.1.38
- Adjusted automated bartender bonus roll timing for self-testing. When the bartender is also the customer, BarManager now waits briefly for the game dice cooldown before sending `/dice party #` instead of immediately marking it as sent.
- Real-customer rolls still queue the bartender bonus roll almost immediately after the customer roll.
- Added spacing between automatic bartender dice commands so multiple quick customer/self rolls stay ordered without the game silently ignoring dice commands.
- Status text now distinguishes queued bartender rolls from sent rolls waiting for a result.


### v0.1.40
- Improved party dice name/world matching for party icons and appended world names.

### v0.1.38
- Fixed real-customer roll tracking by no longer requiring dice messages to arrive as `XivChatType.Party`. Some `/dice` and `/dice party` results visually appear in party chat but are delivered by Dalamud under a different chat kind. BarManager now parses actual `Random!` dice lines from any chat kind, then still requires the sender to match the active customer or local bartender before processing.

### v0.1.41
- Reworked party dice parsing so it does not depend on hardcoded example player names or one exact rendered chat shape.
- Supports generic dice labels such as `First Last World Random! (1-999) 590`, `First Last Random! 590`, `First Last@World: Random! (1-999) 590`, and parenthesized party labels.
- Strips private-use job/class icons before name matching and splits known FFXIV world names from either spaced or appended cross-world labels.
- Removed specific player-name examples from comments and documentation.

### v0.1.43
- Added SeString payload validation for party dice tracking so typed chat text that only says `Random! 729` is ignored.
- Real dice-looking text must now include the game's dice/autotranslate/icon payload structure in the message body before BarManager will resolve it as a roll.
- Kept existing player/world/range validation after the payload check.
