"""Summarize the measured pipeline and re-accept every stored result with latest verifier."""
import gzip,json,statistics
from collections import defaultdict
from pipeline import ROOT,fixtures
from verify import verify
data=fixtures();checked=0;rejected=[];rows=[]
for name in ['pipeline-raw.jsonl.gz','bootstrap-timing.jsonl.gz','stress-pools.jsonl.gz']:
    with gzip.open(ROOT/name,'rt',encoding='utf8') as f:
        for line in f:
            r=json.loads(line);family=r.get('family','existing-demo');pool=r.get('input_pool',data[family]['pool'])
            assert r['state']['pool']==pool
            if r['status']=='SAT':
                m=verify(pool,r['state']['layout'],r.get('B',r['state']['B']))
                assert m==r['metrics'];checked+=1
            else:assert r['state']['phase']=='editing' and r['state']['votes']==[]
summary=json.loads((ROOT/'pipeline-summary.json').read_text(encoding='utf8'))
lines=['# Agent自动实验结果：等待用户体验','',
       '搜索 tree-search-v1；Python independent-verifier-v2；事务原型 transaction-v1。Git提交提供源码追溯，不增加自定义hash或冻结接口。',
       '',f'最新独立验证器重新验收 {checked} 个成功记录，零不符。9,600次完整事务、400次旧见证构造、800次额外给定池事务分别保留完整gzip JSONL。失败均保留原池及指定原布局，清票回编辑；从未把UNKNOWN计为UNSAT。',
       '', '## 完整事务耗时', '',
       '每行100种子。计时包含输入复制、可用见证独立检查、Node往返、最终Python独立验收与替换；批量使用常驻Node进程。夹具载入/检索为一次性成本 %.2fms，进程创建调用为%.2fms；进程实际启动工作计入首条往返。scratch预检所给见证仅作统一记账，普通从零搜索不使用该见证。实时页面每次新建任务，其服务端/页面等待另记。' % (summary['fixture_load_ms'],summary['node_process_spawn_ms']),
       '', '|池族|配置|方案|预算|有效|UNKNOWN|完整UNSAT|P50/P95 ms|返回原见证数|', '|---|---|---|---|---|---|---|---|---|']
for g in summary['groups']:
    lines.append('|%s|%s|%s|%s|%s|%s|0|%.2f / %.2f|%s|'%(g['family'],g['config'],g['method'],g['budget'],g['valid'],g['UNKNOWN'],g['p50_ms'],g['p95_ms'],g['fallbacks']))
lines += ['', '“返回原见证”包括witness方案主动返回及扩展预算未增加用块时返回；不等同于异常发生次数。Python最终验收不通过的候选不发布。', '', '## 旧见证构造成本（补测）','',
          '旧研究演示assemble最多20次尝试，单独作为bootstrap计时，**不是服从本票扩展预算的正式生成器**；只适用于该既有演示池。生产候选仍须从造池时持有独立验收通过的见证，或在给定池预算内找到后才可发布。']
boot=[json.loads(l) for l in gzip.open(ROOT/'bootstrap-timing.jsonl.gz','rt',encoding='utf8')]
for double,B in [(False,15),(False,14),(False,13),(True,13)]:
    selected=[r for r in boot if r['double']==double and r['B']==B];t=sorted(r['timing']['total_ms'] for r in selected)
    lines.append(f'- B={B} double={double}: {len(selected)}样本，{sum(r["status"]=="SAT" for r in selected)}有效，完整构造及独立验收P50/P95 {statistics.median(t):.2f}/{t[94]:.2f}ms。')
lines += ['', '## 额外给定池', '', '100个固定种子先准备合法房型分配（配额/骨架/内部边不变），随后每池×四配置×预算0/1000从零搜索。原池在搜索开始后不再改动；这是100个额外输入池，不代表所有任意池。', '', '|配置|预算|样本|有效|UNKNOWN|UNSAT|原池/失败布局受损|','|---|---|---|---|---|---|---|']
for r in json.loads((ROOT/'stress-summary.json').read_text(encoding='utf8')):
    lines.append('|%s|%s|%s|%s|%s|0|0|'%(r['config'],r['budget'],r['samples'],r['valid'],r['UNKNOWN']))
lines += ['', '## 验证与边界','',
          '- negative-results.json 57项；coverage-results.json 57项。后者明确区分完整布局、几何、拓扑、自动条件和探索阶段；阶段负例只证明该验收阶段，不能替代完整合法性。',
          '- pipeline-check-results.json 50项事务检查；worker-recovery-results.json 两项真实worker中断后JSON重做；recovery-results.json 12项状态出口模拟。原生多人存档由相应验证票承担。',
          '- live-browser-results.json 30项：24组配置/方案、预算零、异常、错误输出、实际活动进程中断恢复、请求失败及非法种子。编辑布局逐字段保持、输入控件锁定、实际等待幕、手机/桌面无溢出，无JS错误；不是用户体验认可。',
          '- 已用Godot检查双版各216测试与构建通过（先前日志godot-check.log.gz），未启动游戏/Steam或改安装。本轮只修改一次性Python/JS/HTML，不新增正式生成器。',
          '- 搜索只枚举碎块接入树，未完整穷尽附加跨边组合。找到且独立通过的解可靠；失败只能UNKNOWN。完整全域UNSAT为0，无最优性、均匀性、任意池必有解保证；无需为本票引入正式求解器。',
          '- 内存为Node进程RSS采样，各组最大采样范围 %.1f–%.1f MiB；不是Python+Node总峰值或单次搜索增量。原始记录保留采样值。' % (min(g['rss_max'] for g in summary['groups'])/1048576,max(g['rss_max'] for g in summary['groups'])/1048576),
          '- 构造保证来自已验证受控见证；额外给定池的成功率与布局多样性仅有实验支持。全部自动成功含异常回退，都经过独立验收。现有扩展实验偏好继续增加可放碎块直至池用满或预算结束，这只是比较样本的搜索策略，不增加最少块数玩法规则。',
          '- 不把任何毫秒值提升为玩法限制。布局、房型、多样性或等待是否可接受仍由用户体验决定；若不接受，回到grilling，不擅改房型配额、额度或最短Boss距离。',
          '', '本票继续开放；不发布resolution、不解除集成票依赖、不关闭其他验证票。下一步仅需用户体验与反馈，以及由反馈触发的必要修订。']
(ROOT/'AGENT-RESULTS.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
print(json.dumps(dict(reaccepted_successes=checked,failures=0)))
