using MyOnOff.DesktopController;
using MyOnOff.Protocol;

namespace MyOnOff.Protocol.Tests;

public sealed class EasyModePresentationTests
{
    [Fact]
    public void OfflineIsTheOnlyStateThatShowsOnButton()
    {
        foreach (var state in Enum.GetValues<HostState>())
        {
            Assert.Equal(state == HostState.Offline, EasyModePresentation.For(state).ShowOnButton);
        }
    }

    [Fact]
    public void UnknownUsesNeutralCheckingPresentation()
    {
        var presentation = EasyModePresentation.For(HostState.Unknown);

        Assert.Equal("Checking status", presentation.Status);
        Assert.False(presentation.ShowProgress);
        Assert.False(presentation.ShowOnButton);
    }

    [Fact]
    public void BootingShowsProgressWithoutOnButton()
    {
        var presentation = EasyModePresentation.For(HostState.Booting);

        Assert.True(presentation.ShowProgress);
        Assert.False(presentation.ShowOnButton);
    }
}
