using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private static readonly string[] CombatPileNames = ["Hand", "DrawPile", "DiscardPile", "ExhaustPile", "PlayPile"];
    private const int RetiredCardVersion = 1;
    private readonly Dictionary<string, object> _baseResetDeckCards = new(StringComparer.Ordinal);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RetiredCardSnapshot(
        [property: JsonRequired] int Version,
        [property: JsonRequired] CardSnapshot Card,
        [property: JsonRequired] ulong OwnerNetId,
        [property: JsonRequired] bool PileIsNull,
        [property: JsonRequired] bool HasBeenRemovedFromState,
        [property: JsonRequired] bool InCombatRegistry,
        [property: JsonRequired] bool InNetCombatCardDb,
        [property: JsonRequired] string? DeckVersionAlias);

    private static ProtocolException RetiredCardError(string path, string detail) =>
        new("unsupported_retired_combat_card", $"{path}: {detail}");

    // The five piles and the CombatState registry are different native collections.
    // Do not substitute PlayerCombatState.AllCards (which only enumerates piles).
    private List<object> NativeCombatCardDomain()
    {
        var cards = new List<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (string name in CombatPileNames)
            foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_pcs!, name)!, "Cards")))
                if (card is not null && seen.Add(card)) cards.Add(card);
        foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "_allCards")))
            if (card is not null && seen.Add(card)) cards.Add(card);
        return cards;
    }

    private void ValidateNativeHistoryCard(object card, HashSet<object> domain, string path)
    {
        if (!ReferenceEquals(ReflectionTools.Get(card, "Owner"), _player))
            throw HistoryReferenceError(path, $"Card {Entry(card)} has a foreign/null Owner.");
        if (ReflectionTools.Get(card, "IsDupe") is true)
            throw HistoryReferenceError(path, $"Card {Entry(card)} is a dupe; dupe lineage is unsupported.");
        if (!domain.Contains(card))
            throw HistoryReferenceError(path, $"Card {Entry(card)} is absent from the explicit combat-card domain (piles/CombatState registry).");
        if (ReflectionTools.Get(card, "Pile") is null) ValidateNativeRetiredCard(card, path);
    }

    private void ValidateNativeRetiredCard(object card, string path)
    {
        if (!ReferenceEquals(ReflectionTools.Get(card, "Owner"), _player)) throw RetiredCardError(path, "Foreign/null Owner.");
        if (ReflectionTools.Get(card, "IsDupe") is true) throw RetiredCardError(path, "Dupe lineage is unsupported.");
        if (ReflectionTools.Get(card, "CloneOf") is not null || ReflectionTools.Get(card, "DupeOf") is not null)
            throw RetiredCardError(path, "Clone lineage is unsupported.");
        if (ReflectionTools.Get(card, "Pile") is not null) throw RetiredCardError(path, "Expected an actual null Pile.");
        if (ReflectionTools.Get(card, "HasBeenRemovedFromState") is not true) throw RetiredCardError(path, "Unremoved no-pile cards are outside the retired profile.");
        if (ReflectionTools.Get(card, "CombatState") is not null || !ReferenceEquals(ReflectionTools.Get(card, "CardScope"), _combat))
            throw RetiredCardError(path, "Expected null card CombatState and executable owner-derived combat CardScope.");
        if (!ReflectionTools.Enumerate(ReflectionTools.Get(_combat!, "_allCards")).Any(c => ReferenceEquals(c, card)))
            throw RetiredCardError(path, "Card is absent from CombatState registry.");
        if (ReflectionTools.Get(card, "Type")?.ToString() != "Power") throw RetiredCardError(path, "Only native retired Power cards are reviewed.");
        if (card.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length != 0)
            throw RetiredCardError(path, "Additional card-subtype private fields require a separate runtime review.");
        object cost = ReflectionTools.Get(card, "EnergyCost")!;
        if (ReflectionTools.Get(cost, "CostsX") is true || ReflectionTools.Get(card, "HasStarCostX") is true
            || ReflectionTools.Enumerate(ReflectionTools.Get(cost, "_localModifiers")).Count != 0
            || ReflectionTools.Enumerate(ReflectionTools.Get(card, "_temporaryStarCosts")).Count != 0)
            throw RetiredCardError(path, "X/local/temporary costs require a separate carrier review.");
        if (Convert.ToInt32(ReflectionTools.Get(card, "LastStarsSpent")) != 0 || Convert.ToInt32(ReflectionTools.Get(card, "BaseReplayCount")) != 0
            || Convert.ToInt32(ReflectionTools.Get(card, "CurrentPlayIndex")) != 0 || ReflectionTools.Get(card, "CurrentTarget") is not null
            || ReflectionTools.Get(cost, "WasJustUpgraded") is true)
            throw RetiredCardError(path, "Non-default play/replay/cost scratch state is outside the reviewed plain retired Power profile.");
        _ = CaptureRetiredDeckAlias(card, path);
    }

    private void ValidateResetDeckIdentity(object deckCard, CardSpec spec, string path)
    {
        object deckPile = ReflectionTools.Get(_player!, "Deck")!;
        if (!ReferenceEquals(ReflectionTools.Get(deckCard, "Owner"), _player)
            || !ReferenceEquals(ReflectionTools.Get(deckCard, "Pile"), deckPile)
            || !ReflectionTools.Enumerate(ReflectionTools.Get(deckPile, "Cards")).Any(c => ReferenceEquals(c, deckCard)))
            throw RetiredCardError(path, "DeckVersion must be an actual self-owned member of this player's Deck.");
        if (Entry(deckCard) != spec.ModelId || Convert.ToInt32(ReflectionTools.Get(deckCard, "CurrentUpgradeLevel")) != spec.Upgrades)
            throw RetiredCardError(path, "DeckVersion native model/upgrade identity differs from its exact base_reset alias.");
        object? enchantment = ReflectionTools.Get(deckCard, "Enchantment");
        if ((spec.Enchantment is null) != (enchantment is null)
            || spec.Enchantment is { } saved && (Entry(enchantment!) != saved.ModelId || Convert.ToDecimal(ReflectionTools.Get(enchantment!, "Amount")) != saved.Amount))
            throw RetiredCardError(path, "DeckVersion enchantment differs from base_reset identity.");
        foreach (var (name, value) in spec.NativeState ?? new Dictionary<string, JsonElement>())
            if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(ReflectionTools.Get(deckCard, name)), JsonNode.Parse(value.GetRawText())))
                throw RetiredCardError(path, $"DeckVersion saved property {name} differs from base_reset identity.");
    }

    private string? CaptureRetiredDeckAlias(object card, string path)
    {
        if (ReflectionTools.Get(card, "DeckVersion") is not { } deck) return null;
        var aliases = _baseResetDeckCards.Where(pair => ReferenceEquals(pair.Value, deck)).Select(pair => pair.Key).ToArray();
        if (aliases.Length != 1) throw RetiredCardError(path, "DeckVersion lacks one exact native base_reset alias; foreign/non-reset Deck versions are unsupported.");
        CardSpec[] specs = _reset!.Deck.Where(s => s.InstanceId == aliases[0]).ToArray();
        if (specs.Length != 1 || specs[0].ModelId != Entry(card))
            throw RetiredCardError(path, "DeckVersion alias is absent/ambiguous or has a different card model in base_reset.Deck.");
        ValidateResetDeckIdentity(deck, specs[0], path + ".DeckVersion");
        return aliases[0];
    }

    private void ValidateRetiredDeckAliases(CombatSnapshot snapshot, ResetRequest reset)
    {
        foreach (RetiredCardSnapshot retired in snapshot.RetiredCards)
        {
            if (retired.DeckVersionAlias is not { } alias) continue;
            CardSpec[] specs = reset.Deck.Where(s => s.InstanceId == alias).ToArray();
            if (string.IsNullOrWhiteSpace(alias) || specs.Length != 1 || specs[0].ModelId != retired.Card.ModelId)
                throw RetiredCardError("RetiredCard." + retired.Card.InstanceId + ".DeckVersionAlias", "Alias must name one exact matching base_reset.Deck instance; model-name lookup is unsupported.");
        }
    }

    private Dictionary<string, object?> ResolveRetiredDeckBindings(CombatSnapshot snapshot)
    {
        ValidateRetiredDeckAliases(snapshot, _reset!);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (RetiredCardSnapshot retired in snapshot.RetiredCards)
        {
            object? deck = null;
            if (retired.DeckVersionAlias is { } alias)
            {
                if (!_baseResetDeckCards.TryGetValue(alias, out deck))
                    throw RetiredCardError("RetiredCard." + retired.Card.InstanceId, "Destination reset lacks the exact native DeckVersion alias.");
                ValidateResetDeckIdentity(deck, _reset!.Deck.Single(s => s.InstanceId == alias), "RetiredCard." + retired.Card.InstanceId + ".DeckVersion");
            }
            result.Add(retired.Card.InstanceId, deck);
        }
        return result;
    }

    private CardSnapshot CaptureNativeCardSnapshot(object card, object db)
    {
        string id = GetCardInstanceId(card);
        object cost = ReflectionTools.Get(card, "EnergyCost")!;
        object? enchantment = ReflectionTools.Get(card, "Enchantment");
        object? affliction = ReflectionTools.Get(card, "Affliction");
        return new(id, (uint)ReflectionTools.Invoke(db, "GetCardId", card)!, Entry(card),
            Convert.ToInt32(ReflectionTools.Get(card, "CurrentUpgradeLevel")), Convert.ToInt32(ReflectionTools.Invoke(cost, "GetResolved")),
            (bool)ReflectionTools.Get(cost, "CostsX")!, Convert.ToInt32(ReflectionTools.Get(cost, "_base") ?? 0),
            Convert.ToBoolean(ReflectionTools.Get(card, "_hasSingleTurnRetain") ?? false),
            Convert.ToBoolean(ReflectionTools.Get(card, "_hasSingleTurnSly") ?? false), Convert.ToBoolean(ReflectionTools.Get(card, "_exhaustOnNextPlay") ?? false),
            (bool)ReflectionTools.Get(card, "HasBeenRemovedFromState")!,
            enchantment is null ? null : Entry(enchantment), enchantment is null ? 0m : Convert.ToDecimal(ReflectionTools.Get(enchantment, "Amount")),
            SavedNativeState(card), CaptureCardDynamicRuntime(card, "Card." + id),
            affliction is null ? null : Entry(affliction), affliction is null ? 0 : Convert.ToInt32(ReflectionTools.Get(affliction, "Amount")),
            affliction is null ? null : SavedNativeState(affliction));
    }

    private List<RetiredCardSnapshot> CaptureRetiredCards(object db)
    {
        var retired = new List<RetiredCardSnapshot>();
        IDictionary cardToId = (IDictionary)ReflectionTools.Get(db, "_cardToId")!;
        IDictionary idToCard = (IDictionary)ReflectionTools.Get(db, "_idToCard")!;
        var domain = NativeCombatCardDomain();
        if (domain.Any(c => !cardToId.Contains(c)))
            throw RetiredCardError("CombatCards.NetDb", "Explicit combat-card domain contains a native card absent from the ID database.");
        if (cardToId.Count != domain.Count || idToCard.Count != domain.Count)
            throw RetiredCardError("CombatCards.NetDb", "Native DB contains cards outside the reviewed explicit combat-card domain.");
        foreach (object card in domain.Where(c => ReflectionTools.Get(c, "Pile") is null))
        {
            ValidateNativeRetiredCard(card, "RetiredCard." + GetCardInstanceId(card));
            if (!cardToId.Contains(card) || !ReferenceEquals(idToCard[cardToId[card]!], card))
                throw RetiredCardError("RetiredCard." + GetCardInstanceId(card), "Missing/nonreciprocal native combat card ID.");
            retired.Add(new(RetiredCardVersion, CaptureNativeCardSnapshot(card, db), (ulong)ReflectionTools.Get(_player!, "NetId")!,
                true, true, true, true, CaptureRetiredDeckAlias(card, "RetiredCard." + GetCardInstanceId(card))));
        }
        return retired;
    }

    private void ValidateCombatCardDomain(CombatSnapshot snapshot)
    {
        if (snapshot.RetiredCards is null || snapshot.CombatCardRegistry is null
            || snapshot.RetiredCards.Any(r => r is null || r.Card is null))
            throw RetiredCardError("CombatCards", "Explicit retired collection and ordered CombatState registry are required.");
        CardSnapshot[] cards = AllSnapshotCards(snapshot).ToArray();
        if (cards.Any(c => c is null || string.IsNullOrWhiteSpace(c.InstanceId) || c.CombatCardId >= snapshot.NextCardId)
            || cards.Select(c => c.InstanceId).Distinct(StringComparer.Ordinal).Count() != cards.Length
            || cards.Select(c => c.CombatCardId).Distinct().Count() != cards.Length)
            throw RetiredCardError("CombatCards", "Card instance/native IDs must be one-to-one and below saved next-id.");
        var ids = cards.Select(c => c.InstanceId).ToHashSet(StringComparer.Ordinal);
        if (snapshot.CombatCardRegistry.Distinct(StringComparer.Ordinal).Count() != snapshot.CombatCardRegistry.Count
            || !ids.SetEquals(snapshot.CombatCardRegistry))
            throw RetiredCardError("CombatCardRegistry", "Reviewed profile requires each actual card once in the ordered native registry.");
        foreach (RetiredCardSnapshot retired in snapshot.RetiredCards)
        {
            string path = "RetiredCard." + retired.Card.InstanceId;
            if (retired.Version != RetiredCardVersion) throw RetiredCardError(path + ".Version", "Unknown retired-card version.");
            if (retired.OwnerNetId != 1) throw RetiredCardError(path + ".OwnerNetId", "Foreign owner is outside the single-player retired profile.");
            if (!retired.PileIsNull) throw RetiredCardError(path + ".PileIsNull", "Retired card must have no native Pile.");
            if (!retired.HasBeenRemovedFromState || !retired.Card.HasBeenRemovedFromState)
                throw RetiredCardError(path + ".HasBeenRemovedFromState", "Both actual removed flags must agree with the reviewed retired profile.");
            if (!retired.InCombatRegistry) throw RetiredCardError(path + ".InCombatRegistry", "Unregistered retired cards are outside the reviewed profile.");
            if (!retired.InNetCombatCardDb) throw RetiredCardError(path + ".InNetCombatCardDb", "Retired card must retain its native combat ID binding.");
            object canonical = Find(ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.Models.ModelDb"), "AllCards")!, retired.Card.ModelId);
            if (ReflectionTools.Get(canonical, "Type")?.ToString() != "Power"
                || canonical.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length != 0
                || retired.Card.CostsX)
                throw RetiredCardError(path, "Unreviewed native card subtype/cost profile.");
        }
    }

    private static string CombatCardDomainFingerprint(CombatSnapshot snapshot) => JsonSerializer.Serialize(new {
        snapshot.NextCardId, snapshot.DynamicCardOrdinal, snapshot.CombatCardRegistry, snapshot.RetiredCards }, PortableRootJson);
    private static bool CombatCardDomainFingerprintMatches(CombatSnapshot expected, CombatSnapshot actual) =>
        CombatCardDomainFingerprint(expected) == CombatCardDomainFingerprint(actual);

    private string? CurrentCombatCardDomainFingerprint()
    {
        if (_runMode || _mapMode || _rewardMode || _restMode || _eventMode || _customRewardMode
            || _combat is null || _pcs is null || _player is null || _reset is null || !PlayerAlive() || !Alive("Enemies")) return null;
        object db = ReflectionTools.GetStatic(T("MegaCrit.Sts2.Core.GameActions.Multiplayer.NetCombatCardDb"), "Instance")!;
        return JsonSerializer.Serialize(new {
            NextCardId = (uint)ReflectionTools.Get(db, "_nextId")!, DynamicCardOrdinal = _dynamicCardOrdinal,
            CombatCardRegistry = ReflectionTools.Enumerate(ReflectionTools.Get(_combat, "_allCards")).Select(c => GetCardInstanceId(c!)).ToList(),
            RetiredCards = CaptureRetiredCards(db) }, PortableRootJson);
    }
}
