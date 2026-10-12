using System;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using SharpHook.Data;
using SharpHook.Simulation;
using SharpHook;
using System.Diagnostics;

namespace AvaloniaApp.Services;

public class KestrelWebSocketServer
{
    private IHost? _host;
    private WebSocket? _clientSocket;
    private string? _clientCode;
    private readonly object _clientLock = new();
    private IEventSimulator simulation;

    public event Action<string>? Log;
    public event Action<string>? StatusChanged;

    public string WebSocketUrl
    {
        get
        {
            var uri = new Uri(_url);
            var builder = new UriBuilder(uri)
            {
                Scheme = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
                Path = $"{uri.AbsolutePath.TrimEnd('/')}/ws"
            };

            return builder.Uri.ToString().TrimEnd('/');
        }
    }

    public bool IsRunning => _host != null;

    public bool IsClientConnected
    {
        get
        {
            lock (_clientLock)
                return _clientSocket != null && _clientSocket.State == WebSocketState.Open;
        }
    }

    public string? ClientCode
    {
        get
        {
            lock (_clientLock)
                return _clientCode;
        }
    }

    private readonly string _url;

    public KestrelWebSocketServer(string url = "http://0.0.0.0:5000")
    {
        _url = url;
        Log?.Invoke("KestrelWebSocketServer constructed");
        StatusChanged?.Invoke(Status());
        simulation = EventSimulator.Create("Connect");
    }

    public void MoveMouse(short dx, short dy)
    {
        simulation.SimulateMouseMovementRelative(dx, dy);

    }

    public void ClickMouse(MouseButton button)
    {
        var pressResult = simulation.SimulateMousePress(button);
        var releaseResult = simulation.SimulateMouseRelease(button);
        var message = $"Mouse {button} click: press={pressResult}, release={releaseResult}";
        Log?.Invoke(message);
        Debug.WriteLine(message);
    }

    public void ScrollMouse(double dy)
    {
        var rotation = (short)Math.Clamp(Math.Round(dy * 120), short.MinValue, short.MaxValue);
        simulation.SimulateMouseWheel(rotation, MouseWheelScrollDirection.Vertical, MouseWheelScrollType.UnitScroll);
    }



