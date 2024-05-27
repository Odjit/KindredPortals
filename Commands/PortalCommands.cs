using System.Collections.Generic;
using Unity.Entities;
using VampireCommandFramework;

namespace KindredPortals.Commands;

[CommandGroup("portal", "Commands for managing portals.")]
static class PortalCommands
{
    static Dictionary<Entity, Entity> startPortalLocations = [];
    [Command("start", "Starts creating a portal at the player's location.  Needs a second location for the other end")]
    public static void StartPortal(ChatCommandContext ctx)
    {
        if(Core.PortalService.StartPortal(ctx.Event.SenderCharacterEntity))
            ctx.Reply("Portal connection started");
        else
            ctx.Reply("Can't start a portal connection as this chunk already has 9 portals");
    }

    [Command("end", "Connects the location started creating a portal.")]
    public static void EndPortal(ChatCommandContext ctx)
    {
        var result = Core.PortalService.EndPortal(ctx.Event.SenderCharacterEntity);

        if(result == null)
            ctx.Reply("Portal connection has been created!");
        else
            ctx.Reply("Failed to create portal connection because "+result);
    }
}
