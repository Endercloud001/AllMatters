import copy,json
from pipeline import Pipeline,fixtures,ROOT
from verify import verify
data=fixtures();p=Pipeline();rows=[]
try:
    for family,d in data.items():
        for double in [False,True]:
            witness=d['witnesses'][double]
            original=dict(pool=copy.deepcopy(d['pool']),double=double,B=13,layout=copy.deepcopy(witness),votes=['auto'],phase='generating')
            # Simulate existing manual edits independently of the fallback witness.
            for n in original['layout']['nodes']:n['x'],n['y']=-n['y'],n['x']
            before=copy.deepcopy(original)
            for name,method,budget,w,fault,expected in [
                ('zero no witness','extend',0,None,None,'UNKNOWN'),
                ('exhausted no witness','scratch',1,None,None,'UNKNOWN'),
                ('exception no witness','extend',100,None,'exception','UNKNOWN'),
                ('invalid output no witness','scratch',10000,None,'invalid-output','UNKNOWN'),
                ('zero valid witness','extend',0,witness,None,'SAT'),
                ('exception valid witness','extend',100,witness,'exception','SAT'),
                ('invalid output valid witness','extend',100,witness,'invalid-output','SAT')]:
                r=p.run(original,method,budget,27,w,fault)
                assert r['status']==expected,(family,double,name,r['status'])
                if fault=='invalid-output':assert r['rejection'], 'fault must exercise independent rejection'
                assert original==before and r['state']['pool']==before['pool']
                assert r['state']['votes']==[]
                if expected=='UNKNOWN':assert r['state']['layout']==before['layout'] and r['state']['phase']=='editing'
                else:verify(d['pool'],r['state']['layout'],13)
                rows.append(dict(family=family,double=double,case=name,status=r['status'],passed=True,rejection=r['rejection'],witness_error=r['witness_error']))
            # A manual-legal Boss-only witness must never be an automatic fallback.
            small=copy.deepcopy(witness);small['used']=[0];small['nodes']=[n for n in small['nodes'] if n['piece'] in [-1,0]];ids={n['id'] for n in small['nodes']};small['edges']=[e for e in small['edges'] if e['a'] in ids and e['b'] in ids]
            verify(d['pool'],small,13,False)
            r=p.run(original,'extend',0,27,small)
            assert r['status']=='UNKNOWN' and r['witness_error'] and r['state']['layout']==before['layout']
            rows.append(dict(family=family,double=double,case='reject old manual-only fallback',status=r['status'],passed=True,witness_error=r['witness_error']))
            for fault in ['exception','invalid-output']:
                r=p.run(original,'extend',0,27,small,fault)
                assert r['status']=='UNKNOWN' and r['witness_error'] and r['state']['layout']==before['layout']
                rows.append(dict(family=family,double=double,case='old manual-only witness on '+fault,status=r['status'],passed=True,witness_error=r['witness_error']))
            legacy=json.loads((ROOT/'legacy-room-inputs.json').read_text(encoding='utf8'))['controlled']['witnesses'][int(double)]
            for fault in [None,'exception','invalid-output']:
                r=p.run(original,'extend',0,27,legacy,fault)
                assert r['status']=='UNKNOWN' and r['witness_error'] and r['state']['layout']==before['layout']
                rows.append(dict(family=family,double=double,case='legacy pre-revision witness '+str(fault),status=r['status'],passed=True,witness_error=r['witness_error']))
            # Restart the same approved input; no editing-layout or pool substitution.
            a=p.run(original,'extend',100,27,witness);b=p.run(json.loads(json.dumps(original)),'extend',100,27,json.loads(json.dumps(witness)))
            assert a['state']==b['state'] and a['expanded']==b['expanded']
            rows.append(dict(family=family,double=double,case='restart same approved input',passed=True,status=b['status']))
    legacy=json.loads((ROOT/'legacy-room-inputs.json').read_text(encoding='utf8'))['controlled']
    for double in [False,True]:
        original=dict(pool=copy.deepcopy(legacy['pool']),double=double,B=13,layout=copy.deepcopy(legacy['witnesses'][int(double)]),votes=['auto'],phase='generating')
        for budget in [0,1000]:
            r=p.run(original,'extend',budget,1,legacy['witnesses'][int(double)])
            assert r['status']=='UNKNOWN' and r['state']['pool']==original['pool'] and r['state']['layout']==original['layout'] and r['witness_error']
            rows.append(dict(family='legacy invalid given pool',double=double,case='reject without repairing or resampling',budget=budget,status=r['status'],passed=True,witness_error=r['witness_error']))
    (ROOT/'pipeline-check-results.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
    print(len(rows),'integrated transaction checks passed')
finally:p.close()
