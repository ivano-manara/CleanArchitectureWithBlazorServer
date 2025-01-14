using CleanArchitecture.Blazor.Infrastructure.Services.JWT;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace CleanArchitecture.Blazor.Infrastructure.Hubs;

public class HubClient : IAsyncDisposable
{
    public delegate Task MessageReceivedEventHandler(object sender, MessageReceivedEventArgs e);

    private readonly HubConnection _hubConnection;
    private bool _started;

    public HubClient(NavigationManager navigationManager, IAccessTokenProvider authProvider)
    {
        var token = authProvider.AccessToken;
        var hubUrl = navigationManager.BaseUri.TrimEnd('/') + SignalR.HubUrl;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token);
                options.Transports = HttpTransportType.WebSockets;
            })
            .Build();
        _hubConnection.ServerTimeout = TimeSpan.FromSeconds(30);
        _hubConnection.On<string,string>(SignalR.OnConnect, (connectionId, userName) => { Login?.Invoke(this, new UserStateChangeEventArgs(connectionId,userName)); });
        _hubConnection.On<string, string>(SignalR.OnDisconnect, (connectionId, userName) => { Logout?.Invoke(this, new UserStateChangeEventArgs(connectionId, userName)); });
        _hubConnection.On<string>(SignalR.SendNotification,
            message => { NotificationReceived?.Invoke(this, message); });
        _hubConnection.On<string, string>(SignalR.SendMessage, HandleReceiveMessage);
        _hubConnection.On<string, string, string>(SignalR.SendPrivateMessage, HandleReceivePrivateMessage);
        _hubConnection.On<string>(SignalR.JobCompleted, message => { JobCompleted?.Invoke(this, message); });
        _hubConnection.On<string>(SignalR.JobStart, message => { JobStarted?.Invoke(this, message); });
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _hubConnection.StopAsync();
        }
        finally
        {
            await _hubConnection.DisposeAsync();
        }
    }

    public async Task StartAsync(CancellationToken cancellation = default)
    {
        if (_started) return;

        _started = true;
        await _hubConnection.StartAsync(cancellation);
    }


    private void HandleReceiveMessage(string from, string message)
    {
        // raise an event to subscribers
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(from, message));
    }

    private void HandleReceivePrivateMessage(string from, string to, string message)
    {
        // raise an event to subscribers
        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(from, message));
    }

    public async Task SendAsync(string message)
    {
        await _hubConnection.SendAsync(SignalR.SendMessage, message);
    }

    public async Task NotifyAsync(string message)
    {
        await _hubConnection.SendAsync(SignalR.SendNotification, message);
    }

    public event EventHandler<UserStateChangeEventArgs>? Login;
    public event EventHandler<string>? JobStarted;
    public event EventHandler<string>? JobCompleted;
    public event EventHandler<UserStateChangeEventArgs>? Logout;
    public event EventHandler<string>? NotificationReceived;
    public event MessageReceivedEventHandler? MessageReceived;

   
}
public class MessageReceivedEventArgs : EventArgs
{
    public MessageReceivedEventArgs(string userName, string message)
    {
        UserId = userName;
        Message = message;
    }

    public string UserId { get; set; }
    public string Message { get; set; }
}

public class UserStateChangeEventArgs : EventArgs
{
    public UserStateChangeEventArgs(string connectionId, string userName)
    {
        ConnectionId = connectionId;
        UserName = userName;
    }
    public string ConnectionId { get; set; }
    public string UserName { get; set; }
}