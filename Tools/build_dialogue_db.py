import glob
import re
import os
import json
import sqlite3

mxml_files = glob.glob(r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Extracted\Lang\language\*.MXML")
print(f"Reading {len(mxml_files)} MXML files...")

unique_lines = set()
pattern = re.compile(r'<Property name="Russian" value="(.*?)" />')

for path in mxml_files:
    fname = os.path.basename(path)
    count = 0
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        for line in f:
            match = pattern.search(line)
            if match:
                val = match.group(1).strip()
                # Unescape XML entities like &#xA; &#x9; &quot; &apos; &amp; &lt; &gt;
                val = val.replace("&#xA;", "\n").replace("&#xD;", "").replace("&#x9;", " ")
                val = val.replace("&quot;", "\"").replace("&apos;", "'").replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
                val = val.strip()
                # Keep meaningful dialogue lines (at least 20 chars, has spaces, not a button or single word)
                if len(val) >= 20 and " " in val:
                    # Split multi-paragraph text
                    for p in val.split("\n"):
                        p_clean = p.strip()
                        if len(p_clean) >= 20 and " " in p_clean:
                            unique_lines.add(p_clean)
                            count += 1
    print(f"{fname}: parsed {count} lines")

print(f"Total unique long dialogue lines: {len(unique_lines)}")

# Save to a clean SQLite database or fast JSON
db_path = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Data\nms_dialogues.db"
os.makedirs(os.path.dirname(db_path), exist_ok=True)

if os.path.exists(db_path):
    os.remove(db_path)

conn = sqlite3.connect(db_path)
cur = conn.cursor()
cur.execute("CREATE TABLE dialogues (id INTEGER PRIMARY KEY AUTOINCREMENT, text TEXT, length INTEGER)")
cur.execute("CREATE INDEX idx_len ON dialogues(length)")

cur.executemany("INSERT INTO dialogues (text, length) VALUES (?, ?)", [(t, len(t)) for t in unique_lines])
conn.commit()
conn.close()

db_size_kb = os.path.getsize(db_path) / 1024
print(f"Saved SQLite DB to {db_path} ({db_size_kb:.1f} KB)")
