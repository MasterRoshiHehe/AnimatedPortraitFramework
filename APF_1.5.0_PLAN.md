# APF 1.5.0 — implementation plan

Written 2026-09-25 in the planning chat, against the 1.4.0 code on GitHub (commit `e9de648`).
Read the whole file before you start. Line numbers are approximate (1.4.0 `ModEntry.cs`).

## Ground rules (unchanged)
- **Don't reinvent Content Patcher in APF.** CP/the game decide *which root* (Pyjamas, Summer, …) an NPC wears. APF only decides *which sub-variant* of that root (Rules). No new condition types come out of this plan.
- Don't edit Pixel Waifu Ripening / Pixel Waifu Reminiscens. The game sources and `DialogueDisplayFrameworkContinued-main` are read-only.
- Build: `dotnet build -c Release`. `<EnableModDeploy>false</EnableModDeploy>` stays. Never copy builds into the Steam `Mods` folder (the game runs through Mod Organizer 2).
- Everything must work in multiplayer (host + farmhands).
- Bump `manifest.json` to `1.5.0` and document both features in `README.md`.

---

## Task 1 — Multiplayer-safe daily seed

### Problem
`VariantEvaluator.DailySeed` (≈ line 28) uses `Game1.stats.DaysPlayed`. That is a **per-player** stat: a farmhand who joined the save later has a lower number than the host, so host and farmhand roll different outfits for the same NPC on the same day. `ConditionChecker.CheckRandom` uses the same seed, so it has the same problem.

### Change
- Use the world date instead: `Game1.Date.TotalDays` (same value for every player).
- Keep `Game1.uniqueIDForThisGame` and `Game1.hash.GetDeterministicHashCode` (those are already correct in 1.4.0).
- Add a day offset parameter now, because Task 2 needs it:

```csharp
internal static int DailySeed(string npcName, string rootVariant, int dayOffset = 0)
{
    int day = Context.IsWorldReady ? Game1.Date.TotalDays + dayOffset : 0;
    string key = $"{npcName?.ToLowerInvariant()}|{rootVariant?.ToLowerInvariant()}|{day}|{Game1.uniqueIDForThisGame}";
    return Game1.hash.GetDeterministicHashCode(key);
}
```

Side effect: everyone's current rolls change once when updating to 1.5.0. That's acceptable; mention it in the README changelog.

---

## Task 2 — Carry-over (e.g. pyjamas)

### Problem
Rules roll a new sub-variant every day. So Haley goes to bed in tonight's pyjamas roll (say `Pyjamas_UndiesBlue`) and wakes up in a different roll (`Pyjamas_Default`) the next morning.

### Wanted behaviour
- A Rule can opt in with `"CarryOver": true`.
- **Morning:** the first stretch of the day in which that root is active uses **yesterday's** roll (what she went to bed in).
- **After that:** as soon as the root is no longer active (she got dressed), switch to **today's** roll. That is what she wears tonight, and so what she wakes up in tomorrow.
- **No previous day** (the first day of a new save) → today's roll.
- The pack author only adds one flag. No times, no conditions.

Pack example (PWR's `Haley_Outfits.json`, for reference only; don't edit PWR):

```json
{
  "NPC": "Haley",
  "Root": "Pyjamas",
  "CarryOver": true,
  "Variants": [
    { "Id": "Pyjamas_Default",    "Priority": 10, "Weight": 1.0 },
    { "Id": "Pyjamas_UndiesBlue", "Priority": 10, "Weight": 0.8 },
    { "Id": "Pyjamas_Nude",       "Priority": 10, "Weight": 0.2 }
  ]
}
```

### Key insight: no save data needed
The daily roll is deterministic (Task 1), so "yesterday's roll" can simply be **recomputed** with `dayOffset: -1`. Nothing is written to the save. That's why it works for farmhands, and for players who install APF mid-save (their NPC "went to bed" in whatever yesterday's seed gives; nobody can tell the difference).

### Implementation

1. **Model** (`Framework/VariantModels.cs`): add `public bool CarryOver { get; set; }` to `VariantRule`. Make sure `ContentPackManager` keeps it when it collects Rules (it deserialises `VariantRule`, so it should just work — verify).

2. **Evaluator** (`Framework/VariantEvaluator.cs`):
   - `Evaluate(string npcName, string rootVariant, int dayOffset = 0)`.
   - Pass `dayOffset` to `DailySeed`.
   - Pass it through to the Random condition too, so yesterday's roll is fully yesterday's. That means `ConditionChecker.Check(...)` / `CheckRandom(...)` get a `dayOffset` parameter (default 0).
   - If `Game1.Date.TotalDays + dayOffset < 0` (the first day of a save), use offset 0.
   - Add a helper `bool IsCarryOver(string npc, string root)` (true if any Rule for that NPC + root has `CarryOver`).
   - Known limitation (document it in the README): yesterday's roll is evaluated with *today's* game state, so Rule conditions like Season/Weather can come out differently on e.g. the first day of a new season. Pyjamas rules normally have no conditions, so this doesn't matter in practice.

