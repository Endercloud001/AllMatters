[CmdletBinding()]
param(
    [string]$Operation = 'check'  # 'check' = 验证仅，'list' = 列出 worktree
)

$ErrorActionPreference = 'Stop'

try {
    $gitDir = git rev-parse --git-dir 2>$null
    if (-not $gitDir) {
        Write-Error "不在 Git 仓库中"
        exit 1
    }

    # 规范化路径用于比较
    $gitDir = $gitDir.Trim()

    # 检查是否在 worktree 中
    if ($gitDir -like "*worktrees*") {
        Write-Error "当前目录是 worktree，不能在此创建新 worktree`nWorktree 路径: $gitDir`n`n请返回主仓库根目录后再创建新 worktree"
        exit 1
    }

    if ($Operation -eq 'list') {
        Write-Host "当前仓库已有的 worktree："
        git worktree list
        exit 0
    }

    Write-Host "✓ 当前在主仓库根目录" -ForegroundColor Green
    Write-Host "✓ 可安全创建新 worktree" -ForegroundColor Green
    exit 0
}
catch {
    Write-Error "验证失败: $_"
    exit 1
}
