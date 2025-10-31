$ruleName = "Assist Inbound UDP Rule"
$ports = @(12345, 12346, 12347, 12348, 12349, 12350, 12351, 12352)  # example ports used by your app

foreach ($port in $ports) {
    if (-not (Get-NetFirewallRule -DisplayName "$ruleName $port" -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName "$ruleName $port" `
                            -Direction Inbound `
                            -Action Allow `
                            -Protocol UDP `
                            -LocalPort $port `
                            -Profile Any
    }
}
