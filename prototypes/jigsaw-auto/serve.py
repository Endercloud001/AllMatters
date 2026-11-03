"""Loopback-only throwaway live prototype. python -X utf8 .../serve.py"""
import copy,json,threading,uuid,time
from http.server import ThreadingHTTPServer,SimpleHTTPRequestHandler
from pipeline import Pipeline,fixtures,ROOT
DATA=fixtures();JOBS={};LOCK=threading.Lock()
def execute(job):
    q=job['request'];data=DATA[q['family']];double=q['config']=='act3-double';B={'act1':15,'act2':14,'act3':13,'act3-double':13}[q['config']]
    original=dict(pool=copy.deepcopy(q['pool']),double=double,B=B,layout=copy.deepcopy(q.get('layout',{})),votes=['auto'],phase='generating')
    p=None;start=time.perf_counter()
    try:
        p=Pipeline()
        with LOCK:
            job['pipeline']=p
            if job['cancelled']:raise RuntimeError('interrupted')
        r=p.run(original,q['method'],int(q['budget']),int(q['seed']),data['witnesses'][double] if q.get('withWitness',True) else None,q.get('fault'))
        r['server_total_ms']=(time.perf_counter()-start)*1000
        if not job['cancelled']:job['result']=r
    except Exception as e:
        job['result']=dict(status='INTERRUPTED' if job['cancelled'] else 'UNKNOWN',state={**original,'phase':'generating' if job['cancelled'] else 'editing','votes':[],'error':str(e)},metrics=None)
    finally:
        if p and p.process.poll() is None:p.close()
        job['done']=True
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*args,**kwargs):super().__init__(*args,directory=str(ROOT),**kwargs)
    def log_message(self,*args):pass
    def send_json(self,x,status=200):
        b=json.dumps(x,ensure_ascii=False).encode();self.send_response(status);self.send_header('Content-Type','application/json; charset=utf-8');self.end_headers();self.wfile.write(b)
    def do_GET(self):
        if self.path.startswith('/job/'):
            j=JOBS.get(self.path.split('/')[-1]);return self.send_json(dict(done=j['done'],active=bool(j.get('pipeline') and j['pipeline'].process.poll() is None and not j['done']),result=j.get('result')) if j else {},200 if j else 404)
        if self.path=='/':self.path='/live.html'
        super().do_GET()
    def do_POST(self):
        q=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        if self.path=='/start':
            if not isinstance(q.get('pool'),list):return self.send_json({'error':'实验输入缺少原池，请刷新页面；原布局保留'},400)
            if q.get('family') not in DATA or q.get('config') not in {'act1','act2','act3','act3-double'} or q.get('method') not in {'witness','extend','scratch'} or q.get('budget') not in [0,100,1000,10000] or type(q.get('seed')) is not int:return self.send_json({'error':'invalid experiment input'},400)
            id=uuid.uuid4().hex;j=dict(request=copy.deepcopy(q),cancelled=False,done=False);JOBS[id]=j
            threading.Thread(target=execute,args=(j,),daemon=True).start();return self.send_json(dict(id=id))
        if self.path=='/interrupt':
            j=JOBS[q['id']]
            with LOCK:
                j['cancelled']=True
                was_running=bool(j.get('pipeline') and j['pipeline'].process.poll() is None)
                if was_running:j['pipeline'].process.terminate()
            return self.send_json(dict(interrupted=True,was_running=was_running))
        self.send_json({},404)
if __name__=='__main__':
    print('PROTOTYPE: http://127.0.0.1:8769 — memory only; Ctrl+C stops',flush=True)
    ThreadingHTTPServer(('127.0.0.1',8769),Handler).serve_forever()
