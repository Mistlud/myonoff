using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MyOnOff.Protocol;

namespace MyOnOff.DesktopController;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly HostControllerClient _client = new();
    private readonly ControllerOperationCoordinator _operations = new();
    private readonly DispatcherTimer _pollTimer = new();
    private ControllerSettings _settings = new();
    private ProbeResult? _lastProbe;
    private bool _isBusy;
    private bool _isEasyMode;
    private HostState _displayedState = HostState.Unknown;

    public MainWindow()
    {
        InitializeComponent();
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = $"Version {version}";
        EasyVersionText.Text = $"Version {version}";
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _client.Dispose();
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await _settingsStore.LoadAsync();
            _isEasyMode = _settings.StartInEasyMode;
            ApplySettingsToUi();
            ApplyModeToUi();
            ConfigureTimer();
            await RefreshAsync();
            _pollTimer.Start();
        }
        catch (Exception exception)
        {
            await ReportErrorAsync("Unable to load controller settings.", exception);
        }
    }

    private async Task RefreshAsync()
    {
        if (!_operations.TryBeginRefresh())
        {
            return;
        }

        try
        {
            var probe = await _client.ProbeAsync(_settings, CancellationToken.None);
            if (_operations.ShouldApplyRefreshResult)
            {
                _lastProbe = probe;
                RenderProbe(probe);
            }
        }
        catch (Exception exception)
        {
            if (_operations.ShouldApplyRefreshResult)
            {
                _lastProbe = null;
                await ReportErrorAsync("Status check failed.", exception);
                UpdateButtons();
            }
        }
        finally
        {
            _operations.EndRefresh();
        }
    }

    private async void On_Click(object sender, RoutedEventArgs e)
    {
        var validationErrors = _settings.Validate();
        if (validationErrors.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, validationErrors),
                "Check Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await RunActionAsync(
            HostState.Booting,
            "Sending Wake-on-LAN packet...",
            token => _client.SendWakeAsync(_settings, token),
            "Wake-on-LAN packet sent; waiting for Agent and SMB.",
            "Wake-on-LAN send failed.",
            result => result.State == HostState.Online,
            TimeSpan.FromSeconds(90));
    }

    private async void Sleep_Click(object sender, RoutedEventArgs e)
    {
        await RunActionAsync(
            HostState.GoingToSleep,
            "Requesting sleep...",
            token => _client.SendSleepAsync(_settings, token),
            "Sleep request accepted; waiting for the host to become unreachable.",
            "Sleep request failed.",
            result => result.State == HostState.Offline,
            TimeSpan.FromSeconds(45));
    }

    private async void Shutdown_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "Shut down the Host PC normally?",
            "Confirm Shutdown",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunActionAsync(
            HostState.ShuttingDown,
            "Requesting shutdown...",
            token => _client.SendShutdownAsync(_settings, token),
            "Shutdown request accepted; waiting for the host to become unreachable.",
            "Shutdown request failed.",
            result => result.State == HostState.Offline,
            TimeSpan.FromSeconds(45));
    }

    private async Task RunActionAsync(
        HostState transitionalState,
        string progressMessage,
        Func<CancellationToken, Task> action,
        string acceptedMessage,
        string failureMessage,
        Func<ProbeResult, bool> isComplete,
        TimeSpan transitionTimeout)
    {
        if (!_operations.TryBeginAction())
        {
            return;
        }

        try
        {
            SetBusy(true);
            RenderState(transitionalState, progressMessage);
            await action(CancellationToken.None);
            DetailsText.Text = acceptedMessage;
            await AppLog.WriteAsync(acceptedMessage);

            var deadline = DateTimeOffset.UtcNow + transitionTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                _lastProbe = await _client.ProbeAsync(_settings, CancellationToken.None);
                RenderProbe(_lastProbe);
                if (isComplete(_lastProbe))
                {
                    return;
                }

                RenderState(transitionalState, acceptedMessage);
            }

            RenderProbe(_lastProbe ?? await _client.ProbeAsync(_settings, CancellationToken.None));
            DetailsText.Text = $"Transition timed out after {(int)transitionTimeout.TotalSeconds} seconds. " +
                               "Review the current signals and Host Agent log.";
            await AppLog.WriteAsync(DetailsText.Text);
        }
        catch (Exception exception)
        {
            _lastProbe = null;
            await ReportErrorAsync(failureMessage, exception);
        }
        finally
        {
            _operations.EndAction();
            SetBusy(false);
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings, _settingsStore.SettingsPath) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Settings is null)
        {
            return;
        }

        _settings = dialog.Settings;
        await _settingsStore.SaveAsync(_settings);
        ApplySettingsToUi();
        ConfigureTimer();
        await AppLog.WriteAsync("Controller settings updated.");
        await RefreshAsync();
    }

    private void EasyMode_Click(object sender, RoutedEventArgs e)
    {
        _isEasyMode = true;
        ApplyModeToUi();
    }

    private void DetailedMode_Click(object sender, RoutedEventArgs e)
    {
        _isEasyMode = false;
        ApplyModeToUi();
    }

    private void ApplyModeToUi()
    {
        NormalModePanel.Visibility = _isEasyMode ? Visibility.Collapsed : Visibility.Visible;
        EasyModePanel.Visibility = _isEasyMode ? Visibility.Visible : Visibility.Collapsed;
        RenderEasyState(_displayedState);
    }

    private void ConfigureTimer() =>
        _pollTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.PollIntervalSeconds, 1, 60));

    private void ApplySettingsToUi()
    {
        IpText.Text = _settings.HostIp;
        if (string.IsNullOrWhiteSpace(_settings.HostMac))
        {
            DetailsText.Text = "Open Settings and enter the Host PC wired Ethernet MAC address.";
        }
    }

    private void RenderProbe(ProbeResult result)
    {
        RenderState(result.State, result.Snapshot.Error);
        IpText.Text = _settings.HostIp;
        AgentText.Text = result.Snapshot.AgentReachable ? "Ready" : "Unavailable";
        SmbText.Text = result.Snapshot.SmbReachable
            ? $"Ready (\\\\{_settings.HostIp}\\{_settings.SmbShare})"
            : "Unavailable";
        LatencyText.Text = result.Snapshot.LatencyMilliseconds is { } latency
            ? $"{latency} ms"
            : "—";
        LastRefreshText.Text = $"Last checked {DateTime.Now:T}";
        UpdateButtons();
    }

    private void RenderState(HostState state, string? details)
    {
        _displayedState = state;
        StatusText.Text = state switch
        {
            HostState.GoingToSleep => "GOING TO SLEEP",
            HostState.ShuttingDown => "SHUTTING DOWN",
            _ => state.ToString().ToUpperInvariant()
        };
        StatusIndicator.Fill = state switch
        {
            HostState.Online => new SolidColorBrush(Color.FromRgb(18, 183, 106)),
            HostState.Booting => new SolidColorBrush(Color.FromRgb(247, 144, 9)),
            HostState.Offline => new SolidColorBrush(Color.FromRgb(102, 112, 133)),
            HostState.GoingToSleep or HostState.ShuttingDown => new SolidColorBrush(Color.FromRgb(46, 144, 250)),
            _ => new SolidColorBrush(Color.FromRgb(240, 68, 56))
        };
        DetailsText.Text = string.IsNullOrWhiteSpace(details) ? DescribeState(state) : details;
        RenderEasyState(state);
    }

    private void RenderEasyState(HostState state)
    {
        var presentation = EasyModePresentation.For(state);
        EasyStatusIndicator.Fill = state switch
        {
            HostState.Online => new SolidColorBrush(Color.FromRgb(18, 183, 106)),
            HostState.Booting => new SolidColorBrush(Color.FromRgb(247, 144, 9)),
            HostState.Offline => new SolidColorBrush(Color.FromRgb(102, 112, 133)),
            _ => new SolidColorBrush(Color.FromRgb(152, 162, 179))
        };
        EasyStatusText.Text = presentation.Status;
        EasyStatusDetailText.Text = presentation.Detail;
        EasyProgress.Visibility = presentation.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
        EasyOnButton.Visibility = presentation.ShowOnButton ? Visibility.Visible : Visibility.Collapsed;
        EasyOnButton.IsEnabled = !_isBusy && presentation.ShowOnButton;
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        SettingsButton.IsEnabled = !_isBusy;
        EasyModeButton.IsEnabled = !_isBusy;
        OnButton.IsEnabled = !_isBusy;
        EasyOnButton.IsEnabled = !_isBusy && _displayedState == HostState.Offline;
        var agentReady = _lastProbe?.Snapshot is
        {
            AgentReachable: true,
            AgentResponseMalformed: false,
            UnexpectedHost: false,
            AccessDenied: false
        } && !string.IsNullOrWhiteSpace(_settings.AuthToken);
        SleepButton.IsEnabled = !_isBusy && agentReady;
        ShutdownButton.IsEnabled = !_isBusy && agentReady;
    }

    private async Task ReportErrorAsync(string summary, Exception exception)
    {
        RenderState(HostState.Unknown, $"{summary} {exception.Message}");
        await AppLog.WriteAsync($"{summary} {exception.GetType().Name}: {exception.Message}");
    }

    private static string DescribeState(HostState state) => state switch
    {
        HostState.Online => "Host Agent and SMB are ready.",
        HostState.Booting => "The host is reachable but Agent or SMB is not ready yet.",
        HostState.Offline => "No normal host, Agent, or SMB response was received.",
        HostState.Unknown => "The controller cannot confidently classify the host.",
        HostState.GoingToSleep => "Sleep was requested; waiting for the host to become unreachable.",
        HostState.ShuttingDown => "Shutdown was requested; waiting for the host to become unreachable.",
        _ => string.Empty
    };
}
