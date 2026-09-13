using MyOnOff.Protocol;

namespace MyOnOff.DesktopController;

internal sealed record EasyModePresentation(
    string Status,
    string Detail,
    bool ShowProgress,
    bool ShowOnButton)
{
    public static EasyModePresentation For(HostState state) => state switch
    {
        HostState.Online => new("ON", "Host is ready.", false, false),
        HostState.Booting => new("Turning on", "Please wait while the host becomes ready.", true, false),
        HostState.Offline => new("Host is off", "Press ON to wake the host.", false, true),
        _ => new("Checking status", "Checking the local network.", false, false)
    };
}
