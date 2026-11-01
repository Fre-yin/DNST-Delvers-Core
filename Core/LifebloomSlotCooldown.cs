namespace DungeonSettlersDelvers.Core;

// The Lifebloom slot's cooldown display counts down to the caster's next charge while it
// holds 1 or 2 (LifebloomSlotCooldownRules). Core writes the display only: the fill of _imgCooldown and the game's own
// seconds text (SkillSlotUI.SetCooldown); the game's cooldown, which alone locks the skill, is not touched.
// Slots are looked up every RescanTicks (and after a charge change) and drawn every PaintTicks. The game refreshes the slots
// itself in QuickSlotUI.RefreshSkillSlotUI and writes the fill there directly, the global cooldown of other skills included,
// so the hooks on SkillSlotUI (LifebloomSlotGlobalPatch, LifebloomSlotStatePatch) do not fire
// on that way. The slot showed the other skills' global cooldown and no countdown with charges left;
// so Core paints the slot again right after that refresh too (LifebloomSlotRefreshPatch), and full it clears whatever the
// game drew. Only skill slots exist for selected player units: Core's enemies have none.
// Probe (capped): SetGlobalCooldown on other skills' slots, which image the game fills (CORE_LIFEBLOOM_CD_PROBE).
// The slot's cooldown image runs the other way round (LifebloomSlotCooldownRules.SlotClockwise). The game never sets the fill
// direction itself (Image.set_fillClockwise has no caller in its code), so Core turns it on the Lifebloom slot and gives the
// game's direction back once the slot shows another skill or the slot list is reset.
internal static class LifebloomSlotCooldown
{
    private const int PaintTicks = 6;
    private const int RescanTicks = 30;
    private const int ProbeCap = 10;
    private static int paintTicks;
    private static int rescanTicks = RescanTicks;
    private static int probes;
    private static int failures;
    private static readonly List<SkillSlotUI> slots = new();
    // Slots Core drew a countdown on (by pointer), and the last line per caster.
    private static readonly HashSet<IntPtr> drawn = new();
    private static readonly Dictionary<Guid, string> logged = new();
    // Slots whose cooldown image Core turned, with the game's direction and shade to give back.
    private static readonly Dictionary<IntPtr, (SkillSlotUI Slot, bool GameClockwise, float GameAlpha)> turned = new();

    internal static void Reset()
    {
        GiveBack(all: true);
        slots.Clear();
        drawn.Clear();
        logged.Clear();
        paintTicks = 0;
        rescanTicks = RescanTicks;
        probes = 0;
    }

    // Every game tick (GroupAiBanditTickPatch).
    internal static void OnGameTick()
    {
        if (!Lifebloom.Active) return;
        if (++rescanTicks >= RescanTicks)
        {
            rescanTicks = 0;
            Rescan();
        }
        if (slots.Count == 0 || ++paintTicks < PaintTicks) return;
        paintTicks = 0;
        foreach (var slot in slots) Paint(slot);
    }

    // Whether the unit is the selected one: the skill bar shows its skills (Lifebloom's bloom sound). A fresh look, not the
    // slot list: that holds only Lifebloom's slots and may be up to RescanTicks old; the question comes only with a bloom.
    internal static bool Selected(Guid unit)
    {
        try
        {
            foreach (var slot in UnityEngine.Object.FindObjectsOfType<SkillSlotUI>())
                if (slot != null && CoreGame.GuidOf(slot._owner?.TryCast<UnitEntity>()) == unit) return true;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_CD_FAILED: " + ex.Message);
        }
        return false;
    }

    // Right after the game's bar refreshed one slot (QuickSlotUI.RefreshSkillSlotUI).
    internal static void AfterBarRefresh(QuickSlotUI bar, int index)
    {
        var bars = bar?._skillSlots;
        if (!Lifebloom.Active || bars == null || index < 0 || index >= bars.Count) return;
        Paint(bars[index]);
    }

    // Right after the game refreshed the slot itself.
    internal static void AfterGameRefresh(SkillSlotUI slot)
    {
        if (!Lifebloom.Active || slot == null || slot._key != LifebloomRules.SkillKey) return;
        Paint(slot);
    }

    // The probe line only. The postfix on SetGlobalCooldown calls it next to AfterGameRefresh, which draws the countdown; it asks
    // the group OneShot first (CoreProbes) and returns at once while the group is off, so the drawing never depends on it.
    internal static void Probe(SkillSlotUI slot, float cooldown, float duration)
    {
        if (!CoreProbes.OneShot) return;
        if (probes >= ProbeCap || slot == null || cooldown <= 0f || slot._key == LifebloomRules.SkillKey) return;
        try
        {
            probes++;
            DelversHost.Info(LifebloomSlotCooldownRules.ProbeLine(slot._key, cooldown, duration, slot._imgCooldown?.fillAmount ?? -1f,
                slot._imgSkillduration?.fillAmount ?? -1f, slot._imgSkillDurationBar?.fillAmount ?? -1f));
        }
        catch (Exception ex)
        {
            probes = ProbeCap;
            DelversHost.Warning("CORE_LIFEBLOOM_CD_PROBE_FAILED: " + ex.Message);
        }
    }

