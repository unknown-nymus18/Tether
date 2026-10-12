using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvaloniaApp.Services;

public class WebSocketServer
{
    private readonly HttpListener _listener = new();
    private WebSocket? _clientSocket;
    private readonly object _clientLock = new();
    private CancellationTokenSource? _cts;

    public event Action<string>? Log;
    public event Action<string>? StatusChanged;

    public WebSocketServer(string prefix)
    {
        _listener.Prefixes.Add(prefix);

        // Also try to add a wildcard prefix for emulator / remote clients (may require URL ACL)
        try
        {
            if (Uri.TryCreate(prefix, UriKind.Absolute, out var u))
            {
                var port = u.IsDefaultPort ? 80 : u.Port;
                var wildcard = $"http://+:{port}/";
                if (!_listener.Prefixes.Contains(wildcard))
                    _listener.Prefixes.Add(wildcard);
            }
        }
        catch { /* ignore if invalid */ }

        // Notify that the server instance was created
        Log?.Invoke("WebSocketServer constructed");
        StatusChanged?.Invoke(Status());
    }

    public Task StartAsync()
    {
        if (_listener.IsListening) return Task.CompletedTask;
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException hlex)
        {
            // Common cause: lack of URL ACL for wildcard prefixes on Windows
            Log?.Invoke($"Failed to start HttpListener: {hlex.Message}");
            Log?.Invoke("If you see an access denied error, run the app as Administrator or register the URL ACL:");
            Log?.Invoke("  netsh http add urlacl url=http://+:5000/ user=Everyone");
            Log?.Invoke("Or change the prefix to 'http://localhost:5000/' and connect from the host (not the emulator), or use Kestrel instead of HttpListener.");
            StatusChanged?.Invoke(Status());
            // Don't rethrow; surface failure via logs so UI remains alive
            return Task.CompletedTask;
        }
        _cts = new CancellationTokenSource();
        Log?.Invoke($"Listening at {_listener.Prefixes}");
        StatusChanged?.Invoke(Status());
        _ = AcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_listener.IsListening) return;
        Log?.Invoke("Stopping server: disconnecting client first...");
        await DisconnectClientAsync();
        _cts?.Cancel();
        _listener.Stop();
        StatusChanged?.Invoke(Status());
        Log?.Invoke("Server stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.Url?.AbsolutePath != "/ws")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                lock (_clientLock)
                {
                    if (_clientSocket != null && _clientSocket.State == WebSocketState.Open)
                    {
                        context.Response.StatusCode = 503;
                        context.Response.Close();
                        Log?.Invoke("Rejected connection: a client is already connected.");
                        continue;
                    }
                }

                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                lock (_clientLock)
                {
                    _clientSocket = wsContext.WebSocket;
                }
                Log?.Invoke("Client connected.");
                StatusChanged?.Invoke(Status());
                _ = HandleClientAsync(_clientSocket);
            }
        }
        catch (HttpListenerException) { }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log?.Invoke($"AcceptLoop error: {ex}");
        }
    }

    private async Task HandleClientAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var seg = new ArraySegment<byte>(buffer);
                var result = await socket.ReceiveAsync(seg, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log?.Invoke("Client requested close. Closing connection.");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                int count = result.Count;
                var sb = new StringBuilder();
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));

                while (!result.EndOfMessage)
                {
                    result = await socket.ReceiveAsync(seg, CancellationToken.None);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                var text = sb.ToString();
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    Log?.Invoke("Received JSON message:\n" + pretty);
                }
                catch (JsonException)
                {
                    Log?.Invoke("Received (non-JSON) message:\n" + text);
                }
            }
        }
        catch (WebSocketException wex)
        {
            Log?.Invoke("WebSocket error: " + wex.Message);
        }
        finally
        {
            lock (_clientLock)
            {
                if (_clientSocket == socket)
                    _clientSocket = null;
            }
            Log?.Invoke("Client disconnected.");
            StatusChanged?.Invoke(Status());
        }
    }

    public async Task DisconnectClientAsync()
    {
        WebSocket? s;
        lock (_clientLock) { s = _clientSocket; }

        if (s == null) return;

        try
        {
            if (s.State == WebSocketState.Open || s.State == WebSocketState.CloseReceived)
            {
                await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down client first", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke("Error while disconnecting client: " + ex.Message);
        }
        finally
        {
            lock (_clientLock) { _clientSocket = null; }
            StatusChanged?.Invoke(Status());
        }
    }

    public string Status()
    {
        lock (_clientLock)
        {
            var listening = _listener.IsListening ? "listening" : "stopped";
            var client = _clientSocket != null && _clientSocket.State == WebSocketState.Open ? "connected" : "none";
            return $"Server: {listening}, Client: {client}";
        }
    }
}
