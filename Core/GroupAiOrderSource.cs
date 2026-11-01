using HarmonyLib;
#if BEPINEX
using CommandFlow = global::Refactor.Control.CommandFlow;
using DefaultCommand = global::Refactor.Control.DefaultCommand;
using FormationMoveOrder = global::Refactor.Control.FormationMoveOrder;
using MoveOrder = global::Refactor.Control.MoveOrder;
#else
using CommandFlow = Il2CppRefactor.Control.CommandFlow;
using DefaultCommand = Il2CppRefactor.Control.DefaultCommand;
using FormationMoveOrder = Il2CppRefactor.Control.FormationMoveOrder;
using MoveOrder = Il2CppRefactor.Control.MoveOrder;
#endif

namespace DungeonSettlersDelvers.Core;

// Probe (yet up to
// 29 MoveTasks with source=player followed): which of the game's input paths a player's direct order came through. Only
// watches: while one of the senders runs, its name is the context (nested ones joined by '>'); a DirectOrderRequested the
// game enqueues then (TickFlowHandler.EnqueueEvent) is tagged by its task, and TaskSystem.DirectOrder writes the tag as
// via= into CORE_AI_ORDER (GroupAiDirectOrderPatch). Senders: DefaultCommand.Click (the right
// click's entry, with shift it calls MoveOrder.TryMove; DefaultMode.RightClickInteraction holds the same body built in), DefaultCommand.RightClick → CommandFlow.Move → FormationMoveOrder.TryOrder, MoveOrder.Near
// (InteractionRunner), PlayerControlEventHandler.OnEvent(ContinueWaypointRequested). Nothing is changed or blocked.
// DIAGNOSTIC: it runs only while the group AI is on and the probe group probes.commands is on in Core's
// settings (CoreProbeRules; off by default, and then the eight classes below are not patched at all, CoreProbes), is capped, and after
// the finding it stays, shrinks or goes. A player's order in the AI does not depend on it: GroupAiDirectOrderPatch passes the order
// on to GroupAiStance.NotePlayerOrder either way, and without the group Take finds no tag, so via= reads "-".
internal static class GroupAiOrderSource
{
    private const int TagCap = 256;
    private const int LineCap = 300;
    private static readonly List<string> context = new();
    private static readonly Dictionary<IntPtr, string> tags = new();
    private static int lines;

    private static bool Active => CoreProbes.Commands && GroupAiSettings.Enabled;

    internal static string Current => context.Count == 0 ? null : GroupAiRules.OrderVia(context);

    internal static void Reset()
    {
        context.Clear();
        tags.Clear();
        lines = 0;
    }

    // A sender that threw leaves its name behind (no finalizer on the game's methods); a deep context is cleared.
    internal static void Enter(string sender)
    {
        if (!Active) return;
        if (context.Count >= 8) context.Clear();
        context.Add(sender);
    }

    internal static void Leave()
    {
        if (context.Count > 0) context.RemoveAt(context.Count - 1);
    }

    // A DirectOrderRequested enqueued inside a sender: its task carries the path until DirectOrder takes it.
    internal static void NoteEnqueued(IEventData data)
    {
        if (context.Count == 0 || !Active) return;
        var order = data?.TryCast<DirectOrderRequested>();
        var task = order?.Task;
        if (task == null) return;
        if (tags.Count >= TagCap) tags.Clear();
        var via = Current;
        tags[task.Pointer] = via;
        if (lines >= LineCap) return;
        lines++;
        var unit = order.Target?.TryCast<UnitEntity>();
        DelversHost.Info(GroupAiRules.OrderSourceLine(unit?.Guid.ToString(), via, task.GetIl2CppType().Name, CoreGame.Flow?._campaignData?.TimeData?.Tick ?? 0));
    }

    internal static string Take(IntPtr task)
    {
        if (!Active) return null;
        if (!tags.TryGetValue(task, out var via)) return null;
        tags.Remove(task);
        return via;
    }
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(DefaultCommand), nameof(DefaultCommand.Click))]
internal static class GroupAiOrderLeftClickPatch
{
    private static void Prefix(bool alt, bool shift) => GroupAiOrderSource.Enter(GroupAiRules.ClickSender(alt, shift));
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(DefaultCommand), "RightClick")]
internal static class GroupAiOrderRightClickPatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("right-click");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(MoveOrder), nameof(MoveOrder.TryMove))]
internal static class GroupAiOrderTryMovePatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("trymove");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(MoveOrder), nameof(MoveOrder.Near))]
internal static class GroupAiOrderNearPatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("near");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(CommandFlow), nameof(CommandFlow.Move))]
internal static class GroupAiOrderMovePatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("move");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(FormationMoveOrder), nameof(FormationMoveOrder.TryOrder))]
internal static class GroupAiOrderFormationPatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("formation");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(PlayerControlEventHandler), nameof(PlayerControlEventHandler.OnEvent), new[] { typeof(ContinueWaypointRequested) })]
internal static class GroupAiOrderWaypointPatch
{
    private static void Prefix() => GroupAiOrderSource.Enter("waypoint");
    private static void Postfix() => GroupAiOrderSource.Leave();
}

[ProbeGroup(CoreProbeRules.Commands)]
[HarmonyPatch(typeof(TickFlowHandler), nameof(TickFlowHandler.EnqueueEvent))]
internal static class GroupAiOrderEnqueuePatch
{
    private static void Prefix(IEventData eventData)
    {
        try { GroupAiOrderSource.NoteEnqueued(eventData); }
        catch (Exception ex) { GroupAiPolicy.Warn("CORE_AI_ORDER_SOURCE_FAILED: " + ex.Message); }
    }
}
