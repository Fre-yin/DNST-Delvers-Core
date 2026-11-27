using UnityEngine;
#if BEPINEX
using CreateVFXRequest = global::Refactor.FX.CreateVFXRequest;
using FXSpeechParameter = global::Refactor.FX.FXSpeechParameter;
using IFXParameter = global::Refactor.FX.IFXParameter;
using IFXRequest = global::Refactor.FX.IFXRequest;
using SetOwnerVFXRequest = global::Refactor.FX.SetOwnerVFXRequest;
using SetParamVFXRequest = global::Refactor.FX.SetParamVFXRequest;
using SetVFXLocalPositionRequest = global::Refactor.FX.SetVFXLocalPositionRequest;
using SpeechID = global::Refactor.FX.SpeechID;
using VFXID = global::Refactor.FX.VFXID;
#else
using CreateVFXRequest = Il2CppRefactor.FX.CreateVFXRequest;
using FXSpeechParameter = Il2CppRefactor.FX.FXSpeechParameter;
using IFXParameter = Il2CppRefactor.FX.IFXParameter;
using IFXRequest = Il2CppRefactor.FX.IFXRequest;
using SetOwnerVFXRequest = Il2CppRefactor.FX.SetOwnerVFXRequest;
using SetParamVFXRequest = Il2CppRefactor.FX.SetParamVFXRequest;
using SetVFXLocalPositionRequest = Il2CppRefactor.FX.SetVFXLocalPositionRequest;
using SpeechID = Il2CppRefactor.FX.SpeechID;
using VFXID = Il2CppRefactor.FX.VFXID;
#endif

namespace DungeonSettlersDelvers.Core;

// A combo call: the game's speech bubble the way UnitSpeechAction builds one, over the bandit, going with it. The decision runs
// inside the game's unit tick, so the call waits for the frame's end (DelversCoreRuntime.LateFrame). The bubble keeps the game's
// colours for now; the bandits' red comes back with the final texts.
internal static class BanditComboCall
{
    private const float HeightBase = 1f;
    private const float HeightScale = 0.4f;
    private const float HeadRoom = 0.8f;
    private const int QueueCap = 16;
    private const int FxSalt = 0x5340;

    private static readonly Queue<(UnitEntity Unit, BanditCallKind Kind)> waiting = new();
    private static readonly Dictionary<BanditCallKind, int> last = new();
    private static int counter;

    internal static void Reset()
    {
        waiting.Clear();
        last.Clear();
    }

    internal static void Queue(UnitEntity unit, BanditCallKind kind)
    {
        if (unit != null && waiting.Count < QueueCap) waiting.Enqueue((unit, kind));
    }

    internal static void LateFrame()
    {
        while (waiting.Count > 0)
        {
            var (unit, kind) = waiting.Dequeue();
            Say(unit, kind);
        }
    }

    private static void Say(UnitEntity unit, BanditCallKind kind)
    {
        if (unit.IsDead || GroupAiBandits.IsDowned(unit) || !CoreGame.TryMapOfUnit(unit, out var map)) return;
        var maps = CoreGame.Flow?._inMapDatas;
        var inMap = maps != null && maps.ContainsKey(map) ? maps[map] : null;
        var dispatcher = CoreGame.Flow?._viewDispatcher;
        if (inMap == null || dispatcher == null) return;
        var index = BanditComboCallText.Pick(BanditComboCallText.CountOf(kind), last.TryGetValue(kind, out var shown) ? shown : -1,
            CoreRandom.Next01(CoreRandomRules.BanditCalls, unit.Guid.ToString()));
        last[kind] = index;
        var speech = (SpeechID)BanditComboCallText.SpeechId(kind, index);
        var fx = GuidGenerator.GuidInt(unit.Guid, FxSalt + ++counter).Generate();
        var height = DataSheetManager.Instance?.GetUnitData(unit.Key)?.SpriteHeight ?? HeightBase;
        Send(dispatcher, map, new CreateVFXRequest(fx, VFXID.Speech, inMap.GetWorldPosition(unit.Guid)).Cast<IFXRequest>());
        Send(dispatcher, map, new SetOwnerVFXRequest(fx, unit.Cast<IEntity>()).Cast<IFXRequest>());
        Send(dispatcher, map, new SetVFXLocalPositionRequest(fx, new Vector3(0f, (height - HeightBase) * HeightScale + HeadRoom, 0f)).Cast<IFXRequest>());
        Send(dispatcher, map, new SetParamVFXRequest(fx, new FXSpeechParameter(speech).BoxIl2CppObject().Cast<IFXParameter>()).Cast<IFXRequest>());
    }

    private static void Send(ViewUpdateDispatcher dispatcher, MapType map, IFXRequest request)
        => dispatcher.AddViewUpdateCall(new ViewFXRequestData(UpdateType.FXRequested, map, request).Cast<IViewUpdateData>());
}
