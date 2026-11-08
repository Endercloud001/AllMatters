param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Output,
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Path).Path
$outputPath = [IO.Path]::GetFullPath($Output)
$comparison = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
if ([string]::Equals($sourcePath, $outputPath, $comparison)) { throw 'Output must differ from the source evidence record.' }
$record = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
if (!$record.question) { throw 'The evidence record needs a question.' }
$items = @($record.requirements)
if (!$items.Count) { throw 'The evidence record needs requirements.' }
$ids = @{}
foreach ($item in $items) {
    if (!$item.id -or $ids.ContainsKey($item.id)) { throw 'Requirement IDs must be present and unique.' }
    $ids[$item.id] = $true
    if ($item.owner -notin @('agent', 'user')) { throw "Invalid owner for $($item.id)." }
    if ($item.status -notin @('todo', 'blocked', 'passed', 'not_applicable')) { throw "Invalid status for $($item.id)." }
    if ($item.status -eq 'passed' -and !@($item.evidence | Where-Object { $_ }).Count) { throw "Passed requirement $($item.id) needs evidence, including the user's actual reply when owner=user." }
    if ($item.status -in @('blocked', 'not_applicable') -and !$item.reason) { throw "Requirement $($item.id) needs a reason." }
}
$agentOpen = @($items | Where-Object { $_.owner -eq 'agent' -and $_.status -in @('todo', 'blocked') })
$userOpen = @($items | Where-Object { $_.owner -eq 'user' -and $_.status -in @('todo', 'blocked') })
$stage = if ($agentOpen.Count) { 'Agent工作未完成' } elseif ($userOpen.Count) { '等待用户体验或决定' } else { '可发布resolution（仍须核对票据类型与范围）' }
function Cell($Value) { return ([string]$Value).Replace('|', '\|').Replace("`r", '').Replace("`n", ' ') }
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# ' + (Cell $record.question)); $lines.Add('')
$lines.Add('阶段：' + $stage); $lines.Add('')
if ($record.issue) { $lines.Add('工单：' + [string]$record.issue); $lines.Add('') }
$lines.Add('|要求|负责方|状态|证据|下一步/原因|')
$lines.Add('|---|---|---|---|---|')
foreach ($item in $items) {
    $next = if ($item.next) { $item.next } else { $item.reason }
    $cells = @((Cell ($item.id + ': ' + $item.description)), (Cell $item.owner), (Cell $item.status), (Cell (@($item.evidence) -join '; ')), (Cell $next))
    $lines.Add('|' + ($cells -join '|') + '|')
}
$lines.Add(''); $lines.Add('本页由证据记录生成；测试通过不会自动把用户条目标记为通过。')
$text = ($lines -join "`n") + "`n"
if ($Check) {
    if (!(Test-Path -LiteralPath $Output -PathType Leaf)) { throw 'Generated status report is missing.' }
    $existing = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $Output).Path).Replace("`r`n", "`n")
    if ($existing -cne $text) { throw 'Status report differs from its evidence record; regenerate it.' }
}
else { [IO.File]::WriteAllText([IO.Path]::GetFullPath($Output), $text, [Text.UTF8Encoding]::new($false)) }
[pscustomobject]@{ Stage = $stage; AgentRemaining = $agentOpen.Count; UserRemaining = $userOpen.Count; Checked = [bool]$Check; Output = [IO.Path]::GetFullPath($Output) }
