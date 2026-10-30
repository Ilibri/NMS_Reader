import subprocess
import time
import os

piper_exe = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\piper\piper.exe"
model = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Voices\ru_RU-ruslan-medium.onnx"

print("Starting persistent Piper process...")
start_init = time.time()
proc = subprocess.Popen(
    [piper_exe, "-m", model, "--output_raw"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    bufsize=0
)
init_time = time.time() - start_init
print(f"Process started in {init_time*1000:.1f} ms")

# Send sentence 1
t0 = time.time()
proc.stdin.write("Огни на корпусе электронной формы жизни становятся ярче.\n".encode("utf-8"))
proc.stdin.flush()

# Read some raw audio (sample rate is 22050 16-bit mono = 44100 bytes/sec)
# Sentence is ~3.5 sec audio = ~150,000 bytes
raw_audio = proc.stdout.read(44100 * 2) # read 2 seconds of audio
t1 = time.time()
print(f"Sentence 1: Received audio in {(t1-t0)*1000:.1f} ms! Bytes read: {len(raw_audio)}")

# Send sentence 2 (now the model is already 100% warm in RAM)
t2 = time.time()
proc.stdin.write("Существо резко дергает головой вправо.\n".encode("utf-8"))
proc.stdin.flush()
raw_audio2 = proc.stdout.read(44100 * 2)
t3 = time.time()
print(f"Sentence 2 (Warm in RAM): Received audio in {(t3-t2)*1000:.1f} ms! Bytes read: {len(raw_audio2)}")

proc.terminate()
