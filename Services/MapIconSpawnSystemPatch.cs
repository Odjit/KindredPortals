using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;

namespace KindredPortals.Services;

[HarmonyPatch(typeof(MapIconSpawnSystem), nameof(MapIconSpawnSystem.OnUpdate))]
static class MapIconSpawnSystemPatch
{
    public static void Prefix(MapIconSpawnSystem __instance)
    {
        var entities = __instance.__query_1050583545_0.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            if (!entity.Has<Attach>()) continue;

            var attachParent = entity.Read<Attach>().Parent;
            if (attachParent.Equals(Entity.Null)) continue;

            if (!attachParent.Has<SpawnedBy>()) continue;

            var mapIconData = entity.Read<MapIconData>();

            mapIconData.RequiresReveal = false;
            mapIconData.AllySetting = MapIconShowSettings.Global;
            mapIconData.EnemySetting = MapIconShowSettings.Global;
            entity.Write(mapIconData);
        }
        entities.Dispose();
    }
}
