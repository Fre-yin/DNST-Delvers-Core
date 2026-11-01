using UnityEngine;

namespace DungeonSettlersDelvers.Core;

// Move orders of the bandit AI (formation, kite). Core's runtime wires Order to
// GroupAiZones.OrderMove when it starts and clears it on unload; without it a move is refused and
// logged once. Every order lands in the bandit log (GroupAiBandits.Log2), its end as
// CORE_AI_BANDIT_MOVE_DONE (GroupAiMoveWatch).
internal static class GroupAiBanditMoves
{
    internal static Func<UnitEntity, Vector2Int, string, bool> Order;

    private static bool unwiredLogged;

    internal static bool TryOrder(UnitEntity unit, Vector2Int tile, string reason)
    {
        if (Order == null)
        {
            if (unwiredLogged) return false;
            unwiredLogged = true;
            DelversHost.Info("CORE_AI_BANDIT_MOVE_UNWIRED reason=" + reason);
            return false;
        }
        // (133 kite orders in 2.5 s, each ended "held" a tick later): no order while
        // the unit is stunned, bound or unconscious.
        if (GroupAiMoveWatch.IsHeld(unit)) return false;
        try
        {
            var ok = Order(unit, tile, reason);
            var fields = ok ? GroupAiMoveWatch.Fields(unit.Guid.ToString()) : "";
            GroupAiBandits.Log2($"CORE_AI_BANDIT_MOVE unit={unit.Guid} to={tile.x},{tile.y} reason={reason} ok={ok}{fields}");
            return ok;
        }
        catch (Exception ex)
        {
            DelversHost.Warning("CORE_AI_BANDIT_MOVE_FAILED: " + ex.Message);
            return false;
        }
    }
}
