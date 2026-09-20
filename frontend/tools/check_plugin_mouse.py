#!/usr/bin/env python3
"""Real frontend pointer checks: plugin activation, MO2_VERIFY_PLUGIN_DRAG or MO2_VERIFY_PANEL_DRAG."""
from pathlib import Path
import os,json,subprocess,time
from evdev import UInput,AbsInfo,ecodes as e
phase=Path('/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json');phase.unlink(missing_ok=True)
env=dict(os.environ,MO2_VERIFY_CATALOG='1',MO2_VERIFY_PLUGIN_MULTI='1',MO2_SCREENSHOT='/home/deck/mo2/frontend/artifacts/plugin-multi.png')
for name in ['MO2_BRIDGE_DIRECTORY','MO2_FIXTURES','MO2_FRONTEND_LAYOUT']:
 env.pop(name,None)
drag=env.get('MO2_VERIFY_PLUGIN_DRAG') == '1'
if drag:
 if os.environ.get('MO2_VERIFY_PLUGIN_MULTI') != '1': env.pop('MO2_VERIFY_PLUGIN_MULTI',None)
 env['MO2_SCREENSHOT']='/home/deck/mo2/frontend/artifacts/plugin-drag.png'
panel=env.get('MO2_VERIFY_PANEL_DRAG') == '1'
if panel:
 if os.environ.get('MO2_VERIFY_PLUGIN_MULTI') != '1': env.pop('MO2_VERIFY_PLUGIN_MULTI',None)
 env['MO2_SCREENSHOT']='/home/deck/mo2/frontend/artifacts/panel-drag.png'
seen=set()
with open('/tmp/mo2-panel-drag.log' if panel else '/tmp/mo2-plugin-drag.log' if drag else '/tmp/mo2-plugin-multi.log','w') as output, UInput({e.EV_KEY:[e.KEY_LEFTCTRL,e.KEY_LEFTSHIFT,e.KEY_A]},name='MO2 multi-select keyboard') as keyboard, UInput({e.EV_KEY:[e.BTN_LEFT],e.EV_ABS:[(e.ABS_X,AbsInfo(0,0,1279,0,0,0)),(e.ABS_Y,AbsInfo(0,0,799,0,0,0))]},name='MO2 multi-select pointer',input_props=[e.INPUT_PROP_POINTER]) as pointer:
 # Release by default, as run.sh uses. This named the Debug build, and Debug has not
 # built since MockHost took a reference to AvaloniaUI.DiagnosticsSupport: upstream
 # references Avalonia.Diagnostics in Debug alone, the two conflict, and the build
 # stops. So every check this driver exists to run — PANEL_DRAG, PLUGIN_DRAG,
 # PLUGIN_MULTI, the only ones that press a real pointer — could not be started at
 # all, and nothing said so, because nothing ran them.
 configuration=os.environ.get('MO2_BUILD_CONFIGURATION','Release')
 binary=f'frontend/MockHost/bin/{configuration}/net9.0/NexusModsApp.dll'
 if not os.path.exists(binary): raise SystemExit(f'No {configuration} build at {binary}; build it first')
 process=subprocess.Popen(['.tools/dotnet/dotnet',binary],env=env,stdout=output,stderr=subprocess.STDOUT)
 try:
  while process.poll() is None:
   try: data=json.loads(phase.read_text())
   except (FileNotFoundError,json.JSONDecodeError):time.sleep(.1);continue
   if data['Phase'] in seen:time.sleep(.1);continue
   seen.add(data['Phase']);time.sleep(.5)
   for point in data['Points']:
    assert 0<=point['X']<1280 and 0<=point['Y']<800,point
    keyboard.write(e.EV_KEY,e.KEY_LEFTCTRL,int(point['Ctrl']));keyboard.syn()
    pointer.write(e.EV_ABS,e.ABS_X,point['X']);pointer.write(e.EV_ABS,e.ABS_Y,point['Y']);pointer.syn();time.sleep(.2)
    pointer.write(e.EV_KEY,e.BTN_LEFT,1);pointer.syn();time.sleep(.15)
    pointer.write(e.EV_KEY,e.BTN_LEFT,0);pointer.syn();time.sleep(.45)
    keyboard.write(e.EV_KEY,e.KEY_LEFTCTRL,0);keyboard.syn()
   if 'Drag' in data:
    start,end=data['Drag']['Start'],data['Drag']['End']
    for point in [start,end]:assert 0<=point['X']<1280 and 0<=point['Y']<800,point
    pointer.write(e.EV_ABS,e.ABS_X,start['X']);pointer.write(e.EV_ABS,e.ABS_Y,start['Y']);pointer.syn();time.sleep(.2)
    pointer.write(e.EV_KEY,e.BTN_LEFT,1);pointer.syn();time.sleep(.2)
    for step in range(1,31):
     pointer.write(e.EV_ABS,e.ABS_X,round(start['X']+(end['X']-start['X'])*step/30))
     pointer.write(e.EV_ABS,e.ABS_Y,round(start['Y']+(end['Y']-start['Y'])*step/30));pointer.syn();time.sleep(.04)
    time.sleep(.3);pointer.write(e.EV_KEY,e.BTN_LEFT,0);pointer.syn()
   print('Mouse phase completed:',data['Phase'],flush=True)
 finally:
  keyboard.write(e.EV_KEY,e.KEY_LEFTCTRL,0);keyboard.syn()
  pointer.write(e.EV_KEY,e.BTN_LEFT,0);pointer.syn()
 print('Frontend exit:',process.returncode,flush=True)
 raise SystemExit(process.returncode)
