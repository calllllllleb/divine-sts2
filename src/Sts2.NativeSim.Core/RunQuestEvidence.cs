// Public map marker history, with the exact-build two-owner source census. Author: XuShuxi.
using System.Text.Json;
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private readonly List<RunQuestMarkerFacts> _runQuestMarkers = [];
    private string? _questEvidenceFailure;

    private void CapturePublicQuestMarkers()
    {
        // XuShuxi: Identity follows the source census AND public ownership AND
        // disjoint point types. A marker boolean alone never supplies identity.
        JsonElement map = JsonSerializer.SerializeToElement(PublicMapSnapshot());
        bool fur = ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")).Any(model => Entry(model!) == "FUR_COAT");
        bool spoils = ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_player!, "Deck")!, "Cards")).Any(model => Entry(model!) == "SPOILS_MAP");
        int act = Convert.ToInt32(ReflectionTools.Get(_run!, "CurrentActIndex"));
        List<RunCoordinate> furCoords = [], spoilsCoords = [];
        foreach (JsonElement point in map.GetProperty("points").EnumerateArray().Where(point => point.GetProperty("has_quest_marker").GetBoolean()))
        {
            string type = point.GetProperty("point_type").GetString()!;
            JsonElement position = point.GetProperty("coord");
            var coord = new RunCoordinate(position.GetProperty("col").GetInt32(), position.GetProperty("row").GetInt32());
            if (fur && type is "Monster" or "Elite") furCoords.Add(coord);
            else if (spoils && act == 1 && type == "Treasure") spoilsCoords.Add(coord);
            else _questEvidenceFailure = $"Uncertified marker ownership at act={act}, point={coord}, type={type}; the two-owner source census requires public FUR_COAT/SPOILS_MAP ownership and its producer-specific point type.";
        }
        void Remember(string model, List<RunCoordinate> coords)
        {
            if (coords.Count == 0) return;
            var ordered = coords.Distinct().OrderBy(coord => coord.Row).ThenBy(coord => coord.Col).ToArray();
            var fact = new RunQuestMarkerFacts(model, act, ordered);
            var old = _runQuestMarkers.LastOrDefault(item => item.ModelId == model && item.Act == act);
            if (old is null || !old.Coordinates.SequenceEqual(ordered)) _runQuestMarkers.Add(fact);
        }
        Remember("FUR_COAT", furCoords); Remember("SPOILS_MAP", spoilsCoords);
    }

    private void ValidatePublicQuestEvidence(PortableRunRoot root)
    {
        int act = root.PublicContext["act_index"].GetInt32();
        foreach (RunQuestMarkerFacts fact in root.QuestMarkers)
            if (fact.ModelId is not ("FUR_COAT" or "SPOILS_MAP") || fact.Act < 0 || fact.Act > act
                || !fact.Coordinates.SequenceEqual(fact.Coordinates.Distinct().OrderBy(coord => coord.Row).ThenBy(coord => coord.Col))
                || fact.ModelId == "SPOILS_MAP" && (fact.Act != 1 || fact.Coordinates.Count != 1)
                || fact.ModelId == "FUR_COAT" && fact.Coordinates.Count > 7)
                throw new ProtocolException("invalid_public_quest_evidence", "Quest history contradicts the pinned public producer contract.");
        if (root.CurrentPlayer.Relics.Any(relic => relic.ModelId == "FUR_COAT") && !root.QuestMarkers.Any(fact => fact.ModelId == "FUR_COAT"))
            throw new ProtocolException("run_quest_evidence_missing", "FUR_COAT needs its publicly observed marked-coordinate journal, including the old act's coordinates.");
        if (root.CurrentPlayer.Relics.Count(relic => relic.ModelId == "FUR_COAT") > 1 || root.CurrentPlayer.Deck.Count(card => card.ModelId == "SPOILS_MAP") > 1)
            throw new ProtocolException("run_quest_instance_evidence_missing", "Duplicate quest owners need publicly distinguished instance histories.");
    }

    private void InstallPublicQuestOwners(PortableRunRoot root)
    {
        int currentAct = root.PublicContext["act_index"].GetInt32();
        object map = ReflectionTools.Get(_run!, "Map")!;
        void Attach(object owner, RunQuestMarkerFacts fact)
        {
            if (fact.Act != currentAct) return;
            foreach (RunCoordinate coord in fact.Coordinates)
            {
                object nativeCoord = ReflectionTools.Create(T("MegaCrit.Sts2.Core.Map.MapCoord"), coord.Col, coord.Row);
                object point = ReflectionTools.Invoke(map, "GetPoint", nativeCoord)
                    ?? throw new ProtocolException("invalid_public_quest_evidence", "Marked point is absent from the frozen public map.");
                string expectedType = fact.ModelId == "SPOILS_MAP" ? "Treasure" : "Monster/Elite";
                string actualType = ReflectionTools.Get(point, "PointType")!.ToString()!;
                if (fact.ModelId == "SPOILS_MAP" ? actualType != "Treasure" : actualType is not ("Monster" or "Elite"))
                    throw new ProtocolException("invalid_public_quest_evidence", $"Expected {expectedType} marked point.");
                ReflectionTools.Invoke(point, "AddQuest", owner);
            }
        }
        foreach (object? relic in ReflectionTools.Enumerate(ReflectionTools.Get(_player!, "Relics")))
            if (Entry(relic!) == "FUR_COAT")
            {
                RunQuestMarkerFacts fact = root.QuestMarkers.Last(item => item.ModelId == "FUR_COAT");
                ReflectionTools.Set(relic!, "FurCoatActIndex", fact.Act);
                ReflectionTools.Set(relic!, "FurCoatCoordCols", fact.Coordinates.Select(coord => coord.Col).ToArray());
                ReflectionTools.Set(relic!, "FurCoatCoordRows", fact.Coordinates.Select(coord => coord.Row).ToArray());
                ReflectionTools.Set(relic!, "FurCoatCoordsSet", true);
                Attach(relic!, fact);
            }
        foreach (object? card in ReflectionTools.Enumerate(ReflectionTools.Get(ReflectionTools.Get(_player!, "Deck")!, "Cards")))
            if (Entry(card!) == "SPOILS_MAP" && root.QuestMarkers.LastOrDefault(item => item.ModelId == "SPOILS_MAP" && item.Act == currentAct) is { } fact)
            {
                RunCoordinate coord = fact.Coordinates.Single();
                ReflectionTools.Set(card!, "SpoilsCoord", ReflectionTools.Create(T("MegaCrit.Sts2.Core.Map.MapCoord"), coord.Col, coord.Row));
                Attach(card!, fact);
            }
    }
}
