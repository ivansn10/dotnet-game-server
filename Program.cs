using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:80");
builder.Services.AddSingleton<WebSocketService>();

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/", async context =>
{
    var webSocketService = context.RequestServices.GetRequiredService<WebSocketService>();
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        string webSocketPassword = webSocketService.GeneratePassword();
        //string password = "1234";
        await webSocketService.SendMessage(webSocket, "Password", webSocketPassword);

        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var message = await webSocketService.ReceiveMessage(webSocket);
                if (message != "") Console.WriteLine($"Received: {message}");
                await webSocketService.HandleMessage(webSocket, webSocketPassword, message);
            }
            await webSocketService.CloseConnection(webSocket, webSocketService.Code);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            await webSocketService.CloseConnection(webSocket, webSocketService.Code);
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

await app.RunAsync();

public class WebSocketService
{
    private readonly Dictionary<string, List<WebSocket>> _candidates = new();
    private string _code = "";
    public string Code => _code;

    public WebSocketService()
    {
        Console.WriteLine("WebSocketService created.");
    }

    public async Task<string> ReceiveMessage(WebSocket webSocket)
    { 
        string messageText = string.Empty;
        try
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);

            //Ignore binary messages
            if (result.MessageType == WebSocketMessageType.Binary) return "";

            //Ignore empty messages
            if (string.IsNullOrWhiteSpace(messageText)) return "";

            if (result.CloseStatus.HasValue || result.MessageType == WebSocketMessageType.Close) 
            {
                Console.WriteLine("Connection closed.");
                return "";
            }

            using (JsonDocument.Parse(messageText))
            {
                return messageText;
            }
        }
        catch (JsonException)
        {
            return "";
        }
    }

    public async Task SendMessage(WebSocket webSocket, string type, string content)
    {
        var message = JsonSerializer.Serialize(new ServerMessage 
        { 
            MessageType = type ?? "", 
            MessageContent = content ?? ""
        });
        
        await webSocket.SendAsync(
            Encoding.UTF8.GetBytes(message), 
            WebSocketMessageType.Text, 
            true, 
            CancellationToken.None);
        Console.WriteLine($"Sent: {message}");
    }

    public async Task HandleMessage(WebSocket webSocket, string password, string message)
    {
        try
        {
            ServerMessage? messageObject = JsonSerializer.Deserialize<ServerMessage>(message);
            if (messageObject == null)
            {
                Console.WriteLine("Failed to deserialize message.");
                return;
            }
            switch (messageObject.MessageType)
            {
                case "ConnectionRequest":
                    if (!string.IsNullOrEmpty(messageObject.MessageContent)) 
                    {
                        password = messageObject.MessageContent;
                        Console.WriteLine("Received connection request with password: " + password);
                    }
                    _code = password;
                    HandleConnectionRequest(webSocket, password);
                    break;

                case "SendSDPAnswer":
                case "SendSDPOffer":
                case "RecieveSDPAnswer":
                case "ICECandidate":
                    await ForwardMessage(webSocket, _code, messageObject);
                    break;

                default:
                    Console.WriteLine($"Unknown message type: {messageObject.MessageType}");
                    break;
            }
        }
        catch (JsonException)
        {
            //Not a valid server message
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling message: {ex.Message}");
        }
    }

    private void HandleConnectionRequest(WebSocket webSocket, string password)
    {
        _candidates.TryAdd(password, new List<WebSocket>());
        _candidates[password].Add(webSocket);
        Console.WriteLine("Added candidate with password: " + password + " and count is now: " + _candidates[password].Count);

        if (_candidates[password].Count == 2)
        {
            Console.WriteLine("Found a pair.");
            foreach (WebSocket candidate in _candidates[password])
            {
                if (candidate != webSocket)
                {
                    SendMessage(candidate, "SendSDPOffer", "").Wait();
                    break;
                }
            }
        }
    }

    private async Task ForwardMessage(WebSocket webSocket, string password, ServerMessage messageObject)
    {
        string sdp = messageObject.MessageContent ?? string.Empty;
        foreach (WebSocket candidate in _candidates[password])
        {
            if (candidate != webSocket)
            {
                if (messageObject.MessageType != null)
                {
                    await SendMessage(candidate, messageObject.MessageType, sdp);
                }
                else
                {
                    Console.WriteLine("MessageType is null.");
                }
                break;
            }
        }
    }

    public async Task CloseConnection(WebSocket webSocket, string password)
    {
        try
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived || webSocket.State == WebSocketState.CloseSent)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                Console.WriteLine("Connection closed with candidate with password: " + password);
            }
            else if (webSocket.State == WebSocketState.Aborted)
            {
                Console.WriteLine("WebSocket is in an aborted state and cannot be closed gracefully.");
            }
            else
            {
                Console.WriteLine("WebSocket is not in a state that allows closing: " + webSocket.State);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error closing WebSocket: " + ex.Message);
        }
        finally
        {
            webSocket.Dispose();

            if (_candidates.ContainsKey(password))
            {
                Console.WriteLine("Removing candidate with password: " + password);
                _candidates[password].Remove(webSocket);

                if (_candidates[password].Count == 0)
                {
                    _candidates.Remove(password);
                    Console.WriteLine("Password " + password + " is now available for reuse.");
                }
            }
        }
    }

    public string GeneratePassword()
    {
        string password;
        do
        {
            password = RandomNumberGenerator.GetInt32(1000, 10000).ToString();
        } while (_candidates.ContainsKey(password));
        return password;
    }
}