3. **Morning state** (`ModEntry.cs`):
   - New field: `HashSet<(string Npc, string Root)> _carryOverMorning` (case-insensitive comparer, like `ActiveVariantCache`).
   - `OnDayStarted`: **before** `EvaluateAllOverworldSprites()`, fill it with every (NPC, Root) whose Rule has `CarryOver`. Don't check the NPC's appearance here; it may not be updated yet at DayStarted.
   - `OnSaveLoaded` / returning to title: clear it (DayStarted refills it).

4. **Use it** in `ApplyEvaluatedVariant` (≈ line 1522). This single method feeds both the overworld sprite (`EvaluateAllOverworldSprites`) and the dialogue portrait (`ResolveSessionVariant` → CP-sync), so sprite and portrait stay consistent automatically:

```csharp
int dayOffset = _carryOverMorning.Contains((portrait.Target, resolvedRoot)) ? -1 : 0;
string winner = _evaluator.Evaluate(portrait.Target, resolvedRoot, dayOffset);
```

5. **End of the morning stretch**:
   - Rule: the morning ends at the **first check where the root is not the NPC's active appearance.** She always starts the day at 6:00 in her morning outfit (saves always load at DayStarted), so this is exactly "she got dressed". If she *isn't* in pyjamas that morning at all (e.g. the CP condition failed), the first check ends the morning straight away, and her evening pyjamas correctly use today's roll.
   - "Active root" = the suffix of the NPC's current **overworld sprite** asset name: `npc.Sprite.textureName.Value` = `Characters/{TextureName}_{Root}` → `Root`. Reuse the existing `StripPrefix(assetName, "Characters", npcTextureName)` helper (≈ line 1281), the same convention the CP-sync uses for portraits. Compare case-insensitively.
   - When the morning ends: remove the pair from `_carryOverMorning`, then call `ApplyEvaluatedVariant(portrait, root)`. The winner changes, so the existing code already invalidates `Characters/{NPC}_{Root}` and calls `reloadSprite()`. She isn't wearing that root right now, so the swap is invisible.
   - Log it: `[CARRY-OVER] Haley/Pyjamas: morning over → today's roll 'Pyjamas_Nude'` (Debug), and at DayStarted `[CARRY-OVER] Haley/Pyjamas: morning uses yesterday's roll 'Pyjamas_UndiesBlue'` (Debug).

6. **When to check** (new method `CheckCarryOverMornings()`):
   - New handler `helper.Events.GameLoop.TimeChanged` → `CheckCarryOverMornings()`.
   - Also at the end of `OnWarped` (after `EvaluateAllOverworldSprites()`).
   - Skip while `Game1.eventUp` or a festival is running (a festival sprite is not "she got dressed"). Skip NPCs that aren't loaded (`Game1.getCharacterFromName(name)` null) or have no `currentLocation`.
   - Cheap: only iterates the pairs still in `_carryOverMorning`, so after everyone is dressed it does nothing.

7. **Portrait side:** no separate work. CP-sync goes through `ApplyEvaluatedVariant`, so a morning conversation gets yesterday's pyjamas portrait and an evening one gets today's.

8. **Multiplayer:** everything is computed locally from the world date and the NPC's current sprite, so every client reaches the same result. No messages, no save data.

### Test plan
1. New save, day 1 (Spring 1): Haley's morning pyjamas = the normal roll (no yesterday); no errors.
2. Note her pyjamas at 20:00+ on day N. Sleep. Day N+1 at 6:10: same pyjamas. Log shows `morning uses yesterday's roll`.
3. Once she changes into her day outfit: log shows `morning over → today's roll`; no visible flicker.
4. Evening of day N+1: pyjamas = today's roll (may differ from the morning).
5. Talk to her in the morning and in the evening: the portrait matches the sprite both times.
6. Rule without `CarryOver`: behaves exactly like 1.4.0 (apart from the Task 1 seed change).
7. Multiplayer (two instances, farmhand joined on a later day): both see the same outfits.

---

## Task 3 — Live Appearance framework compatibility (NOT now)
A separate new mod is being planned (`claude/LiveAppearance-design.md` in the SDV project) that makes the game re-choose NPC appearances every 10 minutes / on tile change. **APF needs no change for it**: APF's `AssetRequested` Late overlay already puts the rolled winner into `Characters/{NPC}_{Root}`, so whoever loads the root gets the right outfit.
Later, optional: if that mod exposes an `AppearanceChanged` API event, APF can subscribe (soft dependency) and call `CheckCarryOverMornings()` from it so the morning switch happens instantly. Don't implement until that mod exists.

---

## Notes for Glarthon (CP side, not APF code)
- Haley's pyjamas Appearance condition `TIME 600 930 2000 2600` only reads the first range. Should be `ANY "TIME 600 930" "TIME 2000 2600", Spiderbuttons.BETAS_NPC_NEAR_AREA HaleyHouse 5 6 4 Haley`.
- Add `"CarryOver": true` to the Pyjamas Rules you want it on.
