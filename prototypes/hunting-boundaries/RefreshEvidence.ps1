param([string]$Run = 'combined-r12')
# Report only. Does not approve requirements, enforce a gate or mutate game data.
$ErrorActionPreference = 'Stop'
$log = Join-Path $PSScriptRoot "evidence/$Run.log"
$exitPath = Join-Path $PSScriptRoot "evidence/$Run.exit.txt"
$exitStatus = [int](Get-Content -LiteralPath $exitPath -Raw)
$lines = Get-Content -LiteralPath $log
$version = ''
$records = [Collections.Generic.List[object]]::new()
$matrixPath = Join-Path $PSScriptRoot 'matrix.json'
$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
for ($index=0; $index -lt $lines.Count; $index++) {
    $line = $lines[$index]
    if ($line -match '^Testing ([^:]+) : ([^ ]+)') { $version = "$($Matches[1]) $($Matches[2])" }
    if ($line -notmatch '^(EVIDENCE |SPAWN|LIVE_FIVE|LIVE_QUEEN|COMPANION |MODEL |RANDOM|LAST_SPORE |NATIVE_|COMBAT_END|STAGE|QUEEN players|SHOP|REST|TREASURE|TABLE|CLOCK |KNOWLEDGE |MOVES |FATAL |MASK|VISIT|VISUAL |SCENE |COVERAGE |COVERAGE_SUMMARY|RESOURCE_|UI_|HUD_|LAYOUT)') { continue }
    $players = $null
    if ($line -match 'players=(\d+)') { $players = [int]$Matches[1] }
    if ($line -match 'players=1-4') { $players = @(1,2,3,4) }
    $kind = ($line -split ' ')[0]
    if($null -eq $players -and $kind -match '^UI_SHOP|^UI_CHOICE|^HUD_'){$players=1}
    $group = switch -Regex ($kind) {
        '^SPAWN|^LIVE_FIVE|^COMPANION|^VISUAL' { @(1,3,8); break }
        '^LIVE_QUEEN' { @(1,6,8); break }
        '^LAYOUT' { @(1,8); break }
        '^COVERAGE' { @(1,8); break }
        '^RESOURCE_|^SCENE' { @(1); break }
        '^UI_REST' { @(10); break }
        '^UI_SHOP' { @(9); break }
        '^UI_CHOICE' { @(4); break }
        '^UI_TREASURE' { @(11); break }
        '^HUD_' { @(13); break }
        '^MODEL|^RANDOM|^LAST_SPORE' { @(2,8); break }
        '^NATIVE_PLAYER_TURN|^NATIVE_ALTERNATING' { @(5); break }
        '^NATIVE_' { @(2,8); break }
        '^COMBAT_END' { @(2,8,12); break }
        '^STAGE' { @(5); break }
        '^QUEEN' { @(6); break }
        '^SHOP' { @(9); break }
        '^REST' { @(10); break }
        '^TREASURE' { @(11); break }
        '^TABLE' { @(3,5,8); break }
        '^CLOCK' { @(7); break }
        '^KNOWLEDGE' { @(4); break }
        '^MOVES' { @(4,5); break }
        '^FATAL' { @(8); break }
        '^MASK|^VISIT' { @(12); break }
        default { @(8) }
    }
    $rules = @($matrix.groups | Where-Object { $_.id -in $group } | ForEach-Object rule)
    $expected = switch -Regex ($kind) {
        '^LAYOUT' { '保留全部原敌及准确追加物种；运行真实视觉Ready/Bounds、原生定位、UpdateBounds及原生cosmetic缩放。逐项报告缺Marker、越宽及重叠，不把扫描退出0当布局批准。专用场景的通用fallback仅测量，实验Marker不是正式方案。'; break }
        '^COVERAGE' { '保留原敌；第一幕按生态追加普通敌人、第三幕追加神秘术士，HP只作一次原生缩放；第二幕仅全员1弱/1脆/1易伤。专用场景缺Marker应作为缺口保留。'; break }
        '^RESOURCE_' { '实际报告原始骨架版本、原生资源/骨架加载结果；读取成功不得当成可转换或可发布。'; break }
        '^UI_SHOP|^SHOP' { '全部购买及删牌入口（含免费）拒绝；原库存/RNG/钱/补货/次数保持；真实动态折扣和跨店价格继续生效；原生事件服务放行。'; break }
        '^UI_CHOICE' { '原生卡牌节点中英文名称、真实纹理及描述可用，展示对象不入牌堆；完整选择屏及外观另验。'; break }
        '^UI_TREASURE' { '空列表不提前通知；由原生展示Tween发送一次空奖励通知，并完成原生两个等待任务；保留非奖励效果、禁止真实数据写入。重建夹具/外围替代及目标版本范围单列，不等于完整游戏UI。'; break }
        '^HUD_' { '四态提示明确区分最后一步/本房安全/面具生效/按时Boss；示例输入须明示，Godot容器在给定分辨率内且不抢既有焦点；独立DLL实际注册钩子，不据此宣称原生游戏UI通过。'; break }
        '^UI_REST|^REST' { '保留原禁用条件和Heal/Mend效果/额外次数，其余选项禁用且不记历史；public/beta完成时序分别观测。'; break }
        '^MODEL|^RANDOM|^LAST_SPORE' { '真菌兽首咬/两咬必生长/40%条件与孢子2；神秘术士三状态、每存活玩家六选一、随机受伤目标治疗12/随机活敌力量2；火炬头攻击10循环。'; break }
        '^NATIVE_PLAYER_TURN|^NATIVE_ALTERNATING' { '已启动阶段输入下，真实原生起手5张放行，3/6/9额外抽牌受限；原生玩家结束两阶段移除细看并清手。穿插原生敌方开始/执行/结束和切边推进轮数；不升级为自动准备调度或实验体全场。'; break }
        '^NATIVE_|^COMBAT_END' { '真实原生结束检测下最后死亡效果先结算，阻止死亡不触发、人工制品按原生抵挡；默认暂缓接缝在死亡移除后释放；原生清理在胜利/奖励前，最终面具移除、首Boss/事件Boss保留。负例开关/原型遭遇/IO隔离范围须明示。'; break }
        '^SPAWN|^COMPANION|^VISUAL' { '五虫真实召唤并按1至5排序，出生空过后奇攻偶寄生；墨宝普通敌，指定睡眠/随从及Minion语义保持；资源/Marker独立报告。'; break }
        '^QUEEN' { '任意当前意图覆盖一次重燃；新怪不进入当前快照，下个敌方阶段直接攻；声明左槽行动顺序，激怒只增存活火炬头力量2。'; break }
        '^STAGE' { '二次复生完成后清除胆小吮吸并启动完整玩家方回合计数；3/6/9限制额外抽牌，正常起手和直接入手放行，回合末移除。'; break }
        '^TREASURE' { '同步/异步奖励清空；非奖励钩子、Face消耗及Spoils任务/移牌/移标记保留；空投票完成通知。'; break }
        '^CLOCK' { '每玩家初始/显示/重置12，五状态固定顺序入手并保留溢出和凋萎强化；其他玩家计数不变。'; break }
        '^KNOWLEDGE' { '每存活玩家三次真实选择；原生能力经展示适配应用、展示对象不入牌堆；伤害意图及治疗20按目标版本/进阶。'; break }
        '^MOVES' { '无厌沙坑5、抽/弃牌各4；实验体二阶段3+N次9/10，伤害与意图一致。'; break }
        '^FATAL' { '普通追加可触发Feed/HandOfGreed，Minion不触发Fatal奖励；原生死亡遗物仍触发。'; break }
        '^MASK|^VISIT' { '最后一步本房及子战斗安全；全员完成或商店真实离开后发唯一不可交换面具；最终Boss奖励前移除、首Boss/事件Boss保留。'; break }
        default { ($rules -join ' / ') }
    }
    $records.Add([pscustomobject]@{
        rule = $rules
        ruleSource = 'https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226'
        groups = @($group)
        version = $version
        players = $players
        layer = $(if($kind -match '^LAYOUT'){'真实物种资源/原生定位/缩放/Hitbox几何；raw控件，非完整Ready/意图/物理点击/public原版资源'}elseif($kind -match '^UI_TREASURE_SCREEN'){'真实beta原版场景/OpenChest/Spine/Tween；外围宿主替代，不是游戏实机/联网'}elseif($kind -match '^UI_TREASURE_TWEEN'){'真实双版Collection/原生Tween/完成处理器；重建视觉夹具，不是完整屏幕'}elseif($kind -match '^HUD_'){'真实Godot视口/独立DLL注册；示例输入，未启动游戏'}elseif($kind -match '^RESOURCE_|^SCENE|^VISUAL'){'真实资源/原生节点/动画；共享beta资源，不是实机布局'}elseif($kind -match '^UI_'){'真实原生UI节点及注入处理器；不是物理输入/完整屏幕'}elseif($kind -match '^COVERAGE'){'真实原生遭遇生成/添加接口；原敌未全面初始化，不是完整战斗'}else{'Godot真实程序集/命令/局部适配；显式驱动范围见BATCH-RESULTS.md'})
        input = "原生测试宿主固定输入及操作顺序见src/Tests/PrototypeHunting*Tests.cs；记录类型=$kind，版本=$version，人数=$players；观测行包含本分支种子/目标/状态输入。"
        expected = $expected
        actual = $line
        verdict = $(if($kind -eq 'LAYOUT_GAP'){'confirmed-gap: exact resource or native geometry path failed'}elseif($kind -eq 'LAYOUT' -and $line -match 'missingNativeAppendMarker=True'){'confirmed-gap: native dedicated scene has no append marker; temporary measurement marker does not resolve layout'}elseif($line -match 'negativeControl=True'){'negative control: native ending rejects last-death power without hold'}elseif($kind -eq 'UI_CHOICE_SCREEN_NEGATIVE'){'confirmed-gap: beta PCK references UI script absent in public; native pair/completion only, not full public screen'}elseif($kind -match 'NEGATIVE'){'confirmed-gap: actual direct grants bypass cleared list'}elseif($kind -eq 'TREASURE_RETURN' -and $line -match 'hookCounterActual=0'){'confirmed-gap: cancelled receipt interrupts this caller nonreward continuation'}elseif($kind -eq 'TREASURE_RETURN'){'bounded observation: contextual carrier only; production strategy not selected'}else{'scoped runtime observation; not whole-requirement approval'})
        requirementSatisfied = $(if($kind -eq 'LAYOUT_GAP' -or ($kind -eq 'LAYOUT' -and $line -match 'missingNativeAppendMarker=True') -or $line -match 'negativeControl=True' -or $kind -match 'NEGATIVE' -or ($kind -eq 'TREASURE_RETURN' -and $line -match 'hookCounterActual=0')){$false}else{$null})
        command = $(if($Run -match '^full-check'){'./tools/Check.ps1'}elseif($Run -match '^continuation-all'){'./prototypes/hunting-boundaries/Run.ps1 -Branch All -SkipResourceExport'}else{'./tools/Test.ps1 -Branch All -Filter PrototypeHunting -SkipResourceExport'})
        exitStatus = $exitStatus
        log = "evidence/$Run.log"
        rawLine = $index+1
        gap = '见本行日志尾部及BATCH-RESULTS.md对应组；完整UI/网络/保存不由此推定'
    })
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'runtime-cases.json'),($records | ConvertTo-Json -Depth 6)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
foreach ($group in $matrix.groups) {
    $covered = @($records | Where-Object { $_.groups -contains $group.id })
    if (!$covered.Count) { continue }
    $group.actual = "已运行子实验$($covered.Count)条；范围及断言见runtime-cases.json和BATCH-RESULTS.md，不代表整组完成"
    $group.exitStatus = $exitStatus
    $group.logs = @("evidence/$Run.log", 'runtime-cases.json', 'BATCH-RESULTS.md')
    $group.status = 'partial'
    $group.gap = '组内剩余自动分支与用户实机均须逐项补齐；详见BATCH-RESULTS.md'
}
[IO.File]::WriteAllText($matrixPath,($matrix | ConvertTo-Json -Depth 8)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
Write-Output "Recorded $($records.Count) runtime rows from $Run (script exit $exitStatus); no requirement auto-approved."
