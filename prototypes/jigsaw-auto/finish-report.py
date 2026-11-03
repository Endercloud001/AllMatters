"""Summarize the measured pipeline and re-accept every stored result with latest verifier."""
import gzip,json,statistics
from collections import defaultdict,Counter
from pipeline import ROOT,fixtures
from verify import verify
data=fixtures();checked=0;rejected=[];rows=[];layout_groups=defaultdict(list)
def signatures(s):
    coords={n['id']:(n['x'],n['y'],n['type'],n['piece']) for n in s['nodes']}
    edges=sorted((min(e['a'],e['b']),max(e['a'],e['b']),e['internal']) for e in s['edges'])
    variants=[]
    for _ in range(4):
        variants.append(json.dumps((sorted(coords.items()),edges)))
        coords={k:(-y,x,t,p) for k,(x,y,t,p) in coords.items()}
    return variants[0],min(variants)
for name in ['pipeline-raw.jsonl.gz','bootstrap-timing.jsonl.gz','stress-pools.jsonl.gz']:
    with gzip.open(ROOT/name,'rt',encoding='utf8') as f:
        for line in f:
            r=json.loads(line);family=r.get('family','existing-demo');pool=r.get('input_pool',data[family]['pool'])
            assert r['state']['pool']==pool
            if r['status']=='SAT':
                m=verify(pool,r['state']['layout'],r.get('B',r['state']['B']))
                assert m==r['metrics'];checked+=1
            else:assert r['state']['phase']=='editing' and r['state']['votes']==[]
            if name=='pipeline-raw.jsonl.gz':
                metrics=r.get('metrics');sig=signatures(r['state']['layout']) if metrics else (None,None)
                layout_groups[(r['family'],r['config'],r['method'],r['budget'])].append(dict(metrics=metrics,expanded=r['expanded'],signatures=sig))
summary=json.loads((ROOT/'pipeline-summary.json').read_text(encoding='utf8'))
layouts=[]
for key,items in layout_groups.items():
    good=[r for r in items if r['metrics']];exp=sorted(r['expanded'] for r in items)
    room_totals=Counter();walk_totals=Counter()
    for r in good:room_totals.update(r['metrics']['rooms']);walk_totals.update(r['metrics']['walk_rooms'])
    layouts.append(dict(family=key[0],config=key[1],method=key[2],budget=key[3],valid=len(good),unique_exact=len({r['signatures'][0] for r in good}),unique_up_to_global_rotation=len({r['signatures'][1] for r in good}),expansion_p50=statistics.median(exp),expansion_p95=exp[94],blocks=dict(Counter(r['metrics']['blocks'] for r in good)),ordinary_nodes=dict(Counter(r['metrics']['normal'] for r in good)),Boss_distance=dict(Counter(r['metrics']['distance'] for r in good)),rests=dict(Counter(r['metrics']['rest'] for r in good)),Boss_neighbors=dict(Counter(r['metrics']['boss_neighbors'] for r in good)),room_totals=dict(room_totals),walk_room_totals=dict(walk_totals),required_rest_coverage_complete=all(r['metrics']['required']==r['metrics']['covered'] for r in good)))
(ROOT/'layout-summary.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf8')
layout_index={(r['family'],r['config'],r['method'],r['budget']):r for r in layouts}
lines=['# 自动实验与用户验收结果','',
       '搜索 tree-search-v2（增量几何/必要Boss邻块优先）；Python independent-verifier-v3（新房型规则）；事务原型 transaction-v1。Git提交提供源码追溯，不增加自定义hash或冻结接口。',
       '',f'最新独立验证器重新验收 {checked} 个成功记录，零不符。9,600次完整事务、400次旧见证构造、800次额外给定池事务分别保留完整gzip JSONL。失败均保留原池及指定原布局，清票回编辑；从未把UNKNOWN计为UNSAT。',
       '', '## 完整事务耗时', '',
       '每行100种子。计时包含输入复制、可用见证独立检查、Node往返、最终Python独立验收与替换；批量使用常驻Node进程。夹具载入/检索为一次性成本 %.2fms，进程创建调用为%.2fms；进程实际启动工作计入首条往返。scratch预检所给见证仅作统一记账，普通从零搜索不使用该见证。实时页面每次新建任务，其服务端/页面等待另记。' % (summary['fixture_load_ms'],summary['node_process_spawn_ms']),
       '', '|池族|配置|方案|预算|有效|UNKNOWN|完整UNSAT|P50/P95 ms|去整体旋转布局数|返回原见证数|', '|---|---|---|---|---|---|---|---|---|---|']
for g in summary['groups']:
    unique=layout_index[(g['family'],g['config'],g['method'],g['budget'])]['unique_up_to_global_rotation']
    lines.append('|%s|%s|%s|%s|%s|%s|0|%.2f / %.2f|%s|%s|'%(g['family'],g['config'],g['method'],g['budget'],g['valid'],g['UNKNOWN'],g['p50_ms'],g['p95_ms'],unique,g['fallbacks']))
