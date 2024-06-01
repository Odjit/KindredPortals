using KindredPortals.Commands.Converters;
using Stunlock.Core;
using VampireCommandFramework;

namespace KindredPortals.Commands;

[CommandGroup("waygate", "Commands for managing waygates.")]
class WaygateCommands
{
    readonly static PrefabGUID defaultWaygate = new (2107199037);

    [Command("create", "Creates a waygate at the player's location.", adminOnly: true)]
    public static void CreateWaygate(ChatCommandContext ctx, FoundWaygatePrefab foundWaygatePrefab = null)
    {
        if(!Core.WaygateService.CreateWaygate(ctx.Event.SenderCharacterEntity, foundWaygatePrefab?.Value ?? defaultWaygate))
            ctx.Reply("Current chunk already has a waygate");
        else
            ctx.Reply("Waygate created");
    }

    [Command("teleporttoclosest", "Teleports the player to the closest spawned waygate.", adminOnly: true)]
    public static void TeleportToClosestWaygate(ChatCommandContext ctx)
    {
        if(Core.WaygateService.TeleportToClosestWaygate(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Teleported to closest spawned waygate");
        else
            ctx.Reply("No spawned waygate to teleport to");
    }

    [Command("destroy", "Destroys a spawned waygate you're standing on", adminOnly: true)]
    public static void DestroyWaygate(ChatCommandContext ctx)
    {
        if(Core.WaygateService.DestroyWaygate(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Destroyed waygate");
        else
            ctx.Reply("Not standing on a spawned waygate");
    }
}
