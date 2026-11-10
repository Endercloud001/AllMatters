[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host "=== GitHub API 诊断工具 ===" -ForegroundColor Cyan

try {
    Write-Host "`n1. 检查身份认证..."
    & gh auth status
    if ($LASTEXITCODE -ne 0) {
        Write-Error "GitHub 认证失败，请运行 'gh auth login'"
        exit 1
    }

    Write-Host "`n2. 检查 API 速率限制..."
    $rateLimit = & gh api rate_limit 2>$null | jq -r '.core.remaining,.core.limit' 2>$null
    if ($LASTEXITCODE -eq 0) {
        $remaining, $limit = $rateLimit -split "`n"
        Write-Host "已用 API 配额: $remaining / $limit"
        if ([int]$remaining -lt 10) {
            Write-Warning "API 配额即将耗尽，请稍候后重试"
            exit 2
        }
    } else {
        Write-Warning "无法获取 API 速率信息，请检查网络连接"
        exit 2
    }

    Write-Host "`n✓ 诊断完成，可以发起 GitHub 操作" -ForegroundColor Green
    exit 0
}
catch {
    Write-Error "诊断过程出错: $_"
    exit 3
}
