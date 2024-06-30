using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using VampireCommandFramework;

namespace KindredPortals.Commands.Converters;

public record class FoundMapIcon(PrefabGUID Value);

class FoundMapIconConverter : CommandArgumentConverter<FoundMapIcon>
{
    // Case insensitive dictionary
    static Dictionary<string, PrefabGUID> _mapIconNameToGuid = new(StringComparer.OrdinalIgnoreCase);

    // Excluded names
    static string[] excludedMapIcons = [
        "CastleWaypoint_Active",
        "CharmedUnit",
        "DeathContainer",
        "LocalPlayer",
        "Mount",
        "Player",
        "PlayerCustomMarker",
        "PlayerCustomMarkerPathfindDot",
        "PlayerPathDot",
        "POI_Spawn_CoffinSelect",
        "POI_Spawn_CryptSelect",
        "POI_Spawn_WaypointSelect",
        "RecommendedTerritoryIcon",
        "StartGraveyardExit",
        "StoneCoffin",
        "WoodenCoffin"
    ];

    public static void Initialize()
    {
        foreach(var (prefabGUID, name) in Core.PrefabCollection._PrefabGuidToNameDictionary)
        {
            if (name.StartsWith("MapIcon_"))
            {
                if (excludedMapIcons.Contains(name[8..]))
                    continue;

                if (!Core.PrefabCollection._PrefabGuidToEntityMap.TryGetValue(prefabGUID, out var entity))
                    continue;

                if (!entity.Has<MapIconData>())
                    continue;

                _mapIconNameToGuid[name] = prefabGUID;
            }
        }
    }

    public static IEnumerable<(string, PrefabGUID)> MapIconNames => _mapIconNameToGuid.Select(x => (x.Key, x.Value));

    public override FoundMapIcon Parse(ICommandContext ctx, string input)
    {
        if (int.TryParse(input, out var integral))
        {
            if(!Core.PrefabCollection._PrefabGuidToNameDictionary.TryGetValue(new(integral), out var name))
                throw ctx.Error($"Invalid prefabGUID: {input}");

            if(!name.StartsWith("MapIcon_"))
                throw ctx.Error($"PrefabGUID({input}) is {name} not a MapIcon");

            if (excludedMapIcons.Contains(name[8..]))
                throw ctx.Error($"PrefabGUID({input}) is {name} not a Valid Visible MapIcon");

            return new(new(integral));
        }

        if(_mapIconNameToGuid.TryGetValue(input, out var result))
            return new(result);

        if(_mapIconNameToGuid.TryGetValue("MapIcon_"+input, out result))
            return new(result);

        var searchResults = _mapIconNameToGuid.Where(x => x.Key.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();
        if(searchResults.Count == 1)
            return new(searchResults[0].Value);

        throw ctx.Error("Multiple results be more specific\n"+
            String.Join("\n",searchResults.Select(x => x.Key)));
    }
}
