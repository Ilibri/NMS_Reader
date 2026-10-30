p = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Services\AutoDetectService.cs"
with open(p, "r", encoding="utf-8") as f:
    s = f.read()

# 1. Update minimum length threshold
s = s.replace(
    "if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 8)",
    "if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 32 && CountWords(cleaned) >= 4)"
)

# 2. Update finalCleaned check
s = s.replace(
    "if (string.IsNullOrWhiteSpace(finalCleaned) || finalCleaned.Length < 6)",
    "if (string.IsNullOrWhiteSpace(finalCleaned) || finalCleaned.Length < 32 || CountWords(finalCleaned) < 4)"
)

# 3. Add CountWords helper if not present
if "private static int CountWords(" not in s:
    helper = """        private static int CountWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static bool IsSameDialogue("""
    s = s.replace("        private static bool IsSameDialogue(", helper)

with open(p, "w", encoding="utf-8", newline="") as f:
    f.write(s)
print("Updated AutoDetectService.cs successfully.")
