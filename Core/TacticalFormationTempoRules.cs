using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The formation's tempo (TacticalFormationTempo): with the tempo button of the formation window on, outside a
// fight every member with its formation on, the leader too, walks at the speed of the slowest of them; nobody becomes faster. In a
// fight each walks at its own speed again. Core slows the faster members with stacks of its own hidden effect. Pure, so the offline
// tests cover it.
internal static class TacticalFormationTempoRules
{
    // Core's hidden effect, one stack takes about one point of MovementSpeed; the prefix keeps it out of the game's save
    // (CoreSaveEffects).
    internal const string EffectKey = CoreSaveEffects.Prefix + "Tempo";
    internal const int MaxStacks = 999;
    // What one stack takes is measured in the game (PerStack); until then one point.
    internal const float FirstPerStack = 1f;
    private const float MinPerStack = 0.05f, MaxPerStack = 10f;

    // The speed the counted members walk at, or null when nothing is capped: the button off, a fight, fewer than two members with a
    // speed. A speed that is not a number or not above 0 is no walk (a member that cannot move does not stop the others). The
    // speeds are the members' own, without Core's effect (Own).
    internal static float? Cap(bool on, bool fight, IEnumerable<float> speeds)
    {
        if (!on || fight) return null;
        var count = 0;
        var slowest = float.PositiveInfinity;
        foreach (var speed in speeds)
        {
            if (float.IsNaN(speed) || !(speed > 0f)) continue;
            count++;
            if (speed < slowest) slowest = speed;
        }
        return count >= 2 ? slowest : null;
    }

    // A member's own speed: the speed read now plus what its stacks take.
    internal static float Own(float speed, int stacks, float perStack) => speed + stacks * perStack;

    // The stacks for the cap, from the stacks on now and the speed read with them: more while the member is faster than the cap, fewer
    // once it is slower by a whole stack or more, so it ends at the cap or less than one stack below it. Never below 0.
    internal static int Stacks(int stacks, float speed, float cap, float perStack)
    {
        if (float.IsNaN(speed) || float.IsNaN(cap) || !(perStack > 0f)) return stacks;
        var target = stacks;
        if (speed > cap) target = stacks + (int)Math.Ceiling((speed - cap) / perStack - 0.0001f);
        else if (cap - speed >= perStack) target = stacks - (int)Math.Floor((cap - speed) / perStack + 0.0001f);
        return Math.Clamp(target, 0, MaxStacks);
    }

    // What one stack takes, measured from two looks: the speed lost over the stacks added. Kept as it was when the stacks did not
    // change or the measure is out of reason (another effect changed the speed between the looks).
    internal static float PerStack(int stacksBefore, float speedBefore, int stacks, float speed, float old)
    {
        if (stacks == stacksBefore || float.IsNaN(speedBefore) || float.IsNaN(speed)) return old;
        var measured = (speedBefore - speed) / (stacks - stacksBefore);
        return measured >= MinPerStack && measured <= MaxPerStack ? measured : old;
    }

    // left: the formation's members that do not count, "<unit>:<dead|off|lying|still>" joined by commas, or empty; slowed: the
    // members with stacks, "<unit>:<stacks>", or empty.
    internal static string Line(bool on, bool fight, float? cap, int members, string left = "", string slowed = "")
        => "CORE_TACTICAL_TEMPO on=" + (on ? "1" : "0") + " fight=" + (fight ? "1" : "0") + " cap="
           + (cap is { } value ? value.ToString("0.###", CultureInfo.InvariantCulture) : "-") + " members=" + members
           + " left=" + (string.IsNullOrEmpty(left) ? "-" : left) + " slowed=" + (string.IsNullOrEmpty(slowed) ? "-" : slowed);
}
