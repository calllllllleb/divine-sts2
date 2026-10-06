// Ordered public relic operations and source-native replay. Author: XuShuxi.
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private readonly List<RunRelicOperationFacts> _runRelicOperations = [];
    private readonly Dictionary<int, string> _unrevealedRelicOperations = [];
    private int _runEvidenceOrdinal, _relicPullDepth, _shopRelicSlot;
    private int _uncertifiedRelicOrdinal;
    private bool _recordingShopRelics, _replayingRunGenerators;
    private int? _historicalTotalFloor;
    private bool _historicalLuminousGold;
    private int? _lastRelicPullOrdinal;
    private readonly Dictionary<int, object> _replayedRelicPulls = [];
    private readonly HashSet<string> _publicRelicAcquisitions = [];

    private void ClearRunGeneratorEvidence()
    {
        _publicRestSelection = null;
        _runEventSelections.Clear(); _pendingEventEvidence = null; _eventEligibilityOverride = null;
        _runRelicOperations.Clear(); _unrevealedRelicOperations.Clear(); _runEvidenceOrdinal = 0;
        _relicPullDepth = 0; _shopRelicSlot = 0; _recordingShopRelics = false;
        _uncertifiedRelicOrdinal = 0;
        _historicalTotalFloor = null; _replayingRunGenerators = false;
        _lastRelicPullOrdinal = null; _replayedRelicPulls.Clear();
        _replayedTreasureRarities.Clear(); _regeneratingTreasureOperation = null;
        _runQuestMarkers.Clear();
        _questEvidenceFailure = null;
        _publicRelicAcquisitions.Clear();
    }

    private void InstallRunRelicObservers(Action<MethodInfo, string> prefix, Action<MethodInfo, string> postfix)
    {
        Type bag = T("MegaCrit.Sts2.Core.Runs.RelicGrabBag");
        foreach (MethodInfo method in bag.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(method => method.Name is "PullFromFront" or "PullFromBack"))
        {
            prefix(method, nameof(CaptureRelicOperationBefore));
            postfix(method, nameof(CaptureRelicOperationAfter));
        }
        foreach (string method in new[] { "Remove", "MoveToFallback" })
            prefix(bag.GetMethods().Single(candidate => candidate.Name == method && !candidate.IsGenericMethod), nameof(CaptureRelicRemoval));
        Type inventory = T("MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory");
        prefix(inventory.GetMethod("PopulateRelicEntries", BindingFlags.NonPublic | BindingFlags.Instance)!, nameof(BeginPublicShopRelicProducer));
        postfix(inventory.GetMethod("PopulateRelicEntries", BindingFlags.NonPublic | BindingFlags.Instance)!, nameof(EndPublicShopRelicProducer));
        prefix(T("MegaCrit.Sts2.Core.Runs.RunState").GetProperty("TotalFloor")!.GetMethod!, nameof(ReplayHistoricalTotalFloor));
        postfix(T("MegaCrit.Sts2.Core.Entities.Players.Player").GetMethod("AddRelicInternal")!, nameof(CapturePublicRelicAcquisition));
        InstallTreasureMaterializationConsumer(prefix);
    }

    private static void CapturePublicRelicAcquisition(object[] __args)
    {
        // RelicCmd.Obtain calls AddRelicInternal before either bag removal and
        // before AfterObtained's continuation. That ownership is public UI state.
        if (_activeEnvironment is { _runMode: true, _replayingRunGenerators: false } environment)
            environment._publicRelicAcquisitions.Add(Entry(__args[0]));
    }

    private static void BeginPublicShopRelicProducer()
    {
        if (_activeEnvironment is { } environment && !environment._replayingRunGenerators)
        { environment._recordingShopRelics = true; environment._shopRelicSlot = 0; }
    }
    private static void EndPublicShopRelicProducer()
    { if (_activeEnvironment is { } environment) environment._recordingShopRelics = false; }

    private static bool ReplayHistoricalTotalFloor(ref int __result)
    {
        if (_activeEnvironment?._historicalTotalFloor is not { } floor) return true;
        __result = floor; return false;
    }

    private string PublicBagOwner(object bag)
    {
        if (ReferenceEquals(bag, ReflectionTools.Get(_run!, "SharedRelicGrabBag"))) return "shared";
        if (ReferenceEquals(bag, ReflectionTools.Get(_player!, "RelicGrabBag"))) return "player";
        // XuShuxi: An unaudited third bag must never inherit player semantics.
        throw new ProtocolException("run_relic_bag_owner_unsupported", "Only the native current player/shared bag owners are certified.");
    }

    private static void CaptureRelicOperationBefore(object __instance, object[] __args, MethodBase __originalMethod,
        out RunRelicOperationFacts? __state)
    {
        __state = null;
        var environment = _activeEnvironment;
        if (environment is null || !environment._runMode || environment._replayingRunGenerators) return;
        if (environment._relicPullDepth++ != 0) return;
        string[] callers = new StackTrace().GetFrames().Select(frame => frame.GetMethod()).Where(method => method is not null)
            .Select(method => method!.DeclaringType?.FullName ?? "").ToArray();
        string producer = callers.FirstOrDefault(name => name.StartsWith("MegaCrit.Sts2.Core.")
            && !name.Contains("RelicGrabBag") && !name.Contains("RelicFactory")) ?? "unknown";
        string? reason = null;
        string rarity;
        if (environment._recordingShopRelics)
            rarity = environment._shopRelicSlot++ < 2 ? "roll" : "Shop";
        else if (callers.Any(name => name.Contains("TreasureRoomRelicSynchronizer"))
            || new StackTrace().GetFrames().Any(frame => frame.GetMethod()?.DeclaringType?.Name == "MerchantRelicEntry"
                && frame.GetMethod()?.Name == "RestockAfterPurchase")) rarity = "roll";
        else
        {
            MethodBase[] frames = new StackTrace().GetFrames().Select(frame => frame.GetMethod()).Where(method => method is not null).Select(method => method!).ToArray();
            bool rolledFactory = frames.Any(method => method.DeclaringType?.Name == "RelicFactory"
                && method.Name.StartsWith("PullNextRelic") && (method.GetParameters().Length == 1
                    || method.GetParameters().Any(parameter => parameter.ParameterType.Name == "Rng")));
            rarity = rolledFactory ? "roll" : __args[0].ToString()!;
            if (frames.Any(method => method.DeclaringType?.Name == "RelicFactory"
                && method.GetParameters().Any(parameter => parameter.ParameterType.Name == "Rng")))
                reason = "RelicFactory Rng override (including CrystalSphere): enclosing public producer and event-RNG chronology are not certified.";
            if (!rolledFactory && !callers.Any(name => name.Contains("RelicReward")))
                reason = $"Unaudited rarity producer {producer}; supplied rarity may be a hidden realization.";
        }
        Delegate? filter = __args.OfType<Delegate>().SingleOrDefault();
        string filterKind = "all";
        string[] blacklist = [];
        if (filter is not null)
        {
            string owner = filter.Method.DeclaringType?.FullName ?? "";
            if (owner.Contains("MerchantRelicEntry"))
            {
                filterKind = "shop_allowed";
                // Source FillSlot's only dynamic filter input is the already
                // offered inventory blacklist. No deque/candidate is inspected.
                object? publicBlacklist = filter.Target is null ? null : ReflectionTools.Get(filter.Target, "blacklist");
                blacklist = ReflectionTools.Enumerate(publicBlacklist).Select(model => Entry(model!)).Distinct().Order().ToArray();
            }
            else if (!owner.Contains("RelicFactory") && !owner.Contains("RelicGrabBag"))
                reason = $"Unaudited filter producer {owner}.{filter.Method.Name}; certified public filter context is required.";
        }
        __state = new(reason is null ? environment._runEvidenceOrdinal++ : --environment._uncertifiedRelicOrdinal, Convert.ToInt32(ReflectionTools.Get(environment._run!, "CurrentActIndex")),
            Convert.ToInt32(ReflectionTools.Get(environment._run!, "TotalFloor")), environment.PublicBagOwner(__instance), producer,
            __originalMethod.Name == "PullFromFront" ? "front" : "back", reason is null ? rarity : null,
            reason is null ? filterKind : null, reason is null ? blacklist : [], null, null, reason);
    }

    private static void CaptureRelicOperationAfter(object? __result, RunRelicOperationFacts? __state)
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._runMode || environment._replayingRunGenerators) return;
        if (--environment._relicPullDepth != 0 || __state is null) return;
        environment._runRelicOperations.Add(__state);
        environment._lastRelicPullOrdinal = __state.Ordinal;
        environment._unrevealedRelicOperations[__state.Ordinal] = __result is null ? "CIRCLET" : Entry(__result);
    }

    private static void CaptureRelicRemoval(object __instance, object[] __args, MethodBase __originalMethod)
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._runMode || environment._replayingRunGenerators || __args.Length != 1) return;
        string model = Entry(__args[0]);
        var frames = new StackTrace().GetFrames();
        bool factoryRemoval = frames.Any(frame => frame.GetMethod()?.DeclaringType?.Name == "RelicFactory");
        bool publicObtain = frames.Any(frame => frame.GetMethod()?.DeclaringType?.FullName?.Contains("RelicCmd") == true)
            && environment._publicRelicAcquisitions.Contains(model);
        bool tutorial = frames.Any(frame => frame.GetMethod()?.Name == "TryGetRelicForTutorial"
            && frame.GetMethod()?.DeclaringType?.Name == "TreasureRoomRelicSynchronizer");
        string? reason = factoryRemoval && environment._lastRelicPullOrdinal < 0 ? "Removal follows an uncertified relic producer." : null;
        var fact = new RunRelicOperationFacts(reason is null ? environment._runEvidenceOrdinal++ : --environment._uncertifiedRelicOrdinal,
            Convert.ToInt32(ReflectionTools.Get(environment._run!, "CurrentActIndex")), Convert.ToInt32(ReflectionTools.Get(environment._run!, "TotalFloor")),
            environment.PublicBagOwner(__instance), tutorial ? "treasure_tutorial_first_chest" : __originalMethod.Name,
            __originalMethod.Name == "Remove" ? "remove" : "fallback",
            null, null, [], factoryRemoval ? environment._lastRelicPullOrdinal : null, publicObtain ? model : null, reason);
        environment._runRelicOperations.Add(fact);
        environment._unrevealedRelicOperations[fact.Ordinal] = model;
    }

    private void RevealPublicRelicOperations(IReadOnlySet<string> revealed, Func<RunRelicOperationFacts, bool> publicProducer)
    {
        for (int index = 0; index < _runRelicOperations.Count; index++)
        {
            RunRelicOperationFacts fact = _runRelicOperations[index];
            if (fact.Act == Convert.ToInt32(ReflectionTools.Get(_run!, "CurrentActIndex"))
                && fact.TotalFloor == Convert.ToInt32(ReflectionTools.Get(_run!, "TotalFloor")) && publicProducer(fact)
                && _unrevealedRelicOperations.TryGetValue(fact.Ordinal, out string? model) && revealed.Contains(model))
            {
                _runRelicOperations[index] = fact with { ModelId = model };
                _unrevealedRelicOperations.Remove(fact.Ordinal);
            }
        }
    }

    private bool ReplayPublicRelicOperation(RunRelicOperationFacts fact)
    {
        object bag = ReflectionTools.Get(fact.Bag == "shared" ? _run! : _player!, fact.Bag == "shared" ? "SharedRelicGrabBag" : "RelicGrabBag")!;
        ReflectionTools.Set(_run!, "CurrentActIndex", fact.Act);
        _historicalTotalFloor = fact.TotalFloor;
        try
        {
            if (fact.Operation is "remove" or "fallback")
            {
                object relic = fact.Producer == "treasure_tutorial_first_chest"
                    ? Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllRelics")!, "GORGET")
                    : fact.PullOrdinal is { } ordinal ? _replayedRelicPulls[ordinal]
                    : Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllRelics")!, fact.ModelId!);
                if (fact.Producer == "treasure_tutorial_first_chest")
                {
                    // BeginRelicPicking rolls before its tutorial constant path.
                    // Consume that source draw on this fresh sampled world only.
                    object rng = ReflectionTools.Get(ReflectionTools.Get(_run!, "Rng")!, "TreasureRoomRelics")!;
                    _replayedTreasureRarities[fact.Ordinal] = ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Factories.RelicFactory"), "RollRarity", rng)!;
                    _replayedRelicPulls[fact.Ordinal] = relic;
                }
                if (fact.ModelId is not null && Entry(relic) != fact.ModelId) return false;
                ReflectionTools.Invoke(bag, fact.Operation == "remove" ? "Remove" : "MoveToFallback", relic);
                return true;
            }
            object rarityRngOwner = fact.Bag == "shared" && fact.Producer.Contains("TreasureRoomRelicSynchronizer")
                ? ReflectionTools.Get(ReflectionTools.Get(_run!, "Rng")!, "TreasureRoomRelics")! : _player!;
            object rarity = fact.RaritySource == "roll" ? ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Factories.RelicFactory"), "RollRarity", rarityRngOwner)!
                : Enum.Parse(T("MegaCrit.Sts2.Core.Entities.Relics.RelicRarity"), fact.RaritySource!);
            if (fact.Producer.Contains("TreasureRoomRelicSynchronizer")) _replayedTreasureRarities[fact.Ordinal] = rarity;
            Type modelType = T("MegaCrit.Sts2.Core.Models.RelicModel");
            var parameter = System.Linq.Expressions.Expression.Parameter(modelType, "relic");
            Func<object, bool> accepts = model => fact.Filter == "all" || (Convert.ToBoolean(ReflectionTools.Get(model, "IsAllowedInShops")) && !fact.Blacklist.Contains(Entry(model)));
            Delegate filter = System.Linq.Expressions.Expression.Lambda(typeof(Func<,>).MakeGenericType(modelType, typeof(bool)),
                System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(accepts), System.Linq.Expressions.Expression.Convert(parameter, typeof(object))), parameter).Compile();
            object? pulled = ReflectionTools.Invoke(bag, fact.Operation == "front" ? "PullFromFront" : "PullFromBack", rarity, filter, _run!);
            object effective = pulled ?? ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Factories.RelicFactory"), "FallbackRelic")!;
            _replayedRelicPulls[fact.Ordinal] = effective;
            return fact.ModelId is null || Entry(effective) == fact.ModelId;
        }
        finally { _historicalTotalFloor = null; }
    }

    private void ValidatePublicRelicEvidence(PortableRunRoot root)
    {
        foreach (RunRelicOperationFacts fact in root.RelicOperations)
        {
            if (fact.FailureReason is not null)
                throw new ProtocolException("run_relic_producer_evidence_missing", fact.FailureReason);
            if (fact.Bag is not ("shared" or "player") || fact.Act < 0 || fact.TotalFloor < 0
                || fact.Operation is not ("front" or "back" or "remove" or "fallback")
                || !fact.Blacklist.SequenceEqual(fact.Blacklist.Distinct().Order())
                || fact.PullOrdinal is { } reference && reference >= fact.Ordinal)
                throw new ProtocolException("invalid_public_relic_evidence", "Invalid public relic operation/eligibility position.");
            if (fact.PullOrdinal is { } causal && !root.RelicOperations.Any(pull => pull.Ordinal == causal
                && pull.Operation is "front" or "back" && pull.Act == fact.Act))
                throw new ProtocolException("invalid_public_relic_evidence", "A removal reference must identify an earlier certified sampled pull in this act.");
            if (fact.Operation is "front" or "back")
            {
                if (fact.RaritySource is not ("roll" or "Common" or "Uncommon" or "Rare" or "Shop")
                    || fact.Filter is not ("all" or "shop_allowed") || fact.PullOrdinal is not null)
                    throw new ProtocolException("invalid_public_relic_evidence", "Invalid source rarity/filter contract.");
            }
            else if (fact.ModelId is null && fact.PullOrdinal is null && fact.Producer != "treasure_tutorial_first_chest")
                throw new ProtocolException("run_unrevealed_relic_removal_evidence_missing", "An unobserved removal needs its source-proven reference to a sampled pull.");
            if (fact.Producer == "treasure_tutorial_first_chest" && (fact.Bag != "shared" || fact.Operation != "remove"
                || fact.ModelId is not (null or "GORGET") || fact.PullOrdinal is not null
                || root.PublicContext["room_history"].EnumerateArray().Count(point => point.GetProperty("rooms")[0].GetProperty("room_type").GetString() == "Treasure") != 1))
                throw new ProtocolException("invalid_public_relic_evidence", "Tutorial constant producer requires the native adapter's first public treasure room.");
        }
        int[] ordinals = root.RelicOperations.Select(fact => fact.Ordinal)
            .Concat(root.EventSelections.Select(fact => fact.OperationOrdinal)).Order().ToArray();
        if (!ordinals.SequenceEqual(Enumerable.Range(0, ordinals.Length)))
            throw new ProtocolException("invalid_public_generator_evidence", "Joint public operation ordinals must be complete and ordered.");
        foreach (string depletion in root.PublicRelicPulls)
            if (!root.RelicOperations.Any(fact => fact.Operation is "front" or "back" && $"{fact.Bag}:{fact.ModelId}" == depletion))
                throw new ProtocolException("run_relic_pull_evidence_missing", "Public depletion has no ordered source/rarity/filter evidence.");
    }

    private bool ReplayPublicGeneratorOperations(PortableRunRoot root, out string contradiction)
    {
        object[] acts = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Select(act => act!).ToArray();
        var operations = root.RelicOperations.Select(fact => (fact.Ordinal, relic: (RunRelicOperationFacts?)fact, ev: (RunEventSelectionFacts?)null))
            .Concat(root.EventSelections.Select(fact => (fact.OperationOrdinal, relic: (RunRelicOperationFacts?)null, ev: (RunEventSelectionFacts?)fact)))
            .OrderBy(item => item.Item1);
        _replayingRunGenerators = true;
        contradiction = "public_generator_operation";
        try
        {
            foreach (var item in operations)
            {
                if (item.relic is { } relic)
                {
                    contradiction = "observed_relic_pull";
                    if (!ReplayPublicRelicOperation(relic)) return false;
                }
                else
                {
                    RunEventSelectionFacts evidence = item.ev!;
                    ReflectionTools.Set(_run!, "CurrentActIndex", evidence.Act);
                    object candidate = ReplayPublicEventSelection(acts[evidence.Act], evidence);
                    contradiction = "observed_Event_selection";
                    if (Entry(candidate) != evidence.ModelId) return false;
                    ReflectionTools.Invoke(acts[evidence.Act], "MarkRoomVisited", Enum.Parse(T("MegaCrit.Sts2.Core.Rooms.RoomType"), "Event"));
                }
            }
            return true;
        }
        finally { _replayingRunGenerators = false; }
    }

    private string SampledRelicSuffixCommitment()
    {
        object[] bags = new[] { ReflectionTools.Get(_run!, "SharedRelicGrabBag")!, ReflectionTools.Get(_player!, "RelicGrabBag")! };
        string[] suffixes = bags.Select(bag => JsonSerializer.Serialize(ReflectionTools.Invoke(bag, "ToSerializable"))).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(suffixes))));
    }
}
