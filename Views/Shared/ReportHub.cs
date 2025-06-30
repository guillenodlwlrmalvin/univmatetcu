using Microsoft.AspNetCore.SignalR;

namespace UnivMate.Hubs
{
    public class ReportHub : Hub
    {
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }
    }
}