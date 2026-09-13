using System.Windows;

namespace MyOnOff.DesktopController;

public partial class SettingsWindow : Window
{
    private readonly ControllerSettings _current;

    public SettingsWindow(ControllerSettings current, string settingsPath)
    {
        InitializeComponent();
        _current = current;
        HostIpBox.Text = current.HostIp;
        HostMacBox.Text = current.HostMac;
        BroadcastIpBox.Text = current.BroadcastIp;
        WolPortBox.Text = current.WolPort.ToString();
        AgentPortBox.Text = current.AgentPort.ToString();
        SmbPortBox.Text = current.SmbPort.ToString();
        SmbShareBox.Text = current.SmbShare;
        ExpectedHostnameBox.Text = current.ExpectedHostname;
        AuthTokenBox.Password = current.AuthToken;
        StartInEasyModeBox.IsChecked = current.StartInEasyMode;
        PathText.Text = $"Local settings file: {settingsPath}";
    }

    public ControllerSettings? Settings { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WolPortBox.Text, out var wolPort) ||
            !int.TryParse(AgentPortBox.Text, out var agentPort) ||
            !int.TryParse(SmbPortBox.Text, out var smbPort))
        {
            MessageBox.Show(this, "Ports must be numeric.", "Invalid Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var candidate = _current with
        {
            HostIp = HostIpBox.Text.Trim(),
            HostMac = HostMacBox.Text.Trim(),
            BroadcastIp = BroadcastIpBox.Text.Trim(),
            WolPort = wolPort,
            AgentPort = agentPort,
            SmbPort = smbPort,
            SmbShare = SmbShareBox.Text.Trim(),
            ExpectedHostname = ExpectedHostnameBox.Text.Trim(),
            AuthToken = AuthTokenBox.Password,
            StartInEasyMode = StartInEasyModeBox.IsChecked == true
        };

        var errors = candidate.Validate();
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), "Invalid Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings = candidate;
        DialogResult = true;
    }
}
