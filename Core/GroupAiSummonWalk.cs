namespace DungeonSettlersDelvers.Core;

// The party's summons in the formation (GroupAiWaterRules), from each look of GroupAiFormation: Core steers a
// summon while the formation wants it somewhere (SummonControl.Hold, an order for each new cell and again when it stopped short,
// the game's own decisions for it skipped meanwhile) and lets it go otherwise (GroupAiFormationRules.SummonStep).
//   Out of a fight every summon with a place in the formation stands on it, like the members, with the group AI or without it
//     (the formation runs on every difficulty, so its summon places do too).
//   In a fight a support water mage's bubblelings stand beside the most hurt member once one is below the guard share, with look
//     ahead and buffer (LowLine.Guard); each keeps its cell while that member stays the most hurt. Otherwise they are the game's
//     and attack the mage's target (SummonFollowTarget); a damage dealer's summons stay aggressive. Only with the full group AI;
//     without it a summon in a fight is the game's.
// Without a formation nothing is held (Forget).
internal static class GroupAiSummonWalk
{
    private static readonly Dictionary<Guid, (Guid Member, (int X, int Y) Cell)> guards = new();

    internal static void Reset() => guards.Clear();

    // A look without a formation that applies: every summon back to the game.
    internal static void Forget()
    {
        guards.Clear();
        var keep = new HashSet<string>(StringComparer.Ordinal);
        BanditSummons.AddHeld(keep);
        SummonControl.ReleaseAllBut(keep, "no-formation");
    }

    internal static void Look(IReadOnlyList<TacticalFormationRules.Slot> slots, (int X, int Y)?[] places, List<UnitEntity> party, bool fight)
    {
        var keep = new HashSet<string>(StringComparer.Ordinal);
        switch (GroupAiFormationRules.SummonStep(fight, GroupAiSettings.Enabled))
        {
            case GroupAiFormationRules.SummonDo.Place:
                Places(slots, places, party, keep);
                break;
            case GroupAiFormationRules.SummonDo.Guard:
                Guard(party, keep);
                break;
        }
        if (!fight) guards.Clear();
        BanditSummons.AddHeld(keep);
        SummonControl.ReleaseAllBut(keep, fight ? "fight" : "no-place");
    }

    private static void Places(IReadOnlyList<TacticalFormationRules.Slot> slots, (int X, int Y)?[] places, List<UnitEntity> party, HashSet<string> keep)
    {
        var members = new HashSet<Guid>(party.ConvertAll(CoreGame.GuidOf));
        for (var i = 0; i < slots.Count; i++)
        {
            var guid = slots[i].Unit;
            if (members.Contains(guid) || places[i] is not { } place) continue;
            var unit = CoreGame.FindUnit(guid);
            if (unit == null || unit.IsDead) continue;
            SummonControl.Hold(unit, new UnityEngine.Vector2Int(place.X, place.Y), "formation-place");
            keep.Add(unit.Guid.ToString());
        }
    }

    private static void Guard(List<UnitEntity> party, HashSet<string> keep)
    {
        if (CoreGame.InMap is not { } inMap) return;
        UnitEntity lowest = null;
        var lowestShare = 1f;
        foreach (var member in party)
        {
            if (member == null || member.IsDead) continue;
            var share = GroupAiLow.Share(member, GroupAiSituation.ShareOf(member), LowLine.Guard);
            if (lowest == null || share < lowestShare)
            {
                lowest = member;
                lowestShare = share;
            }
        }
        if (lowest == null || !GroupAiWaterRules.Guards(lowestShare))
        {
            guards.Clear();
            return;
        }
        var lowestGuid = CoreGame.GuidOf(lowest);
        var at = CoreGame.Position(lowest);
        var taken = new HashSet<(int, int)>();
        foreach (var unit in CoreGame.MapUnits())
            if (unit != null && !unit.IsDead) { var p = CoreGame.Position(unit); taken.Add((p.x, p.y)); }
        foreach (var pair in guards)
            if (pair.Value.Member == lowestGuid) taken.Add(pair.Value.Cell);
        foreach (var mage in party)
        {
            var mageGuid = CoreGame.GuidOf(mage).ToString("D");
            if (GroupAiRoles.PlayerOf(mageGuid) != TacticalRole.Support) continue;
            var magePosition = CoreGame.Position(mage);
            foreach (var summon in DelversSummons.SummonsOf(mageGuid))
            {
                var bubble = summon.TryCast<UnitEntity>();
                if (bubble == null || bubble.IsDead || bubble.Key != WeakBanditKitKeys.Bubbling) continue;
                var bubbleGuid = CoreGame.GuidOf(bubble);
                if (!guards.TryGetValue(bubbleGuid, out var guard) || guard.Member != lowestGuid)
                {
                    var cell = FreeBeside(inMap, (at.x, at.y), (magePosition.x, magePosition.y), taken);
                    if (cell == null) continue;
                    guard = (lowestGuid, cell.Value);
                    guards[bubbleGuid] = guard;
                    taken.Add(cell.Value);
                    GroupAiPolicy.Log(GroupAiWaterRules.Line(bubbleGuid.ToString("D"), "support", "guard", lowestGuid.ToString("D"), lowestShare, 0, 0,
                        CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
                }
                SummonControl.Hold(bubble, new UnityEngine.Vector2Int(guard.Cell.X, guard.Cell.Y), "guard");
                keep.Add(bubble.Guid.ToString());
            }
        }
    }

    private static (int X, int Y)? FreeBeside(IInMapDataSupplier inMap, (int X, int Y) member, (int X, int Y) mage, HashSet<(int, int)> taken)
    {
        foreach (var (x, y) in WeakBanditMageRules.BubblingCells(member, mage, taken))
        {
            var cell = new UnityEngine.Vector2Int(x, y);
            if (inMap.GetNearMovablePosition(cell) == cell) return (x, y);
        }
        return null;
    }
}
