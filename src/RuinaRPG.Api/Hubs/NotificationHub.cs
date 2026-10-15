using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

/// <summary>
/// Server-to-client only: controllers push per-user events through IHubContext (Clients.Users),
/// so there is nothing for a client to invoke and no group to join.
/// </summary>
[Authorize]
public class NotificationHub : Hub;
