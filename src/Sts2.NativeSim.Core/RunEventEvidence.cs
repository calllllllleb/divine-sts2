// Public-derived Event eligibility signatures, never hidden candidates. Author: XuShuxi.
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private readonly List<RunEventSelectionFacts> _runEventSelections = [];
    private RunEventSelectionFacts? _pendingEventEvidence;
    private IReadOnlySet<string>? _eventEligibilityOverride;

    private void InstallRunEventObservers(Action<MethodInfo, string> prefix, Action<MethodInfo, string> postfix)
    {
        MethodInfo pull = T("MegaCrit.Sts2.Core.Models.ActModel").GetMethod("PullNextEvent")!;
        prefix(pull, nameof(CaptureEventSelectionBefore));
        postfix(pull, nameof(CaptureEventSelectionAfter));
        Type eventType = T("MegaCrit.Sts2.Core.Models.EventModel");
        foreach (MethodInfo method in _context.Assembly.GetTypes().Where(type => eventType.IsAssignableFrom(type))
                     .Select(type => type.GetMethod("IsAllowed", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                     .Where(method => method is not null).Select(method => method!).Distinct())
            prefix(method, nameof(ReplayPublicEventEligibility));
    }

    private static void CaptureEventSelectionBefore()
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._runMode || environment._eventEligibilityOverride is not null) return;
        // The exact-build source census proves 35 overrides with public eligibility
        // dependencies. LuminousChoir alone reads/mutates the latent relic bag.
        // Its Gold >= 149 short circuit proves false without touching that bag.
        bool luminousGold = Convert.ToInt32(ReflectionTools.Get(environment._player!, "Gold")) >= 149;
        string[] eligible = ReflectionTools.Enumerate(ReflectionTools.GetStatic(environment.T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllEvents"))
            .Where(model => model is not null && Entry(model) != "LUMINOUS_CHOIR")
            .Where(model => (bool)ReflectionTools.Invoke(model!, "IsAllowed", environment._run!)!)
            .Select(model => Entry(model!)).Order().ToArray();
        int act = Convert.ToInt32(ReflectionTools.Get(environment._run!, "CurrentActIndex"));
        object history = ReflectionTools.Enumerate(ReflectionTools.Get(environment._run!, "MapPointHistory"))[act]!;
        bool lantern = ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(environment._player!, "Deck")!, "Cards"))
            .Any(card => Entry(card!) == "LANTERN_KEY");
        environment._pendingEventEvidence = new(environment._runEventSelections.Count, act,
            ReflectionTools.Enumerate(history).Count, environment._runEvidenceOrdinal++,
            Convert.ToInt32(ReflectionTools.Get(environment._run!, "TotalFloor")), "", eligible, lantern, luminousGold, null);
    }

    private static void CaptureEventSelectionAfter(object __result)
    {
        var environment = _activeEnvironment;
        if (environment?._pendingEventEvidence is not { } evidence || environment._eventEligibilityOverride is not null) return;
        environment._runEventSelections.Add(evidence with { ModelId = Entry(__result) });
        environment._pendingEventEvidence = null;
    }

    private static bool ReplayPublicEventEligibility(object __instance, ref bool __result)
    {
        var mask = _activeEnvironment?._eventEligibilityOverride;
        if (mask is null) return true;
        if (Entry(__instance) == "LUMINOUS_CHOIR")
        {
            var environment = _activeEnvironment!;
            __result = environment._historicalLuminousGold && (bool)ReflectionTools.Invoke(
                ReflectionTools.Get(environment._player!, "RelicGrabBag")!, "HasAvailableRelics", environment._run!)!;
        }
        else __result = mask.Contains(Entry(__instance));
        return false;
    }

    private void ValidatePublicEventEvidence(PortableRunRoot root)
    {
        var observed = root.PublicContext["room_history"].EnumerateArray()
            .GroupBy(point => point.GetProperty("act").GetInt32())
            .SelectMany(group => group.Select((point, index) => (point, index, act: group.Key)))
            .Where(item => item.point.GetProperty("rooms")[0].GetProperty("room_type").GetString() == "Event"
                && item.point.GetProperty("point_type").GetString() != "Ancient").ToArray();
        if (observed.Length != root.EventSelections.Count)
            throw new ProtocolException("run_event_selection_evidence_missing", "Every observed sequence Event requires its public selection-time eligibility signature.");
        string[] allEvents = ReflectionTools.Enumerate(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllEvents"))
            .Select(model => Entry(model!)).ToArray();
        for (int index = 0; index < observed.Length; index++)
        {
            var item = observed[index]; RunEventSelectionFacts evidence = root.EventSelections[index];
            if (evidence.Ordinal != index || evidence.Act != item.act || evidence.HistoryIndex != item.index
                || evidence.ModelId != item.point.GetProperty("rooms")[0].GetProperty("model_id").GetProperty("entry").GetString()
                || !evidence.EligibleEventIds.SequenceEqual(evidence.EligibleEventIds.Distinct().Order())
                || evidence.EligibleEventIds.Any(id => id == "LUMINOUS_CHOIR" || !allEvents.Contains(id)))
                throw new ProtocolException("invalid_public_event_evidence", "Selection ordinal, public room identity or eligibility signature contradicts the pinned source contract.");
            if (evidence.FailureReason is not null)
                throw new ProtocolException("run_event_relic_query_evidence_missing", evidence.FailureReason);
        }
    }

    private object ReplayPublicEventSelection(object act, RunEventSelectionFacts evidence)
    {
        // XuShuxi: Only LanternKey overrides ModifyNextEvent in the source census.
        // Reconstruct that public hook context; the native cyclic scan, final hook
        // fold, visited set and repetition fallback still execute in shipped code.
        object deck = ReflectionTools.Get(_player!, "Deck")!;
        foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(deck, "Cards")))
            if (card is not null) ReflectionTools.Invoke(_run!, "RemoveCard", card);
        ReflectionTools.Invoke(deck, "Clear", true);
        if (evidence.HasLanternKey)
            ReflectionTools.Invoke(deck, "AddInternal", RehydratePublicCard(new("LANTERN_KEY", 0, null)), -1, true);
        _eventEligibilityOverride = evidence.EligibleEventIds.ToHashSet();
        _historicalTotalFloor = evidence.TotalFloor; _historicalLuminousGold = evidence.LuminousGoldEligible;
        try { return ReflectionTools.Invoke(act, "PullNextEvent", _run!)!; }
        finally { _eventEligibilityOverride = null; _historicalTotalFloor = null; }
    }

    private string SampledEventSuffixCommitment()
    {
        // A sampled-world acceptance witness only; never model/training input.
        object[] suffixes = ReflectionTools.Enumerate(ReflectionTools.Get(_run!, "Acts")).Select(act => {
            object rooms = ReflectionTools.Get(act!, "_rooms")!;
            object[] events = ReflectionTools.Enumerate(ReflectionTools.Get(rooms, "events")).Select(model => model!).ToArray();
            int cursor = Convert.ToInt32(ReflectionTools.Get(rooms, "eventsVisited"));
            return (object)new { events = events.Select(Entry).ToArray(), cursor };
        }).ToArray();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(suffixes))));
    }
}
