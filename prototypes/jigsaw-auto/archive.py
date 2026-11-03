import gzip, json
from pathlib import Path
root=Path(__file__).parent
lines=['# 自动实验阶段结果（尚未解决本票）','', '每行100种子；独立验证零拒绝。两族共9,600记录；同一有效布局可在不同预算重复计入，不是9,600个独立池。UNSAT完整穷尽数全部为0。','', '|池族|配置|方案|预算|有效|UNKNOWN|P50/P95 ms|整体旋转去重布局|回退数|','|---|---|---|---|---|---|---|---|---|']
for prefix,family in [('', '既有演示池'),('controlled-','受控池')]:
    d=json.loads((root/(prefix+'summary.json')).read_text(encoding='utf8'))
    for g in d['groups']:
        timing='未测（占位0）' if not prefix and g['method']=='witness' else f"{g['p50_ms']:.2f}/{g['p95_ms']:.2f}"
        lines.append(f"|{family}|{g['config']}|{g['method']}|{g['budget']}|{g['valid']}|{g['UNKNOWN']}|{timing}|{g['unique_up_to_global_rotation']}|{g['fallbacks']}|")
lines+=['','受控见证消除整体旋转后只有一种布局，这是构造的限制；扩展提高了已测样本多样性，但不证明均匀性或体验可接受。从零有界搜索存在UNKNOWN，不能替代交付时已验证的同池见证。','', 'RSS为整个进程采样，不是单次增量/真正峰值；耗时包含两个实验并发时的CPU竞争。既有池见证预处理未计时，不能以其ms=0支持性能结论。当前从零枚举未穷尽附加跨边，任意池普适性未证明。完整负例清单、实际执行中恢复与用户体验尚未完成，故本票保持开放。','', '原始JSONL及独立检查JSONL以gzip保存。Python标准库gzip解压后可直接运行report.py，无需再次生成。']
(root/'RESULTS.md').write_text('\n'.join(lines)+'\n',encoding='utf8')
for name in ['raw.jsonl','controlled-raw.jsonl','checked.jsonl','controlled-checked.jsonl']:
    with (root/name).open('rb') as src, (root/(name+'.gz')).open('wb') as dest:
        with gzip.GzipFile(fileobj=dest,mode='wb',mtime=0) as gz:
            for block in iter(lambda:src.read(1024*1024),b''):gz.write(block)
print('summaries and four compressed complete logs preserved')