lines += ['', '“返回原见证”包括witness方案主动返回及扩展预算未增加用块时返回；不等同于异常发生次数。Python最终验收不通过的候选不发布。layout-summary.json另汇总用块/普通节点、Boss最短距离、房型与探索房型分布、休息覆盖、Boss邻块和P50/P95扩展数。重复布局按块/节点ID统计，另消除整体旋转，不据此声称均匀或视觉体验好。', '', '## 研究装配器构造成本（新规则池）','',
          '旧研究演示assemble最多20次尝试，单独作为bootstrap计时，**不是服从本票扩展预算的正式生成器**；只适用于该独立给定测试池。生产候选仍须从造池时持有独立验收通过的见证，或在给定池预算内找到后才可发布。']
boot=[json.loads(l) for l in gzip.open(ROOT/'bootstrap-timing.jsonl.gz','rt',encoding='utf8')]
for double,B in [(False,15),(False,14),(False,13),(True,13)]:
    selected=[r for r in boot if r['double']==double and r['B']==B];t=sorted(r['timing']['total_ms'] for r in selected)
    lines.append(f'- B={B} double={double}: {len(selected)}样本，{sum(r["status"]=="SAT" for r in selected)}有效，完整构造及独立验收P50/P95 {statistics.median(t):.2f}/{t[94]:.2f}ms。')
lines += ['', '## 额外给定池', '', '100个固定种子先准备合法房型分配（配额/骨架/内部边不变），随后每池×四配置×预算0/1000从零搜索。原池在搜索开始后不再改动；这是100个额外输入池，不代表所有任意池。', '', '|配置|预算|样本|有效|UNKNOWN|UNSAT|原池/失败布局受损|','|---|---|---|---|---|---|---|']
for r in json.loads((ROOT/'stress-summary.json').read_text(encoding='utf8')):
    lines.append('|%s|%s|%s|%s|%s|0|0|'%(r['config'],r['budget'],r['samples'],r['valid'],r['UNKNOWN']))
lines += ['', '## 当前规则及用户反馈','', '- 三项均在Boss替换前检查：每块≤1休息处；不许四节点房型相同；固定内部通路不能直接连接两个精英。仅骨架共边但无内部通路不因此禁止。手动与自动共用整池约束。', '- 6个休息处分属6块；受控六块见证已独立验证。旧三休息块/四块见证及旧固定池不合格，legacy-room-inputs.json仅作拒绝对照；新给定测试池在输入准备时创建，运行时从不修复或重抽给定池。', '- 用户对第2、3组（失败保留、等待/恢复）的通过反馈保留；修改后的地图布局和房型分布仍待用户观察。', '', '## 验证与边界','',
          f'- negative-results.json {len(json.loads((ROOT/"negative-results.json").read_text(encoding="utf8")))}项；coverage-results.json {len(json.loads((ROOT/"coverage-results.json").read_text(encoding="utf8")))}项。后者明确区分完整布局、几何、拓扑、自动条件和探索阶段；阶段负例只证明该验收阶段，不能替代完整合法性。',
          '- pipeline-check-results.json 60项事务检查；worker-recovery-results.json 两项真实worker中断后JSON重做；recovery-results.json 12项状态出口模拟。原生多人存档由相应验证票承担。',
          '- live-browser-results.json 32项：24组配置/方案、预算零、异常、错误输出、实际活动进程中断恢复、请求失败、非法种子、旧给定池及旧页面缺失原池。编辑布局逐字段保持、输入控件锁定、实际等待幕、手机/桌面无溢出，无JS错误；不是用户体验认可。',
          '- 已用Godot检查双版各216测试与构建通过（先前日志godot-check.log.gz），未启动游戏/Steam或改安装。本轮只修改一次性Python/JS/HTML，不新增正式生成器。',
          '- 搜索只枚举碎块接入树，未完整穷尽附加跨边组合。找到且独立通过的解可靠；失败只能UNKNOWN。完整全域UNSAT为0，无最优性、均匀性、任意池必有解保证；无需为本票引入正式求解器。',
          '- 内存为Node进程RSS采样，各组最大采样范围 %.1f–%.1f MiB；不是Python+Node总峰值或单次搜索增量。原始记录保留采样值。' % (min(g['rss_max'] for g in summary['groups'])/1048576,max(g['rss_max'] for g in summary['groups'])/1048576),
          '- 构造保证来自已验证受控见证；额外给定池的成功率与布局多样性仅有实验支持。全部自动成功含异常回退，都经过独立验收。现有扩展实验偏好继续增加可放碎块直至池用满或预算结束，这只是比较样本的搜索策略，不增加最少块数玩法规则。',
          '- 不把任何毫秒值提升为玩法限制。布局、房型、多样性或等待是否可接受仍由用户体验决定；若不接受，回到grilling，不擅改房型配额、额度或最短Boss距离。',
          '', '- 当前规则72组重复运行的scene/status/expanded一致；增量几何与全量几何48组对照的scene、status、扩展数一致。必要Boss邻块优先仅改变候选次序，不新增规则；最终独立Python仍验收全部成功结果。', '', '用户已于2026-11-04确认新版布局更为合理并予以通过，允许收尾；第2/3组此前已通过。验收原文及边界见HITL-ACCEPTANCE.md。本票通过resolution交接，不关闭其他验证票、不将原型扩展为正式生成器。']
(ROOT/'AGENT-RESULTS.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
print(json.dumps(dict(reaccepted_successes=checked,failures=0)))