    private static void Rescan()
    {
        try
        {
            slots.Clear();
            foreach (var slot in UnityEngine.Object.FindObjectsOfType<SkillSlotUI>())
                if (slot != null && slot._key == LifebloomRules.SkillKey && slot._owner != null) slots.Add(slot);
            drawn.RemoveWhere(pointer => !slots.Exists(s => s.Pointer == pointer));
            GiveBack(all: false);
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_CD_FAILED: " + ex.Message);
        }
    }

    private static void Paint(SkillSlotUI slot)
    {
        try
        {
            if (slot == null || slot._key != LifebloomRules.SkillKey) return;
            var unit = slot._owner?.TryCast<UnitEntity>();
            if (unit == null) return;
            Turn(slot);
            var guid = CoreGame.GuidOf(unit);
            var state = Lifebloom.ChargeState(guid, out var now);
            var (mode, fill, seconds) = LifebloomSlotCooldownRules.Show(state.Count, state.NextAt, now, state.Cooldown);
            Shade(slot, mode);
            if (mode == "ours")
            {
                ShowSeconds(slot);
                slot.SetCooldown(seconds);
                if (slot._imgCooldown != null) slot._imgCooldown.fillAmount = fill;
                drawn.Add(slot.Pointer);
            }
            else if (mode == "none" && (drawn.Remove(slot.Pointer) || slot._imgCooldown?.fillAmount > 0f))
            {
                if (slot._imgCooldown != null) slot._imgCooldown.fillAmount = 0f;
                // The game's own way to clear: no text, frame off; the text object stays on for the next countdown.
                slot.SetCooldown(0f);
            }
            else if (mode == "game")
            {
                // The game's seconds and lock stay; only the circle follows the charge's time (LifebloomSlotCooldownRules.Show).
                ShowSeconds(slot);
                if (slot._imgCooldown != null) slot._imgCooldown.fillAmount = fill;
                drawn.Remove(slot.Pointer);
            }
            var line = mode + "|" + state.Count;
            if (!logged.TryGetValue(guid, out var last) || last != line)
            {
                logged[guid] = line;
                DelversHost.Info(LifebloomSlotCooldownRules.ShowLine(guid, state.Count, mode, seconds, slots.Count));
            }
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_CD_FAILED: " + ex.Message);
        }
    }

    // Right after the game set a slot's skill: a slot Core turned that now shows another skill gets the game's direction back.
    internal static void AfterSlotSet(SkillSlotUI slot)
    {
        if (slot == null || slot._key == LifebloomRules.SkillKey || !turned.TryGetValue(slot.Pointer, out var entry)) return;
        Restore(slot.Pointer, entry);
    }

    private static void Turn(SkillSlotUI slot)
    {
        var image = slot._imgCooldown;
        if (image == null || turned.ContainsKey(slot.Pointer)) return;
        var game = image.fillClockwise;
        turned[slot.Pointer] = (slot, game, image.color.a);
        image.fillClockwise = LifebloomSlotCooldownRules.SlotClockwise(game);
    }

    // The seconds always, at 0, 1 and 2 charges. The game's SetCooldown writes the text and switches only
    // its frame, never the text object itself, so a text object switched off stays off and the
    // number was gone after the first full charge or a new selection; Core switches it on again.
    private static void ShowSeconds(SkillSlotUI slot)
    {
        var text = slot._txtCooldown?.gameObject;
        if (text != null && !text.activeSelf) text.SetActive(true);
    }

    // The cooldown image's shade for the mode (LifebloomSlotCooldownRules.SlotAlpha); written only when it changes.
    private static void Shade(SkillSlotUI slot, string mode)
    {
        var image = slot._imgCooldown;
        if (image == null || !turned.TryGetValue(slot.Pointer, out var entry)) return;
        var color = image.color;
        var alpha = LifebloomSlotCooldownRules.SlotAlpha(mode, entry.GameAlpha);
        if (Math.Abs(color.a - alpha) > 0.001f) image.color = new UnityEngine.Color(color.r, color.g, color.b, alpha);
    }

    private static void GiveBack(bool all)
    {
        foreach (var pointer in turned.Keys.ToList())
        {
            var entry = turned[pointer];
            if (!all && slots.Exists(s => s.Pointer == pointer)) continue;
            Restore(pointer, entry);
        }
    }

    private static void Restore(IntPtr pointer, (SkillSlotUI Slot, bool GameClockwise, float GameAlpha) entry)
    {
        turned.Remove(pointer);
        try
        {
            var image = entry.Slot != null ? entry.Slot._imgCooldown : null;
            if (image != null)
            {
                image.fillClockwise = entry.GameClockwise;
                var color = image.color;
                image.color = new UnityEngine.Color(color.r, color.g, color.b, entry.GameAlpha);
            }
        }
        catch (Exception ex)
        {
            // A slot the game destroyed meanwhile has nothing left to give back.
            if (++failures <= 3) DelversHost.Warning("CORE_LIFEBLOOM_CD_FAILED: " + ex.Message);
        }
    }
}
