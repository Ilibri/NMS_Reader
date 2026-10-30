import os

path = r"C:\Program Files (x86)\Steam\steamapps\common\No Man's Sky\GAMEDATA\PCBANKS\NMSARC.globals.pak"
with open(path, "rb") as f:
    data = f.read(64)
    print("Magic:", data[:4])
    print("Hex:", data[:32].hex())
