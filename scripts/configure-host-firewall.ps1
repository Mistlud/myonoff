[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateRange(1, 65535)]
    [int]$AgentPort = 5055,

    [ValidatePattern('^\d{1,3}(\.\d{1,3}){3}/\d{1,2}$')]
    [string]$RemoteSubnet = '192.168.219.0/24',

    [string]$RuleName = 'MyOnOff Host Agent (LAN only)'
)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an elevated PowerShell window.'
}

if (Get-NetFirewallRule -DisplayName $RuleName -ErrorAction SilentlyContinue) {
    if ($PSCmdlet.ShouldProcess($RuleName, 'Replace existing firewall rule')) {
        Remove-NetFirewallRule -DisplayName $RuleName
    }
}

if ($PSCmdlet.ShouldProcess($RuleName, "Allow TCP $AgentPort from $RemoteSubnet")) {
    New-NetFirewallRule `
        -DisplayName $RuleName `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort $AgentPort `
        -RemoteAddress $RemoteSubnet `
        -Profile Private
}
