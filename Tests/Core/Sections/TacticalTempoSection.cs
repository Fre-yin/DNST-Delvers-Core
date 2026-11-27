using DungeonSettlersDelvers.Core;
using static TestKit;

// The formation's tempo (TacticalFormationTempoRules): with the button on and outside a fight the members with
// their formation on walk at the slowest one's speed, nobody faster; in a fight each at its own. Core's hidden effect takes the
// difference in stacks.
internal static class TacticalTempoSection
{
    internal static void Run()
    {
        Check(TacticalFormationTempoRules.Cap(true, false, new[] { 4f, 2.5f, 3f }) == 2.5f,
            "tempo: the button on, outside a fight: the slowest member's speed");
        Check(TacticalFormationTempoRules.Cap(false, false, new[] { 4f, 2.5f }) == null && TacticalFormationTempoRules.Cap(true, true, new[] { 4f, 2.5f }) == null,
            "tempo: nothing capped with the button off or in a fight");
        Check(TacticalFormationTempoRules.Cap(true, false, new[] { 4f }) == null
                && TacticalFormationTempoRules.Cap(true, false, new[] { 4f, float.NaN, 0f, -1f }) == null
                && TacticalFormationTempoRules.Cap(true, false, new[] { 4f, 0f, 3f }) == 3f,
            "tempo: a member that cannot walk does not count, and one walking member alone is not capped");
        Check(TacticalFormationTempoRules.Own(45f, 44, 1f) == 89f && TacticalFormationTempoRules.Own(80f, 0, 1f) == 80f,
            "tempo: a member's own speed is the speed read plus what its stacks take");
        Check(TacticalFormationTempoRules.Stacks(0, 89.2f, 45f, 1f) == 45 && TacticalFormationTempoRules.Stacks(45, 44.2f, 45f, 1f) == 45
                && TacticalFormationTempoRules.Stacks(45, 45.3f, 45f, 1f) == 46,
            "tempo: a faster member gets stacks until it is at the cap or less than one stack below it, never above");
        Check(TacticalFormationTempoRules.Stacks(45, 40f, 45f, 1f) == 40 && TacticalFormationTempoRules.Stacks(3, 20f, 45f, 1f) == 0
                && TacticalFormationTempoRules.Stacks(0, 30f, 45f, 1f) == 0,
            "tempo: when the cap rises the stacks go down, never below none; the slowest gets none");
        Check(TacticalFormationTempoRules.Stacks(0, 90f, 45f, 0.9f) == 50 && TacticalFormationTempoRules.Stacks(10, float.NaN, 45f, 1f) == 10
                && TacticalFormationTempoRules.Stacks(0, 5000f, 45f, 1f) == TacticalFormationTempoRules.MaxStacks,
            "tempo: the stacks follow the measured value of a stack and stay within the effect's stacks");
        Check(TacticalFormationTempoRules.PerStack(0, 90f, 50, 45f, 1f) == 0.9f && TacticalFormationTempoRules.PerStack(50, 45f, 50, 40f, 0.9f) == 0.9f
                && TacticalFormationTempoRules.PerStack(0, 90f, 10, 95f, 1f) == 1f && TacticalFormationTempoRules.PerStack(0, float.NaN, 10, 80f, 1f) == 1f,
            "tempo: what a stack takes is measured over a change of stacks, other changes keep the last value");
        Check(TacticalFormationTempoRules.Line(true, false, 2.5f, 4) == "CORE_TACTICAL_TEMPO on=1 fight=0 cap=2.5 members=4 left=- slowed=-"
                && TacticalFormationTempoRules.Line(true, true, null, 4) == "CORE_TACTICAL_TEMPO on=1 fight=1 cap=- members=4 left=- slowed=-"
                && TacticalFormationTempoRules.Line(true, false, 3f, 3, "u:lying", "v:44") == "CORE_TACTICAL_TEMPO on=1 fight=0 cap=3 members=3 left=u:lying slowed=v:44",
            "tempo: a line names the button, the fight, the cap, the counted members, the ones left out and the slowed ones");
        var holder = "{\"Key\": \"" + TacticalFormationTempoRules.EffectKey + "\", \"Stack\": 44, \"Duration\": 0.0, \"InitialDuration\": 0.0}";
        var json = "{\"Affecters\": [{\"Key\": \"AFFECTER_Slow\", \"Stack\": 1, \"Duration\": 3.0, \"InitialDuration\": 3.0}, " + holder + "]}";
        var stripped = CoreSaveEffects.Strip(json, out var removed);
        Check(TacticalFormationTempoRules.EffectKey.StartsWith(CoreSaveEffects.Prefix, StringComparison.Ordinal) && removed == 1
                && !stripped.Contains(TacticalFormationTempoRules.EffectKey) && stripped.Contains("AFFECTER_Slow"),
            "tempo save: Core's tempo effect never reaches the game's save, the game's own effects stay");
        Save();
    }

    static void Save()
    {
        var campaign = Guid.NewGuid();
        var data = CoreSaveData.Create(campaign);
        Check(!data.FormationTempo && !data.Serialize().Contains("formationTempo"), "tempo save: off at first, no field");
        data.SetFormationTempo(true);
        var read = CoreSaveData.Parse(data.Serialize(), campaign);
        Check(read.FormationTempo, "tempo save: the button comes back on from the save");
        read.SetFormationTempo(false);
        Check(!read.FormationTempo && !read.Serialize().Contains("formationTempo"), "tempo save: off again, the field goes");
    }
}
