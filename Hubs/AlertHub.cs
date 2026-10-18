using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LifeLine.Hubs;

/// <summary>
/// The live connection. Every logged-in browser connects once. The server then pushes messages to
/// a single user (Clients.User) or to all admins (the "Admins" group). Browsers never poll.
/// </summary>
[Authorize]
public class AlertHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User != null && Context.User.IsInRole("Admin"))
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        await base.OnConnectedAsync();
    }
}