    public async Task StartAsync()
    {
        if (_host != null) return;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(_url);
                webBuilder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Map("/ws", wsApp => wsApp.Run(AcceptWsAsync));
                    app.Run(async ctx =>
                    {
                        ctx.Response.StatusCode = 404;
                        await ctx.Response.WriteAsync("Not Found");
                    });
                });
            });

        _host = builder.Build();
        await _host.StartAsync();
        Log?.Invoke($"Kestrel listening at {_url}/ws");
        StatusChanged?.Invoke(Status());
    }

    private async Task AcceptWsAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        lock (_clientLock)
        {
            if (_clientSocket != null && _clientSocket.State == WebSocketState.Open)
            {
                context.Response.StatusCode = 503;
                Log?.Invoke("Rejected connection: a client is already connected.");
                return;
            }
        }
        try
        {
            Log?.Invoke($"Accepting websocket from {context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}");


            if (context.Request.Headers.ContainsKey("User-Agent"))
                Log?.Invoke($"UA: {context.Request.Headers["User-Agent"]}");
            if (context.Request.Headers.ContainsKey("Sec-WebSocket-Protocol"))
                Log?.Invoke($"Protocol: {context.Request.Headers["Sec-WebSocket-Protocol"]}");

            var ws = await context.WebSockets.AcceptWebSocketAsync();

            var code = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4", CultureInfo.InvariantCulture);
            lock (_clientLock)
            {
                _clientSocket = ws;
                _clientCode = code;
            }
            StatusChanged?.Invoke(Status());

            try
            {
                var welcome = JsonSerializer.SerializeToUtf8Bytes(new { server = "connected" });
                await ws.SendAsync(new ArraySegment<byte>(welcome), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log?.Invoke("Failed to send welcome message: " + ex.Message);
            }

            Log?.Invoke("Client connected (Kestrel).");

            await HandleClientAsync(ws, code);
        }
        catch (Exception ex)
        {
            Log?.Invoke("AcceptWebSocketAsync failed: " + ex.Message);
            context.Response.StatusCode = 500;
            return;
        }
    }




    private async Task HandleClientAsync(WebSocket socket, string code)
    {
        var buffer = new byte[4096];
        var isAuthenticated = false;
        var failedVerificationAttempts = 0;
        using var verificationTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var seg = new ArraySegment<byte>(buffer);
                var receiveToken = isAuthenticated ? CancellationToken.None : verificationTimeout.Token;
                var result = await socket.ReceiveAsync(seg, receiveToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Log?.Invoke("Client requested close. Closing connection.");
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var sb = new StringBuilder();
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                while (!result.EndOfMessage)
                {
                    result = await socket.ReceiveAsync(seg, receiveToken);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }

                var text = sb.ToString();
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;

                    if (!isAuthenticated)
                    {
                        if (root.ValueKind == JsonValueKind.Object
                            && root.TryGetProperty("type", out var authType)
                            && authType.ValueKind == JsonValueKind.String
                            && authType.GetString() == "verify"
                            && root.TryGetProperty("code", out var providedCode)
                            && providedCode.ValueKind == JsonValueKind.String
                            && IsValidCode(code, providedCode.GetString()))
                        {
                            isAuthenticated = true;
                            verificationTimeout.Cancel();
                            var verified = JsonSerializer.SerializeToUtf8Bytes(new { server = "verified" });
                            await socket.SendAsync(new ArraySegment<byte>(verified), WebSocketMessageType.Text, true, CancellationToken.None);
                            Log?.Invoke("Client verified its session code.");
                        }
                        else
                        {
                            failedVerificationAttempts++;
                            if (await RejectFailedVerificationAsync(socket, failedVerificationAttempts))
                                break;
                        }

                        continue;
                    }

                    if (root.ValueKind != JsonValueKind.Object)
                        continue;

                    if (root.TryGetProperty("type", out var typeElem) && typeElem.ValueKind == JsonValueKind.String)
                    {
                        switch (typeElem.GetString())
                        {
                            case "click" when root.TryGetProperty("button", out var buttonElem)
                                && buttonElem.ValueKind == JsonValueKind.String
                                && TryParseMouseButton(buttonElem.GetString(), out var button):
                                ClickMouse(button);
                                break;
                            case "scroll" when root.TryGetProperty("dy", out var scrollDy)
                                && scrollDy.TryGetDouble(out var dy):
                                ScrollMouse(dy);
                                break;
                            case "move" when root.TryGetProperty("dx", out var moveDx)
                                && moveDx.TryGetDouble(out var dx)
                                && root.TryGetProperty("dy", out var moveDy)
                                && moveDy.TryGetDouble(out var moveY):
                                MoveMouse((short)Math.Round(dx), (short)Math.Round(moveY));
                                break;
                        }
                    }


                    var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
                    Debug.WriteLine(pretty);
                    Log?.Invoke("Received JSON message:\n" + pretty);
                }
                catch (JsonException)
                {
                    Log?.Invoke("Received (non-JSON) message:\n" + text);
                    if (!isAuthenticated)
                    {
                        failedVerificationAttempts++;
                        if (await RejectFailedVerificationAsync(socket, failedVerificationAttempts))
                            break;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!isAuthenticated)
        {
            Log?.Invoke("Client failed to verify within 10 minutes; disconnecting.");
            socket.Abort();
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
                {
                    _clientSocket = null;
                    _clientCode = null;
                }
            }
            Log?.Invoke("Client disconnected.");
            StatusChanged?.Invoke(Status());
        }
    }

    private async Task<bool> RejectFailedVerificationAsync(WebSocket socket, int attempts)
    {
        Log?.Invoke($"Client verification failed ({attempts}/3).");
        if (attempts < 3)
            return false;

        Log?.Invoke("Client exceeded three failed verification attempts; disconnecting.");
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Too many incorrect pairing codes", CancellationToken.None);
        return true;
    }

    private static bool IsValidCode(string expectedCode, string? providedCode)
    {
        return providedCode != null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedCode),
            Encoding.UTF8.GetBytes(providedCode));
    }

    private static bool TryParseMouseButton(string? value, out MouseButton button)
    {
        button = value?.Trim().ToLowerInvariant() switch
        {
            "left" => MouseButton.Button1,
            "right" => MouseButton.Button2,
            "middle" => MouseButton.Button3,
            _ => MouseButton.NoButton
        };

        return button != MouseButton.NoButton;
    }

    public async Task DisconnectClientAsync()
    {
        WebSocket? s;
        lock (_clientLock) { s = _clientSocket; }
        if (s == null) return;
        try
        {
            if (s.State == WebSocketState.Open)
            {
                try
                {
                    var notice = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        type = "server",
                        status = "closing"
                    });
                    await s.SendAsync(new ArraySegment<byte>(notice), WebSocketMessageType.Text, true, CancellationToken.None);
                    Log?.Invoke("Sent server-closing notice to client.");
                }
                catch (Exception ex)
                {
                    Log?.Invoke("Failed to send server-closing notice: " + ex.Message);
                }

                await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disconnect", CancellationToken.None);
            }
            else if (s.State == WebSocketState.CloseReceived)
            {
                await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server disconnect", CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke("Error while disconnecting client: " + ex.Message);
        }
        finally
        {
            lock (_clientLock)
            {
                if (_clientSocket == s)
                {
                    _clientSocket = null;
                    _clientCode = null;
                }
            }
            StatusChanged?.Invoke(Status());
        }
    }

    public async Task StopAsync()
    {
        if (_host == null) return;
        Log?.Invoke("Stopping Kestrel server: disconnecting client first...");
        await DisconnectClientAsync();
        try { await _host.StopAsync(); } catch { }
        _host.Dispose();
        _host = null;
        lock (_clientLock)
            _clientCode = null;
        StatusChanged?.Invoke(Status());
        Log?.Invoke("Kestrel server stopped.");
    }

    public string Status()
    {
        lock (_clientLock)
        {
            var listening = _host != null ? "listening" : "stopped";
            var client = _clientSocket != null && _clientSocket.State == WebSocketState.Open ? "connected" : "none";
            return $"Server: {listening}, Client: {client}";
        }
    }
}
