p = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Services\NmsTextCleaner.cs"
with open(p, "r", encoding="utf-8") as f:
    s = f.read()

target = "        // HUD elements (environmental hazard, quest tracker, quick menu prompts)"
replacement = """        // Inventory, Crafting, and Resource keywords to reject completely
        private static readonly string[] InventoryKeywords = new[]
        {
            "СНАРЯЖЕНИЕ", "ТЕХНОЛОГИИ", "ГРУЗ", "МУЛЬТИТУЛ", "КОРАБЛЬ", "ИНВЕНТАРЬ",
            "ЯЧЕЙКА", "СВОБОДНО", "ВМЕСТИМОСТЬ", "СТОИМОСТЬ", "УСТАНОВИТЬ", "ПЕРЕМЕСТИТЬ",
            "РАЗОБРАТЬ", "ЗАРЯДИТЬ", "СОЗДАТЬ ПРОДУКТ", "СОЗДАТЬ ТЕХНОЛОГИЮ", "ТЕКУЩЕЕ ЗАДАНИЕ",
            "ЦЕЛЬ МИССИИ", "НАЖМИТЕ", "УДЕРЖИВАЙТЕ", "ОТКРЫТЬ", "СЕСТЬ В", "ПОКИНУТЬ"
        };

        // HUD elements (environmental hazard, quest tracker, quick menu prompts)"""

s = s.replace(target, replacement)

target2 = "            int hudMatches = list.Count(l => HudKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));"
replacement2 = """            if (list.Any(l => InventoryKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            int hudMatches = list.Count(l => HudKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));"""

s = s.replace(target2, replacement2)

with open(p, "w", encoding="utf-8", newline="") as f:
    f.write(s)
print("Updated NmsTextCleaner.cs successfully.")
