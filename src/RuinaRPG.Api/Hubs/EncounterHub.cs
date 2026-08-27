using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

[Authorize]
public class EncounterHub : Hub
{
    public async Task JoinEncounter(string encounterId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"encounter-{encounterId}");

    public async Task LeaveEncounter(string encounterId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"encounter-{encounterId}");
}
