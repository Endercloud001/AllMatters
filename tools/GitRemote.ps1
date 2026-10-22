param(
    [Parameter(Mandatory)][ValidateSet('fetch', 'push', 'ls-remote')][string]$Operation,
    [string]$Remote = 'origin',
    [string]$Branch
)
$ErrorActionPreference = 'Stop'
$repoDir = Split-Path -Parent $PSScriptRoot
Push-Location $repoDir
try {
    $arguments = @()
    $configuredProxy = & git config --get http.proxy
    if (!$configuredProxy -and !$env:HTTPS_PROXY -and !$env:HTTP_PROXY -and !$env:ALL_PROXY) {
        $remoteUrl = & git remote get-url $Remote
        if ($LASTEXITCODE -ne 0) { throw 'Unknown Git remote.' }
        $uri = $null
        if ([uri]::TryCreate($remoteUrl, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -in @('http', 'https')) {
            $proxy = [Net.WebRequest]::GetSystemWebProxy().GetProxy($uri)
            if ($proxy -and $proxy -ne $uri) {
                if ($proxy.UserInfo) { throw 'Use existing credential handling for an authenticated proxy.' }
                $arguments += '-c', "http.proxy=$($proxy.AbsoluteUri)"
                Write-Output 'Using the existing system proxy for this command only.'
            }
        }
    }
    $arguments += $Operation, $Remote
    if ($Branch) { $arguments += $Branch }
    & git @arguments
    if ($LASTEXITCODE -ne 0) { throw "git $Operation failed (exit $LASTEXITCODE)." }
}
finally { Pop-Location }
