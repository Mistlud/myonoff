using System.Windows.Controls;
using System.Windows.Shapes;

namespace MyOnOff.DesktopController;

public partial class MainWindow
{
    private Grid NormalModePanel = null!;
    private Grid EasyModePanel = null!;
    private TextBlock LastRefreshText = null!;
    private Ellipse StatusIndicator = null!;
    private TextBlock StatusText = null!;
    private TextBlock IpText = null!;
    private TextBlock AgentText = null!;
    private TextBlock SmbText = null!;
    private TextBlock LatencyText = null!;
    private TextBlock DetailsText = null!;
    private Button OnButton = null!;
    private Button SleepButton = null!;
    private Button ShutdownButton = null!;
    private Button SettingsButton = null!;
    private Button EasyModeButton = null!;
    private Ellipse EasyStatusIndicator = null!;
    private TextBlock EasyStatusText = null!;
    private TextBlock EasyStatusDetailText = null!;
    private ProgressBar EasyProgress = null!;
    private Button EasyOnButton = null!;
    private TextBlock VersionText = null!;
    private TextBlock EasyVersionText = null!;

    private void InitializeComponent()
    {
    }
}

public partial class SettingsWindow
{
    private TextBox HostIpBox = null!;
    private TextBox HostMacBox = null!;
    private TextBox BroadcastIpBox = null!;
    private TextBox WolPortBox = null!;
    private TextBox AgentPortBox = null!;
    private TextBox SmbPortBox = null!;
    private TextBox SmbShareBox = null!;
    private TextBox ExpectedHostnameBox = null!;
    private PasswordBox AuthTokenBox = null!;
    private CheckBox StartInEasyModeBox = null!;
    private TextBlock PathText = null!;

    private void InitializeComponent()
    {
    }
}
