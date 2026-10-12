using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaApp.Services;
using System;

namespace AvaloniaApp.Views;

public partial class MainWindow : Window
{
    private readonly KestrelWebSocketServer? _server;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _server = new KestrelWebSocketServer("http://0.0.0.0:5000");
            _server.StatusChanged += OnStatusChanged;
        }
        catch (Exception ex)
        {
            _server = null;
            FeedbackText.Text = ex.Message;
        }

        ServerToggleButton.Click += async (_, _) =>
        {
            if (_server == null) return;

            ServerToggleButton.IsEnabled = false;
            FeedbackText.Text = string.Empty;
            try
            {
                if (_server.IsRunning)
                    await _server.StopAsync();
                else
                    await _server.StartAsync();
            }
            catch (Exception ex)
            {
                FeedbackText.Text = ex.Message;
            }
            finally
            {
                ServerToggleButton.IsEnabled = true;
                UpdateStatus();
            }
        };

        ServerToggleButton.IsEnabled = _server != null;
        ServerUrlText.Text = _server?.WebSocketUrl ?? string.Empty;
        UpdateStatus();
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.UIThread.Post(UpdateStatus);
    }

    private void UpdateStatus()
    {
        var isRunning = _server?.IsRunning == true;
        var isClientConnected = _server?.IsClientConnected == true;
        var clientCode = isClientConnected ? _server?.ClientCode : null;

        ServerStatusText.Text = isRunning ? "Server open" : "Server closed";
        ServerHintText.Text = isRunning ? "Tap to close the server" : "Tap to open the server";
        ClientStatusText.Text = isClientConnected ? "Client connected" : "No client connected";
        ClientCodeLabel.IsVisible = clientCode != null;
        ClientCodeText.IsVisible = clientCode != null;
        ClientCodeText.Text = clientCode ?? string.Empty;

        ServerIndicator.Classes.Remove("offline");
        ServerIndicator.Classes.Remove("online");
        ServerIndicator.Classes.Add(isRunning ? "online" : "offline");

        ClientIndicator.Classes.Remove("offline");
        ClientIndicator.Classes.Remove("online");
        ClientIndicator.Classes.Add(isClientConnected ? "online" : "offline");

        ServerToggleButton.Classes.Remove("stopped");
        ServerToggleButton.Classes.Remove("running");
        ServerToggleButton.Classes.Add(isRunning ? "running" : "stopped");
        ServerToggleButton.Content = isRunning ? "STOP" : "START";
    }
}
