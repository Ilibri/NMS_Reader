import subprocess
import time
import os

piper_exe = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\piper\piper.exe"
model = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Voices\ru_RU-ruslan-medium.onnx"
cfg = model + ".json"

def test_synth(text):
    temp = os.path.join(os.environ["TEMP"], f"test_{int(time.time()*1000)}.wav")
    t0 = time.time()
    proc = subprocess.Popen([piper_exe, "-m", model, "-c", cfg, "-f", temp], stdin=subprocess.PIPE, stderr=subprocess.PIPE)
    proc.communicate(text.encode("utf-8"))
    dt = time.time() - t0
    size = os.path.getsize(temp) if os.path.exists(temp) else 0
    if os.path.exists(temp): os.remove(temp)
    print(f"Text ({len(text)} chars): '{text}' -> Time: {dt*1000:.1f} ms, File size: {size} bytes")

test_synth("Привет.")
test_synth("Огни на корпусе электронной формы жизни становятся ярче.")
test_synth("Огни на корпусе электронной формы жизни становятся ярче. Существо резко дергает головой вправо, и странное жужжание сигнализирует о невидимом стремительном потоке данных. Что же произойдет дальше?")
