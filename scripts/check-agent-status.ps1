[CmdletBinding()]
param(
    [string]$HostIp = '192.168.219.104',

    [ValidateRange(1, 65535)]
    [int]$AgentPort = 5055
)

$uri = "http://${HostIp}:${AgentPort}/status"
Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 5
