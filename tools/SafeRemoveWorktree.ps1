param(
    [Parameter(Mandatory = $true)]
    [string]$WorktreePath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $WorktreePath -PathType Container)) {
    throw "Worktree 路径不存在：$WorktreePath"
}

$branch = (git -C $WorktreePath branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($branch)) {
    throw "无法确定 worktree 当前分支：$WorktreePath"
}

$uncommitted = @(git -C $WorktreePath status --short)
$unmerged = @(git -C $WorktreePath log --oneline "$branch" "^origin/main")
$timestamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
$backupDir = Join-Path (Join-Path (Get-Location) '.local/worktree-backups') "$timestamp-$branch"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

git -C $WorktreePath diff | Out-File -FilePath (Join-Path $backupDir 'diff.patch') -Encoding utf8
git -C $WorktreePath log --oneline --graph | Out-File -FilePath (Join-Path $backupDir 'commits.txt') -Encoding utf8

Write-Host "Worktree 清理检查报告：$branch"
Write-Host "- 路径：$WorktreePath"
Write-Host "- 未提交文件：$($uncommitted.Count) 个"
Write-Host "- 未合并提交：$($unmerged.Count) 个"
Write-Host "- 备份位置：$backupDir"

if (-not $Force) {
    $confirm = Read-Host '是否继续清理？请输入 y 确认；其他输入取消'
    if ($confirm -notin @('y', 'Y')) {
        Write-Host '已取消清理，worktree 保留。'
        exit 0
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $backupDir 'diff.patch')) -or
    -not (Test-Path -LiteralPath (Join-Path $backupDir 'commits.txt'))) {
    throw "备份文件不完整，已中止清理：$backupDir"
}

git worktree remove --force $WorktreePath
Write-Host "已删除 worktree：$WorktreePath"
