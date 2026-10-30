import re

path = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Extracted\Globals\gcuiglobals.global.MXML"
with open(path, "r", encoding="utf-8", errors="ignore") as f:
    lines = f.readlines()

keywords = ["typewriter", "parallax", "dialog", "alien", "interaction", "speech", "subtitle", "textspeed", "letterbox"]

matches = []
for i, line in enumerate(lines):
    lower = line.lower()
    for kw in keywords:
        if kw in lower:
            start = max(0, i - 1)
            end = min(len(lines), i + 3)
            context = "".join(lines[start:end]).strip()
            matches.append((i+1, kw, context))
            break

print(f"Total matches: {len(matches)}")
for line_no, kw, ctx in matches[:30]:
    print(f"--- Line {line_no} [{kw}] ---")
    print(ctx)
