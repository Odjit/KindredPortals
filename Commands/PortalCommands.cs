using KindredPortals.Commands.Converters;
using Stunlock.Core;
using System.Collections.Generic;
using Unity.Entities;
using VampireCommandFramework;

namespace KindredPortals.Commands;

[CommandGroup("portal", "Commands for managing portals.")]
static class PortalCommands
{
    readonly static PrefabGUID defaultMapIcon = PrefabGUID.Empty;

    static Dictionary<Entity, Entity> startPortalLocations = [];
    [Command("start", "Starts creating a portal at the player's location.  Needs a second location for the other end", adminOnly: true)]
    public static void StartPortal(ChatCommandContext ctx, FoundMapIcon icon=null)
    {
        if(Core.PortalService.StartPortal(ctx.Event.SenderCharacterEntity, icon?.Value ?? defaultMapIcon))
            ctx.Reply("Portal connection started");
        else
            ctx.Reply("Can't start a portal connection as this chunk already has 9 portals");
    }

    [Command("end", "Connects the location started creating a portal.", adminOnly: true)]
    public static void EndPortal(ChatCommandContext ctx, FoundMapIcon icon = null)
    {
        var result = Core.PortalService.EndPortal(ctx.Event.SenderCharacterEntity, icon?.Value ?? defaultMapIcon);

        if(result == null)
            ctx.Reply("Portal connection has been created!");
        else
            ctx.Reply("Failed to create portal connection because "+result);
    }

    [Command("teleporttoclosest", "Teleports the player to the closest spawned portal.", adminOnly: true)]
    public static void TeleportToClosestPortal(ChatCommandContext ctx)
    {
        if(Core.PortalService.TeleportToClosestPortal(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Teleported to closest spawned portal");
        else
            ctx.Reply("No spawned portals to teleport to");
    }

    [Command("destroy", "Destroys a spawned portal you're standing near", adminOnly: true)]
    public static void DestroyPortal(ChatCommandContext ctx)
    {
        if(Core.PortalService.DestroyPortal(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Destroyed portal and connection.  Reconnect to no longer see them.");
        else
            ctx.Reply("Not standing near a spawned portal");
    }
}
