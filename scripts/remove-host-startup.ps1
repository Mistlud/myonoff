[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$TaskName = 'MyOnOff Host Agent'
)

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    if ($PSCmdlet.ShouldProcess($TaskName, 'Remove scheduled task')) {
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    }
}
