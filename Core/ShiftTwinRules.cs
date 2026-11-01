using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The Shift twin of the item quick slot key (Shift twin of the quick slot key at runtime). The game's key
// dispatcher gives a key pressed down the modifier held with it (KeyInputDispatcher.AddKeyInputToInputStream) and the key setting
// looks up exactly that combination (KeySetting.GetInputTypes over _inputsByKeyInput); the quick slot is bound without a
// modifier (Q by default), so Shift with it found nothing and Core's bandage on another unit (BandageOnOther) never got the press.
// After every rebuild of that runtime index Core adds, for each key bound to an item quick slot without a modifier, the same key
// with Shift for those item slots only; the player's own binding is followed, a combination the player has bound stays theirs,
// and the saved settings are never touched. The slots: the game's UseItemQuickSlot_1 and the Extended Hotbar's two extra item
// slots (its KeyInputType 12100 + slot, Extended Hotbar 1.0.2). No game types here; ShiftTwin applies it.
internal static class ShiftTwinRules
{
    internal const int NoModifier = 0;
    internal const int LeftShift = 304;
    internal const int RightShift = 303;
    internal static readonly int[] ShiftKeys = { LeftShift, RightShift };
    internal static readonly int[] HotbarExtraItemSlots = { 12101, 12102 };
    internal const int LogCap = 20;

    // One entry of the runtime index: a key, pressed down or held, its modifier and the input types bound to it.
    internal readonly struct Entry
    {
        internal Entry(int key, bool down, int modifier, int[] types)
        {
            Key = key;
            Down = down;
            Modifier = modifier;
            Types = types ?? Array.Empty<int>();
        }

        internal int Key { get; }
        internal bool Down { get; }
        internal int Modifier { get; }
        internal int[] Types { get; }
    }

    // The twins to add: for each key without a modifier bound to item slots, the same key with each Shift key, bound to those
    // item slots only, unless that combination stands in the index already (the player's own) or the key is a Shift key itself.
    internal static List<Entry> Plan(IReadOnlyList<Entry> index, ICollection<int> itemSlots)
    {
        var twins = new List<Entry>();
        if (index == null || itemSlots == null || itemSlots.Count == 0) return twins;
        var bound = new HashSet<(int, bool, int)>();
        foreach (var entry in index) bound.Add((entry.Key, entry.Down, entry.Modifier));
        foreach (var entry in index)
        {
            if (entry.Modifier != NoModifier || Array.IndexOf(ShiftKeys, entry.Key) >= 0) continue;
            var slots = new List<int>();
            foreach (var type in entry.Types)
                if (itemSlots.Contains(type) && !slots.Contains(type)) slots.Add(type);
            if (slots.Count == 0) continue;
            foreach (var shift in ShiftKeys)
            {
                if (!bound.Add((entry.Key, entry.Down, shift))) continue;
                twins.Add(new Entry(entry.Key, entry.Down, shift, slots.ToArray()));
            }
        }
        return twins;
    }

    internal static string Line(IReadOnlyList<Entry> twins)
    {
        var keys = new List<string>();
        foreach (var twin in twins)
        {
            var key = twin.Key.ToString(CultureInfo.InvariantCulture);
            if (!keys.Contains(key)) keys.Add(key);
        }
        return string.Format(CultureInfo.InvariantCulture, "CORE_SHIFT_TWIN added={0} keys={1}", twins.Count, keys.Count == 0 ? "-" : string.Join("+", keys));
    }
}
