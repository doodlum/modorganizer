"""Cancel the frontend's native bulk-save confirmation on Linux/X11; never accepts deletion.

Requires an already running host and two existing saves. Hash verification includes
companion files. The host owns the save action; this observer sends only Escape.
"""
import argparse,os,subprocess,time,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--instance',type=Path,required=True)
parser.add_argument('--tag',default=str(time.time_ns()))
args=parser.parse_args()
mode=args.tag
if not mode or any(c not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_' for c in mode):
 raise ValueError('Use a simple artifact tag')
instance=args.instance.resolve()
exe=('Z:'+str(instance/'ModOrganizer.exe')).replace('/', '\\')
pids=[]
for item in Path('/proc').iterdir():
 if item.name.isdigit():
  try:
   process_args=(item/'cmdline').read_bytes().decode(errors='replace').split('\0')
   if process_args[0].casefold()==exe.casefold():pids.append(item.name)
  except OSError:pass
if len(pids)!=1:raise RuntimeError('Expected exactly one matching native host')
# Read only the Wine prefix entry, not other host environment variables.
prefix=next((entry.split('=',1)[1] for entry in Path('/proc',pids[0],'environ').read_bytes().decode().split('\0') if entry.startswith('WINEPREFIX=')),None)
if not prefix:raise RuntimeError('Host prefix unavailable')
sys.path.insert(0,str(root/'frontend/tools'))
from data_launch_probe import call
endpoint=instance/'plugins/data/frontend-bridge'
snapshot=call(endpoint,'snapshot')
saved=call(endpoint,'readSaves',profilePath=snapshot['profile']['path'])['directory'].replace('\\','/')
if len(saved)<3 or saved[1:3]!=':/':raise RuntimeError('Unexpected native save path')
save_directory=(Path(prefix)/'dosdevices'/(saved[0].lower()+':')/saved[3:]).resolve()
if not save_directory.is_dir():raise RuntimeError('Mapped save directory unavailable')
def dialogs():
 r=subprocess.run(['xdotool','search','--all','--onlyvisible','--pid',pids[0],'--name','^Confirm$'],capture_output=True,text=True)
 if r.returncode not in (0,1):raise RuntimeError('Could not inspect native confirmation')
 return r.stdout.split()
if dialogs():raise RuntimeError('A native confirmation is already open')
layout=Path('/tmp/mo2-save-cancel-layout-'+mode+'.json')
layout_source=root/'frontend/artifacts/verify-paired-layout.json'
if layout_source.exists():layout.write_bytes(layout_source.read_bytes())
(root/'frontend/artifacts').mkdir(exist_ok=True)
report=root/'frontend/artifacts'/('save-cancel-observer-'+mode+'.txt')
if report.exists():raise RuntimeError('Choose new report')
env=os.environ.copy();env.update(MO2_BRIDGE_DIRECTORY=str(endpoint),MO2_VERIFY_SAVE_DELETE_CANCEL='1',MO2_SAVE_CHECK_DIRECTORY=str(save_directory),MO2_SAVE_CANCEL_REPORT=str(report),MO2_FRONTEND_LAYOUT=str(layout))
log=root/'frontend/artifacts'/('save-delete-cancel-'+mode+'.log')
with log.open('w') as output:
 p=subprocess.Popen([str(root/'.tools/dotnet/dotnet'),str(root/'frontend/MockHost/bin/Release/net9.0/MockHost.dll')],env=env,stdout=output,stderr=subprocess.STDOUT)
 observed=False
 try:
  deadline=time.monotonic()+120
  while p.poll() is None and time.monotonic()<deadline:
   text=log.read_text()
   if not observed and 'READY save delete cancellation:' in text:
    found=dialogs()
    if len(found)>1:raise RuntimeError('Ambiguous confirmation')
    if len(found)==1:
     # No affirmative input. Escape is QMessageBox's No/cancel choice.
     time.sleep(.3)
     report.write_text('cancelled\n')
     subprocess.run(['xdotool','key','--window',found[0],'Escape'],check=True)
     observed=True
   lines=[line for line in text.splitlines() if line.startswith(('PASS save delete cancellation','FAIL save delete cancellation'))]
   if lines:
    print('\n'.join(lines))
    if not observed or not lines[-1].startswith('PASS'):raise RuntimeError('Native cancellation not verified')
    break
   time.sleep(.1)
  else:raise RuntimeError('Native cancellation check timed out; inspect host before retrying')
 finally:
  if p.poll() is None:
   p.terminate()
   try:p.wait(timeout=10)
   except subprocess.TimeoutExpired:p.kill();p.wait()
