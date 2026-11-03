"""Local prototype transaction: original input -> Node -> independent Python -> commit.
No native game saves/network. Each Pipeline owns a persistent Node subprocess.
"""
import copy, gzip, json, subprocess, time
from pathlib import Path
from verify import verify
ROOT=Path(__file__).parent

def fixtures():
    data=json.loads((ROOT/'room-fixtures.json').read_text(encoding='utf8'))
    for d in data.values():
        d['witnesses']=dict(enumerate(d['witnesses']))
        for s in d['witnesses'].values():verify(d['pool'],s,15)
    return data

class Pipeline:
    def __init__(self):
        start=time.perf_counter()
        self.process=subprocess.Popen(['node',str(ROOT/'bridge.cjs')],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf8',bufsize=1)
        self.startup_ms=(time.perf_counter()-start)*1000
    def close(self):
        self.process.terminate();self.process.wait(timeout=10)
    def run(self,original,method,budget,seed,witness=None,fault=None):
        start=time.perf_counter();state=copy.deepcopy(original);pool=state['pool'];B=state['B'];double=state['double']
        witness_error=None
        if witness is not None:
            try:verify(pool,witness,B)
            except (ValueError,KeyError,TypeError) as e:witness_error=str(e);witness=None
        prepared=time.perf_counter()
        q=dict(pool=pool,double=double,B=B,seed=seed,budget=budget,method=method,witness=witness,fault=fault)
        self.process.stdin.write(json.dumps(q)+'\n');self.process.stdin.flush()
        line=self.process.stdout.readline()
        if not line:raise RuntimeError('Node worker exited: '+self.process.stderr.read()[-1000:])
        r=json.loads(line);generated=time.perf_counter();metrics=None;rejection=None
        if r.get('scene') is not None:
            try:metrics=verify(pool,r['scene'],B)
            except (ValueError,KeyError,TypeError) as e:rejection=str(e)
        if metrics is None and witness is not None and method!='scratch':
            # Any exceptional fallback still goes through the independent verifier.
            metrics=verify(pool,witness,B);r['scene']=copy.deepcopy(witness);r['fallback']=True
        if metrics is not None:
            state.update(layout=r['scene'],phase='ancient',votes=[],error=None)
        else:
            state.update(phase='editing',votes=[],error=rejection or r.get('error') or r.get('reason') or 'no independently valid candidate')
        end=time.perf_counter()
        return dict(status='SAT' if metrics else 'UNKNOWN',state=state,metrics=metrics,expanded=r.get('expanded',0),fallback=r.get('fallback',False),rejection=rejection,witness_error=witness_error,
                    timing=dict(prepare_ms=(prepared-start)*1000,node_roundtrip_ms=(generated-prepared)*1000,verify_commit_ms=(end-generated)*1000,total_ms=(end-start)*1000),node_reported_ms=r.get('ms'),rss=r.get('rss'))
