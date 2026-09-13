#!/usr/bin/env python3
"""Exercise isolated FNV plugin multi-selection with Linux evdev pointer input."""
from pathlib import Path
import os,json,subprocess,time
from evdev import UInput,AbsInfo,ecodes as e
phase=Path('/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json');phase.unlink(missing_ok=True)
env=dict(os.environ,MO2_VERIFY_CATALOG='1',MO2_VERIFY_PLUGIN_MULTI='1',MO2_SCREENSHOT='/home/deck/mo2/frontend/artifacts/plugin-multi.png')
for name in ['MO2_BRIDGE_DIRECTORY','MO2_FIXTURES','MO2_FRONTEND_LAYOUT']:
 env.pop(name,None)
seen=set()
with open('/tmp/mo2-plugin-multi.log','w') as output, UInput({e.EV_KEY:[e.KEY_LEFTCTRL,e.KEY_LEFTSHIFT,e.KEY_A]},name='MO2 multi-select keyboard') as keyboard, UInput({e.EV_KEY:[e.BTN_LEFT],e.EV_ABS:[(e.ABS_X,AbsInfo(0,0,1279,0,0,0)),(e.ABS_Y,AbsInfo(0,0,799,0,0,0))]},name='MO2 multi-select pointer',input_props=[e.INPUT_PROP_POINTER]) as pointer:
 process=subprocess.Popen(['.tools/dotnet/dotnet','frontend/MockHost/bin/Debug/net9.0/MockHost.dll'],env=env,stdout=output,stderr=subprocess.STDOUT)
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
   print('Mouse phase completed:',data['Phase'],flush=True)
 finally:
  keyboard.write(e.EV_KEY,e.KEY_LEFTCTRL,0);keyboard.syn()
 print('Frontend exit:',process.returncode,flush=True)
 raise SystemExit(process.returncode)
