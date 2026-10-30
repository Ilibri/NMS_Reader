import os
import subprocess

pcbanks = r"C:\Program Files (x86)\Steam\steamapps\common\No Man's Sky\GAMEDATA\PCBANKS"
hgpaktool = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Tools\hgpaktool.exe"

paks = [f for f in os.listdir(pcbanks) if f.endswith(".pak")]
print(f"Total paks: {len(paks)}")

for pak in paks:
    # Check candidates first: MetadataEtc, misc, Language, etc.
    if any(k in pak.lower() for k in ["meta", "misc", "lang", "ui", "scene", "text"]):
        pak_path = os.path.join(pcbanks, pak)
        res = subprocess.run([hgpaktool, "-f", "*RUSSIAN*", "-L", pak_path], capture_output=True, text=True)
        if "RUSSIAN" in res.stdout or "RUSSIAN" in res.stderr:
            print(f"Found in {pak}: {res.stdout.strip()}")
