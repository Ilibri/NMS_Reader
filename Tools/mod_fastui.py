p = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Mods\FastUI\GCUIGLOBALS\gcuiglobals.global.MXML"
with open(p, "r", encoding="utf-8") as f:
    s = f.read()

s = s.replace('name="TextPrintoutMultiplier" value="3.500000"', 'name="TextPrintoutMultiplier" value="1000.000000"')
s = s.replace('name="TextPrintoutMultiplierAlien" value="8.000000"', 'name="TextPrintoutMultiplierAlien" value="1000.000000"')

with open(p, "w", encoding="utf-8", newline="") as f:
    f.write(s)

print("Values updated.")
