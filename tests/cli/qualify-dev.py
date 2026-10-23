import subprocess,time,urllib.request,urllib.parse,http.cookiejar,re,sqlite3,socket,sys
from pathlib import Path
if len(sys.argv) != 3:
 raise SystemExit('Usage: qualify-dev.py <ew-executable> <generated-Acme.Leave-project>')
cli=str(Path(sys.argv[1]).resolve())
project=Path(sys.argv[2]).resolve()
source=project/'Acme.LeaveModule.cs'
original=source.read_text(encoding='utf-8')
logpath=project.parent/'dev-qualification.log'
log=logpath.open('wb')
# The CLI emits UTF-8; polling may observe an incomplete trailing multibyte character.
def read_log():return logpath.read_text(encoding='utf-8',errors='replace')
with socket.socket() as reservation:
 reservation.bind(('127.0.0.1',0))
 port=reservation.getsockname()[1]
base=f'http://127.0.0.1:{port}'
process=subprocess.Popen([cli,'dev',str(project),'--url',base],stdin=subprocess.PIPE,stdout=log,stderr=log,text=True)
jar=http.cookiejar.CookieJar();opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
def wait(check,message,timeout=45):
 deadline=time.monotonic()+timeout
 while time.monotonic()<deadline:
  if process.poll() is not None:raise RuntimeError(read_log()[-4000:])
  try:
   if check():print(message,flush=True);return
  except (OSError,urllib.error.URLError):pass
  time.sleep(.4)
 raise RuntimeError(message+' timed out\n'+read_log()[-3000:])
def get(path):return opener.open(base+path,timeout=3).read().decode()
def form_fields(html):return {k:v for k,v in re.findall(r'<input[^>]*name="([^"]+)"[^>]*value="([^"]*)"',html)}
def post(path,data):
 request=urllib.request.Request(base+path,urllib.parse.urlencode(data).encode())
 return opener.open(request,timeout=5)
try:
 wait(lambda:'Développement' in get('/__ew/dev'),'Initial runtime ready')
 fields=form_fields(get('/__ew/dev'));fields['identity']='ew-dev-alice';post('/__ew/dev/identity',fields)
 fields=form_fields(get('/start/acme.leave.request/1'));fields['field.reason']='Persisted before watch rebuild'
 response=post('/workflow/start/acme.leave.request/1',fields)
 assert 'started=' in response.url,response.url
 db=project/'.ew/dev/workflow.db'
 wait(lambda:sqlite3.connect(db).execute('select count(*) from EwHumanTasks').fetchone()[0]==1,'Durable task created')
 source.write_text(original+'\nBROKEN CSHARP\n',encoding='utf-8')
 wait(lambda:'Ancienne génération toujours active' in read_log(),'Compilation failure retained old runtime')
 assert 'Développement' in get('/__ew/dev')
 source.write_text(original.replace('.WithName("Acme.Leave")','.WithName("Changed preview")'),encoding='utf-8')
 wait(lambda:'Release already used with another hash' in read_log(),'Same-release mutation rejected')
 assert 'Développement' in get('/__ew/dev')
 source.write_text(original.replace('"1.0.0", 1','"1.0.1", 2').replace('.WithName("Acme.Leave")','.WithName("Changed preview")'),encoding='utf-8')
 wait(lambda:'1.0.1' in get('/__ew/dev'),'Versioned restart ready')
 assert '1.0.0' in get('/__ew/dev')
 c=sqlite3.connect(db)
 assert c.execute('select count(*) from EwHumanTasks').fetchone()[0]==1
 assert c.execute('select DefinitionVersion from EwInstances').fetchone()[0]==1
 print('Old instance and task preserved with both exact artifacts',flush=True)
 fields=form_fields(get('/__ew/dev'));fields['identity']='ew-dev-bob';post('/__ew/dev/identity',fields)
 task_id=c.execute('select Id from EwHumanTasks').fetchone()[0]
 import uuid
 task_path='/tasks/'+str(uuid.UUID(task_id))
 fields=form_fields(get(task_path));post('/workflow'+task_path+'/claim',fields)
 fields=form_fields(get(task_path));fields['action']='approve';post('/workflow'+task_path+'/action',fields)
 wait(lambda:sqlite3.connect(db).execute('select Status from EwInstances').fetchone()[0]==3,'Old version completed using its exact handlers after restart')
finally:
 process.stdin.write("stop\n");process.stdin.flush()
 try:process.wait(timeout=20)
 except subprocess.TimeoutExpired:process.kill();process.wait()
 # Keep qualification artifacts and logs for inspection; no automatic data deletion.
 log.close()
