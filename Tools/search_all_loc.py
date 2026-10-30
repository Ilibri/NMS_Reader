import glob
import sys

sys.stdout.reconfigure(encoding='utf-8')
mxml_files = glob.glob(r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Extracted\Lang\language\*.MXML")

target = "удивительный форма жизни"
print(f"Scanning {len(mxml_files)} files for '{target}'...")

for file in mxml_files:
    with open(file, "r", encoding="utf-8", errors="ignore") as f:
        for i, line in enumerate(f):
            if "удивительный" in line:
                print(f"[{file}] line {i}: {line.strip()}")
            elif "форма жизни" in line and "сканирование" in line:
                print(f"[{file}] line {i}: {line.strip()}")
