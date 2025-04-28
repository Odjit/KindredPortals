using Stunlock.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using VampireCommandFramework;

namespace KindredPortals.Commands.Converters;


public record class FoundWaygatePrefab(PrefabGUID Value);

class FoundWaygatePrefabConverter : CommandArgumentConverter<FoundWaygatePrefab>
{
    // Case insensitive dictionary
    static Dictionary<string, PrefabGUID> _waygateToGuid = new(StringComparer.OrdinalIgnoreCase);

    public static void Initialize()
    {
        foreach (var entry in Core.PrefabCollection._PrefabLookupMap.GuidToEntityMap)
        {
            var name = Core.PrefabCollection._PrefabLookupMap.GetName(entry.Key);
            if (name.StartsWith("TM_Workstation_Waypoint_World"))
                _waygateToGuid[name] = entry.Key;
        }
    }

    public override FoundWaygatePrefab Parse(ICommandContext ctx, string input)
    {
        if (int.TryParse(input, out var integral))
        {
            if (!Core.PrefabCollection._PrefabLookupMap.TryGetName(new(integral), out var name))
                throw ctx.Error($"Invalid prefabGUID: {input}");

            if (!name.StartsWith("TM_Workstation_Waypoint_World"))
                throw ctx.Error($"PrefabGUID({input}) is {name} not a world waypoint");
            return new(new(integral));
        }

        if(input == "" && _waygateToGuid.TryGetValue("TM_Workstation_Waypoint_World", out var result))
            return new(result);

        if (_waygateToGuid.TryGetValue(input, out result))
            return new(result);

        if (_waygateToGuid.TryGetValue("TM_Workstation_Waypoint_World_" + input, out result))
            return new(result);

        var searchResults = _waygateToGuid.Where(x => x.Key.Contains(input, StringComparison.OrdinalIgnoreCase)).ToList();
        if (searchResults.Count == 1)
            return new(searchResults[0].Value);

        throw ctx.Error("Multiple results be more specific\n" +
            String.Join("\n", searchResults.Select(x => x.Key)));
    }
}
