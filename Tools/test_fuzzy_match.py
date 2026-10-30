import sqlite3
import time
import difflib

db_path = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Data\nms_dialogues.db"
conn = sqlite3.connect(db_path)
cur = conn.cursor()

# Query OCR text with minor error (comma at end instead of dot)
ocr_sample = "Огни на корпусе электронной формы жизни становятся ярче. Существо резко дергает головой вправо, и странное жужжание сигнализирует о невидимом стремительном потоке данных,"

t0 = time.time()
# Filter candidates by approximate length (+- 20%) to keep it instant
min_len = int(len(ocr_sample) * 0.8)
max_len = int(len(ocr_sample) * 1.2)

# Also match first 3 words to narrow candidates down to < 5
prefix = ocr_sample[:20]
cur.execute("SELECT text FROM dialogues WHERE length BETWEEN ? AND ? AND text LIKE ?", (min_len, max_len, prefix + "%"))
candidates = [r[0] for r in cur.fetchall()]

if not candidates:
    # Fallback to length-only candidates
    cur.execute("SELECT text FROM dialogues WHERE length BETWEEN ? AND ? LIMIT 100", (min_len, max_len))
    candidates = [r[0] for r in cur.fetchall()]

best_match = None
best_score = 0.0

for cand in candidates:
    score = difflib.SequenceMatcher(None, ocr_sample, cand).ratio()
    if score > best_score:
        best_score = score
        best_match = cand

dt = (time.time() - t0) * 1000

print(f"Candidates checked: {len(candidates)}")
print(f"Search time: {dt:.2f} ms")
print(f"Best score: {best_score*100:.1f}%")
print(f"Original OCR: '{ocr_sample}'")
print(f"Matched text: '{best_match}'")
conn.close()
