[CmdletBinding()]
param(
    [string]$ProjectFile = (Join-Path $PSScriptRoot '..\src\Tests\AllMatters.Tests.csproj')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ProjectFile -PathType Leaf)) {
    Write-Error "测试项目文件不存在：$ProjectFile"
    exit 1
}

$content = Get-Content -LiteralPath $ProjectFile -Raw
$frameworks = @(
    @{ Name = 'xUnit'; Pattern = '(?i)<PackageReference\s+Include=["'']xunit(["''])' },
    @{ Name = 'NUnit'; Pattern = '(?i)<PackageReference\s+Include=["'']NUnit(["''])' },
    @{ Name = 'MSTest'; Pattern = '(?i)<PackageReference\s+Include=["'']MSTest(["''])' }
)

$matches = @($frameworks | Where-Object { $content -match $_.Pattern } | ForEach-Object Name)
if ($matches.Count -eq 0) {
    Write-Output 'Unknown'
    exit 0
}

$matches | ForEach-Object { Write-Output $_ }
exit 0
