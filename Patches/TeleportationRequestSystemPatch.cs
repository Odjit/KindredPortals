using HarmonyLib;
using ProjectM;
using Unity.Collections;

namespace KindredPortals.Patches;

[HarmonyPatch(typeof(TeleportationRequestSystem), nameof(TeleportationRequestSystem.OnUpdate))]
static class TeleportationRequestSystemPatch
{
    public static void Prefix(TeleportationRequestSystem __instance)
    {
        var entities = __instance._TeleportRequestQuery.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            var teleportationRequest = entity.Read<TeleportationRequest>();
            
            Core.Log.LogDebug($"Request {teleportationRequest.PlayerEntity} {teleportationRequest.TeleportationType} {teleportationRequest.FromTarget} {teleportationRequest.ToTarget} {teleportationRequest.EnableCheatChecks}");
        }
        entities.Dispose();
    }
}
