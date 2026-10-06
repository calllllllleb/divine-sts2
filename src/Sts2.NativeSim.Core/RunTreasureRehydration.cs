// Producer-specific current treasure regeneration. Author: XuShuxi.
using System.Reflection;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // XuShuxi: These belong solely to the independently sampled native world.
    // No materialized hidden result or rarity crosses the portable public root.
    private readonly Dictionary<int, object> _replayedTreasureRarities = [];
    private int? _regeneratingTreasureOperation;

    private RunTreasureFacts? ExportPublicTreasure()
    {
        if (_runStage != "treasure") return null;
        return new(_treasureOpened, !_treasureOpened ? [] :
            ReflectionTools.Enumerate(ReflectionTools.Get(_treasureSynchronizer!, "CurrentRelics"))
                .Select(relic => Entry(relic!)).ToArray());
    }

    private void ValidatePublicTreasure(PortableRunRoot root)
    {
        if (root.Stage != "treasure")
        {
            if (root.Treasure is not null) throw new ProtocolException("invalid_run_modal_evidence", "Treasure facts outside their native owner.");
            return;
        }
        RunTreasureFacts face = root.Treasure ?? throw new ProtocolException("invalid_run_modal_evidence", "Treasure root omitted its public screen.");
        JsonElement[] history = root.PublicContext["room_history"].EnumerateArray().ToArray();
        if (history.Length == 0 || history[^1].GetProperty("rooms")[0].GetProperty("room_type").GetString() != "Treasure"
            || (face.Opened ? face.RelicOptions.Count != 1 : face.RelicOptions.Count != 0))
            throw new ProtocolException("invalid_run_modal_evidence", "A strategic single-player chest requires its public Treasure room and pre-open/one-relic screen.");
        RunRelicOperationFacts[] pulls = CurrentTreasureOperations(root);
        if (pulls.Length != 1)
            throw new ProtocolException("run_treasure_producer_evidence_missing", "BeginRelicPicking requires exactly one current shared pull or the first-chest tutorial constant; suppression/modified producers require their own public evidence.");
        RunRelicOperationFacts pull = pulls[0];
        if (face.Opened && pull.ModelId != face.RelicOptions[0] || !face.Opened && pull.ModelId is not null)
            throw new ProtocolException("invalid_run_modal_evidence", "Current treasure visibility contradicts the ordered public producer evidence.");
    }

    private static RunRelicOperationFacts[] CurrentTreasureOperations(PortableRunRoot root)
    {
        JsonElement last = root.PublicContext["room_history"].EnumerateArray().Last();
        int act = last.GetProperty("act").GetInt32();
        int floor = root.PublicContext["room_history"].GetArrayLength();
        return root.RelicOperations.Where(fact => fact.Bag == "shared" && fact.Act == act && fact.TotalFloor == floor
            && (fact.Producer == "treasure_tutorial_first_chest" || fact.Operation == "front" && fact.Producer.Contains("TreasureRoomRelicSynchronizer"))).ToArray();
    }

    private void RegeneratePublicTreasure(PortableRunRoot root)
    {
        RunRelicOperationFacts pull = CurrentTreasureOperations(root).Single();
        if (!_replayedRelicPulls.ContainsKey(pull.Ordinal) || !_replayedTreasureRarities.ContainsKey(pull.Ordinal))
            throw new ProtocolException("run_treasure_producer_evidence_missing", "No independently conditioned native materialization exists for this producer.");
        object room = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Rooms.TreasureRoom"), root.PublicContext["act_index"].GetInt32());
        ReflectionTools.Set(room, "_runState", _run!);
        ReflectionTools.Invoke(_run!, "PushRoom", room);
        object sync = ReflectionTools.Get(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "TreasureRoomRelicSynchronizer")!;
        // Native BeginRelicPicking owns the session, votes and skip lifecycle.
        // It consumes this world's already-conditioned materialization without
        // drawing or depleting a second time. No factual mutable owner is reused.
        _regeneratingTreasureOperation = pull.Ordinal;
        _replayingRunGenerators = true;
        try { ReflectionTools.Invoke(sync, "BeginRelicPicking"); }
        finally { _regeneratingTreasureOperation = null; _replayingRunGenerators = false; }
        _treasureRoom = room; _treasureSynchronizer = sync;
        _treasureOpened = root.Treasure!.Opened; _treasureResolved = false;
    }

    private void InstallTreasureMaterializationConsumer(Action<MethodInfo, string> prefix)
    {
        Type factory = T("MegaCrit.Sts2.Core.Factories.RelicFactory");
        prefix(factory.GetMethods().Single(method => method.Name == "RollRarity" && method.GetParameters()[0].ParameterType.Name == "Rng"), nameof(ConsumeSampledTreasureRarity));
        Type bag = T("MegaCrit.Sts2.Core.Runs.RelicGrabBag");
        // Only the direct no-filter overload is used by BeginRelicPicking.
        prefix(bag.GetMethods().Single(method => method.Name == "PullFromFront" && method.GetParameters().Length == 2), nameof(ConsumeSampledTreasureRelic));
    }

    private static bool ConsumeSampledTreasureRarity(object __0, ref object? __result)
    {
        var environment = _activeEnvironment;
        if (environment?._regeneratingTreasureOperation is not { } ordinal) return true;
        if (!ReferenceEquals(__0, ReflectionTools.Get(environment._treasureSynchronizer
                ?? ReflectionTools.Get(ReflectionTools.GetStatic(environment.T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "TreasureRoomRelicSynchronizer")!, "_rng")))
            throw new ProtocolException("run_treasure_regeneration_mismatch", "Treasure producer changed its RNG owner.");
        __result = environment._replayedTreasureRarities[ordinal]; return false;
    }

    private static bool ConsumeSampledTreasureRelic(object __instance, ref object? __result)
    {
        var environment = _activeEnvironment;
        if (environment?._regeneratingTreasureOperation is not { } ordinal) return true;
        if (!ReferenceEquals(__instance, ReflectionTools.Get(environment._run!, "SharedRelicGrabBag")))
            throw new ProtocolException("run_treasure_regeneration_mismatch", "Treasure producer changed its bag owner.");
        __result = environment._replayedRelicPulls[ordinal]; return false;
    }
}
