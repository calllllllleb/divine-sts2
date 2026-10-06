// Current mechanical import and joint native latent kernel. Author: XuShuxi.
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private static string PublicRunRootDigest(PortableRunRoot root)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(root, PortableRootJson)));

    public RunMechanicalSnapshot ExportRunMechanicalRoot()
    {
        PortableRunRoot root = ExportRunRoot(); // All existing unsupported-family gates remain mandatory.
        ValidateRunMechanicalDenominator();
        return new(1, PublicRunRootDigest(root), Convert.ToInt32(ReflectionTools.Get(_player!, "MaxEnergy")),
            Convert.ToInt32(ReflectionTools.Get(_player!, "BaseOrbSlotCount")),
            Convert.ToBoolean(ReflectionTools.Get(ReflectionTools.Get(_run!, "ExtraFields")!, "StartedWithNeow")));
    }

    private void ValidateRunMechanicalDenominator()
    {
        // XuShuxi: The current native adapter fixes singleplayer/default acts/no
        // modifiers/UnlockState.none. A future adapter expansion must certify its
        // own prior instead of silently importing a new distribution here.
        if (ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Players")).Count != 1
            // XuShuxi: Construct's existing CreateForTest producer passes enum
            // zero (None), not Standard. Do not change factual mode for tests.
            || ReflectionTools.Get(_run!, "GameMode")!.ToString() != "None"
            || ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Modifiers")).Count != 0
            || !ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Select(act => Entry(act!))
                .SequenceEqual(ReflectionTools.Enumerate(ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Models.ActModel"), "GetDefaultList")).Select(act => Entry(act!))))
            throw new ProtocolException("unsupported_run_mechanical_profile", "Singleplayer default acts without modifiers are required.");
        object character = ReflectionTools.Get(_player!, "Character")!;
        object none = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Unlocks.UnlockState"), new List<string>(), List(T("MegaCrit.Sts2.Core.Models.ModelId"), []), 0);
        if (JsonSerializer.Serialize(ReflectionTools.Invoke(ReflectionTools.Get(_player!, "UnlockState")!, "ToSerializable"), PortableRootJson)
            != JsonSerializer.Serialize(ReflectionTools.Invoke(none, "ToSerializable"), PortableRootJson))
            throw new ProtocolException("unsupported_run_unlock_profile", "This adapter certifies UnlockState.none only; no private unlock profile is imported.");
        if (!Equals(ReflectionTools.Get(_player!, "MaxEnergy"), ReflectionTools.Get(character, "MaxEnergy"))
            || !Equals(ReflectionTools.Get(_player!, "BaseOrbSlotCount"), ReflectionTools.Get(character, "BaseOrbSlotCount")))
            throw new ProtocolException("unsupported_run_base_mechanics", "Player.MaxEnergy/BaseOrbSlotCount: non-character base values need a certified public mutation journal.");
        if (ReflectionTools.Get(_player!, "CanRemovePotions") is false)
            throw new ProtocolException("unsupported_run_potion_lock_continuation", "Player.CanRemovePotions=false is an unsettled Event producer continuation, not a current settled RUN anchor.");
        // Repy is presentation-only (QueenBackground); TestSubjectKills and all
        // discovery/achievement aggregates affect post-run progress, not this run.
        // They are not copied, and neither are combat-local powers or callbacks.
    }

    private void ValidateMechanicalRoot(PortableRunRoot root, RunMechanicalSnapshot? mechanical)
    {
        if (mechanical is null)
            throw new ProtocolException("run_mechanical_root_required", "Production composition requires the private current mechanical anchor. Use the explicit reference oracle only for regression.");
        object character = Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllCharacters")!, root.Initialization.Character);
        if (mechanical.SchemaVersion != 1 || mechanical.PublicRootSha256 != PublicRunRootDigest(root)
            || mechanical.StartedWithNeow // InitializeNewRun + UnlockState.none proves false.
            || mechanical.BaseMaxEnergy != Convert.ToInt32(ReflectionTools.Get(character, "MaxEnergy"))
            || mechanical.BaseOrbSlotCount != Convert.ToInt32(ReflectionTools.Get(character, "BaseOrbSlotCount")))
            throw new ProtocolException("run_mechanical_root_mismatch", "Mechanical schema/public binding/base-mechanics profile mismatch.");
    }

    private void ImportCurrentRunMechanics(PortableRunRoot root, RunMechanicalSnapshot mechanical, long entropy)
    {
        // XuShuxi: Reuse the native object-construction/lifetime path exactly once,
        // with CURRENT public inventory, never initial loadout or factual RNG.
        // Its mechanical-import branch skips InitializeNewRun/ascension effects.
        RunPlayerFacts current = root.CurrentPlayer;
        ResetRequest anchor = IndependentRunReset(new(root.Initialization.Character, root.Initialization.Ascension,
            false, current), entropy, 0);
        ResetCore(anchor, runPriorOnly: true, mechanicalRoot: mechanical);
        _runMode = true;
        _initialCharacterPublicLoadout = root.Initialization.Loadout;
        // Initialization provenance remains separate from the current anchor.
        // In particular, an acquired SpoilsMap is not a custom starting card.
        _reset = IndependentRunReset(root.Initialization, entropy, 0);
        ReflectionTools.Invoke(_manager!, "Reset", true);
    }

    private void GenerateJointRunLatents(string searchSeed)
    {
        // Native ordering: InitializeNewRun's shared bag, then player bag;
        // GenerateRooms's shared ancient allocation, then each Act's full room
        // set and optional second boss. Never independently shuffle marginals.
        object rng = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Runs.RunRngSet"), searchSeed);
        ReflectionTools.Set(_run!, "Rng", rng);
        ReflectionTools.Set(_run!, "Odds", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Odds.RunOddsSet"), ReflectionTools.Get(rng, "UnknownMapPoint")));
        ReflectionTools.Invoke(_player!, "InitializeSeed", searchSeed);
        object shared = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Runs.RelicGrabBag"), true);
        object playerBag = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Runs.RelicGrabBag"));
        ReflectionTools.Set(_run!, "SharedRelicGrabBag", shared);
        ReflectionTools.Set(_player!, "<RelicGrabBag>k__BackingField", playerBag);
        object acts = List(T("MegaCrit.Sts2.Core.Models.ActModel"),
            ReflectionTools.Enumerate(ReflectionTools.InvokeStatic(T("MegaCrit.Sts2.Core.Models.ActModel"), "GetDefaultList"))
                .Select(act => ReflectionTools.Invoke(act!, "ToMutable")!).ToArray());
        ReflectionTools.Set(_run!, "Acts", acts);
        ReflectionTools.Invoke(ReflectionTools.Get(_run!, "_visitedEventIds")!, "Clear");
        _replayedRelicPulls.Clear(); _replayedTreasureRarities.Clear();
        object pool = T("MegaCrit.Sts2.Core.Models.ModelDb").GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Single(method => method.Name == "RelicPool" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(T("MegaCrit.Sts2.Core.Models.RelicPools.SharedRelicPool")).Invoke(null, null)!;
        object unlocked = ReflectionTools.Invoke(pool, "GetUnlockedRelics", ReflectionTools.Get(_run!, "UnlockState"))!;
        ReflectionTools.Invoke(shared, "Populate", unlocked, ReflectionTools.Get(rng, "UpFront"));
        ReflectionTools.Invoke(_player!, "PopulateRelicGrabBagIfNecessary", ReflectionTools.Get(rng, "UpFront"));
        ReflectionTools.Invoke(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!, "GenerateRooms");
    }

    private void ReplaceRunFutureRng(long entropy)
    {
        // XuShuxi: Retain the accepted SEARCH-LOCAL UpFront draw sequence. All
        // other streams get a new independent continuation, and odds are rebound
        // to the new objects; no stale rng reference may survive in a producer.
        object acceptedUpFront = ReflectionTools.Get(ReflectionTools.Get(_run!, "Rng")!, "UpFront")!;
        string seed = $"run-search-future-v1-{entropy:X16}";
        object fresh = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Runs.RunRngSet"), seed);
        IDictionary streams = (IDictionary)ReflectionTools.Get(fresh, "_rngs")!;
        string[] expected = ["UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration", "CombatPotionGeneration",
            "CombatCardSelection", "CombatEnergyCosts", "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics"];
        if (!Enum.GetNames(T("MegaCrit.Sts2.Core.Entities.Rngs.RunRngType")).Order().SequenceEqual(expected.Order())
            || !Enum.GetNames(T("MegaCrit.Sts2.Core.Entities.Rngs.PlayerRngType")).Order().SequenceEqual(new[] { "Rewards", "Shops", "Transformations" }.Order()))
            throw new ProtocolException("unsupported_run_rng_denominator", "Build RNG owner census changed; no future stream may be silently omitted.");
        streams[Enum.Parse(T("MegaCrit.Sts2.Core.Entities.Rngs.RunRngType"), "UpFront")] = acceptedUpFront;
        ReflectionTools.Set(_run!, "Rng", fresh);
        ReflectionTools.Set(_run!, "Odds", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Odds.RunOddsSet"), ReflectionTools.Get(fresh, "UnknownMapPoint")));
        ReflectionTools.Invoke(_player!, "InitializeSeed", seed);
        // InitializeShared binds TreasureRoomRelicSynchronizer to concrete bag/rng
        // objects. Rebuild it AFTER replacing both owners, never copy its relics.
        object manager = ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Runs.RunManager"), "Instance")!;
        ReflectionTools.Set(manager, "TreasureRoomRelicSynchronizer", ReflectionTools.Create(
            T("MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer"), _run!, (ulong)1,
            ReflectionTools.Get(manager, "ActionQueueSynchronizer"), ReflectionTools.Get(_run!, "SharedRelicGrabBag"),
            ReflectionTools.Get(fresh, "TreasureRoomRelics")));
        uint nativeSeed = Convert.ToUInt32(ReflectionTools.Get(fresh, "Seed"));
        ReflectionTools.Set(ReflectionTools.Get(manager, "EventSynchronizer")!, "_multiplayerOptionSelectionRng",
            ReflectionTools.Create(T("MegaCrit.Sts2.Core.Random.Rng"), nativeSeed, "event_synchronizer"));
        ReflectionTools.Set(ReflectionTools.Get(manager, "MapSelectionSynchronizer")!, "_multiplayerMapPointSelection",
            ReflectionTools.Create(T("MegaCrit.Sts2.Core.Random.Rng"), nativeSeed, "map_point_selection"));
    }

    private string SampledRunFutureRngCommitment()
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            run = ReflectionTools.Invoke(ReflectionTools.Get(_run!, "Rng")!, "ToSerializable"),
            player = ReflectionTools.Invoke(ReflectionTools.Get(_player!, "PlayerRng")!, "ToSerializable")
        }, PortableRootJson)));

    private string SampledRunJointLatentCommitment()
        // XuShuxi: Includes unseen Act bosses/ancients as well as all sequences
        // and cursors. This digest is private acceptance data, never model facts.
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            acts = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts"))
                .Select(act => ReflectionTools.Invoke(act!, "ToSave")).ToArray(),
            bags = SampledRelicSuffixCommitment()
        }, PortableRootJson)));

    // XuShuxi: Explicit native fixture only; not advertised by hello, never used
    // by composition, and unavailable without an opt-in process environment.
    // Alter materialized FUTURE suffixes and factual counters, preserving public
    // prefixes/identities. This is an adversarial independence witness, not play.
    public object PerturbRunHiddenFutureForTest()
    {
        if (Environment.GetEnvironmentVariable("STS2_RUN_KERNEL_TEST_SEAM") != "1")
            throw new ProtocolException("test_seam_disabled", "Native hidden-future fixture is disabled.");
        PortableRunRoot root = ExportRunRoot();
        string before = SampledEncounterSuffixCommitment() + SampledEventSuffixCommitment() + SampledRelicSuffixCommitment() + SampledRunFutureRngCommitment();
        static void ReverseSuffix(IList list, int start)
        {
            for (int left = start, right = list.Count - 1; left < right; left++, right--)
                (list[left], list[right]) = (list[right], list[left]);
        }
        foreach (object? act in ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")))
        {
            object rooms = ReflectionTools.Get(act!, "_rooms")!;
            foreach (string category in new[] { "normalEncounters", "eliteEncounters", "events" })
                ReverseSuffix((IList)ReflectionTools.Get(rooms, category)!, Convert.ToInt32(ReflectionTools.Get(rooms, category + "Visited")));
        }
        foreach (object bag in new[] { ReflectionTools.Get(_run!, "SharedRelicGrabBag")!, ReflectionTools.Get(_player!, "RelicGrabBag")! })
            foreach (IList deque in ((IDictionary)ReflectionTools.Get(bag, "_deques")!).Values) ReverseSuffix(deque, 0);
        foreach (object rngSet in new[] { ReflectionTools.Get(_run!, "Rng")!, ReflectionTools.Get(_player!, "PlayerRng")! })
            foreach (object stream in ((IDictionary)ReflectionTools.Get(rngSet, "_rngs")!).Values)
                ReflectionTools.Invoke(stream, "FastForwardCounter", Convert.ToInt32(ReflectionTools.Get(stream, "Counter")) + 37);
        if (PublicRunRootDigest(ExportRunRoot()) != PublicRunRootDigest(root))
            throw new ProtocolException("test_seam_public_mutation", "Hidden fixture altered a public root.");
        string after = SampledEncounterSuffixCommitment() + SampledEventSuffixCommitment() + SampledRelicSuffixCommitment() + SampledRunFutureRngCommitment();
        return new { state = Capture(null), before_commitment = before, after_commitment = after };
    }

    private object ComposeCurrentRunKernel(PortableRunRoot root, RunMechanicalSnapshot mechanical, long entropy)
    {
        Stopwatch wall = Stopwatch.StartNew();
        ImportCurrentRunMechanics(root, mechanical, entropy);
        double importMs = wall.Elapsed.TotalMilliseconds;
        const int maxAttempts = 8192;
        Dictionary<string, int> contradictions = [];
        string contradiction = "joint_native_generator", first = "";
        int attempts;
        for (attempts = 1; attempts <= maxAttempts; attempts++)
        {
            // Only latent owners and the source-proven eligibility context are
            // replaced per trial. No RunReset, map generation, combat, inventory
            // lifecycle, manager initialization or public mechanics rejection.
            GenerateJointRunLatents(RunSearchPriorSeed(entropy, attempts));
            if (ReplayPublicEncounters(root, out contradiction) && ReplayPublicGeneratorOperations(root, out contradiction)) break;
            if (first.Length == 0) first = contradiction;
            contradictions[contradiction] = contradictions.GetValueOrDefault(contradiction) + 1;
        }
        if (attempts > maxAttempts)
            throw new ProtocolException("run_composition_attempts_exhausted",
                $"domain=joint_upfront_kernel; attempts={maxAttempts}; first_contradicted_public_evidence={first}",
                new { domain = "joint_upfront_kernel", attempts = maxAttempts, first_contradicted_public_evidence = first,
                    rejected_public_evidence_counts = contradictions });
        double kernelMs = wall.Elapsed.TotalMilliseconds - importMs;
        ReplaceRunFutureRng(entropy);
        return FinishComposedRunWorld(root, attempts, "current_root_joint_kernel", wall, importMs, kernelMs, contradictions);
    }
}
