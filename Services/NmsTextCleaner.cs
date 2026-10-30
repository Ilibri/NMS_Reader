using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NMS_Reader.Services
{
    public static class NmsTextCleaner
    {
        private static readonly Regex ChoiceNumRegex = new(@"^(\[?[\dЗз][\.\)\]]|\[?[1-9бвгд]\]|[•\-\*●]\s+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private static readonly HashSet<string> NoiseButtons = new(StringComparer.OrdinalIgnoreCase)
        {
            "[E]", "E", "[TAB]", "TAB", "ESC", "[ESC]", "[C]", "C", "[X]", "X", "[F]", "F", "[W]", "W", "[S]", "S"
        };

        // Trade Terminal / Shop / Inventory keywords to reject completely
        private static readonly string[] TradeKeywords = new[]
        {
            "КУПИТЬ", "ПРОДАТЬ", "ЗАПРОСИТЬ", "ЗАПРОСИЛ", "СИСТЕМА —", "СИСТЕМА -", "СИСТЕМА –",
            "ТЕХНОЛОГИЯ //", "ТЕХНОЛОГИЯ /", "ЭКОНОМИКА //", "КОНФЛИКТ //", "ТОРГОВЫЙ ТЕРМИНАЛ",
            "ГАЛАКТИЧЕСКИЙ ТЕРМИНАЛ", "НАХОДЯСЬ В КОСМОСЕ", "ПОГОВОРИТЕ С ИТЕРАЦИ"
        };

        // System / ESC / Pause Menu keywords to reject completely
        private static readonly HashSet<string> MenuKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "НАСТРОЙКИ", "ОБЩЕЕ", "ЭКРАН И ГРАФИКА", "СЕТЬ", "СПЕЦИАЛЬНЫЕ ВОЗМОЖНОСТИ", 
            "КАМЕРА", "УПРАВЛЕНИЕ", "СЛОЖНОСТЬ", "ГРАФИКА", "ЗВУК", "ПРИВЯЗКА КЛАВИШ",
            "ОТКРЫТИЯ", "ЖУРНАЛ", "СПИСОК ИГРОКОВ", "ПЕРЕЗАГРУЗИТЬ", "СОХРАНЕНИЯ",
            "ВЫХОД В СИСТЕМУ", "ГЛАВНОЕ МЕНЮ", "ЭКСПЕДИЦИЯ", "ВИДЕО"
        };

        // Inventory, Crafting, and Resource keywords to reject completely
        private static readonly string[] InventoryKeywords = new[]
        {
            "СНАРЯЖЕНИЕ", "ТЕХНОЛОГИИ", "ГРУЗ", "МУЛЬТИТУЛ", "КОРАБЛЬ", "ИНВЕНТАРЬ",
            "ЯЧЕЙКА", "СВОБОДНО", "ВМЕСТИМОСТЬ", "СТОИМОСТЬ", "УСТАНОВИТЬ", "ПЕРЕМЕСТИТЬ",
            "РАЗОБРАТЬ", "ЗАРЯДИТЬ", "СОЗДАТЬ ПРОДУКТ", "СОЗДАТЬ ТЕХНОЛОГИЮ", "ТЕКУЩЕЕ ЗАДАНИЕ",
            "ЦЕЛЬ МИССИИ", "НАЖМИТЕ", "УДЕРЖИВАЙТЕ", "ОТКРЫТЬ", "СЕСТЬ В", "ПОКИНУТЬ"
        };

        // HUD elements (environmental hazard, quest tracker, quick menu prompts)
        private static readonly string[] HudKeywords = new[]
        {
            "°C", "°c", "РАД:", "ТОКС:", "RUS_COM", "RUS-CO", "UNITY (",
            "БЫСТРОЕ МЕНЮ", "ВЫЗОВИТЕ", "ГОЛОГРАФИЧЕСКОЙ АРЕНЕ", "ПИТОМЦЕВ В БИТВЕ", 
            "ТЕКУЩЕЕ ЗАДАНИЕ", "ЦЕЛЬ МИССИИ", "БИТВЫ НА ГОЛОГРАФИЧЕСКОЙ",
            "ЗАЩИТА ОТ ОПАСНОСТЕЙ", "СИСТЕМА ЖИЗНЕОБЕСПЕЧЕНИЯ", "ВЗАИМОДЕЙСТВОВАТЬ", "УДЕРЖИВАЙТЕ"
        };

        // NPC and Object header keywords to strip from speech
        private static readonly string[] TitleKeywords = new[]
        {
            "МОНОЛИТ", "ОБЕЛИСК", "СУЩНОСТЬ", "КОРВАКС", "ГЕК", "ВАЙ'КИН", 
            "ТЕРМИНАЛ", "ЖРЕЦ", "НАДА", "ПОЛО", "КОНСОЛЬ", "МАЯК", "ТЕХНИК", 
            "СТРАЖ", "АРХИВ", "БОРТОВОЙ", "СИГНАЛ", "ТОРГОВЕЦ", "ВОИН", "КУПЕЦ",
            "РАЗВЕДЧИК", "УЧЕНЫЙ", "НАУЧНЫЙ", "ПИЛОТ", "КАПИТАН", "КОМАНДИР", "БРОКЕР",
            "ПРОДАВЕЦ", "КАРТОГРАФ", "УТИЛИЗАТОР", "ИТЕРАЦИЯ", "ГИЛЬДИЯ",
            "ТЕХНОЛОГИЙ", "КОМПОНЕНТОВ", "ОРУЖЕЙНИК", "НАБЛЮДАТЕЛЬ"
        };

        private static readonly string[] CommonActionVerbs = new[]
        {
            "отойти", "уйти", "покинуть", "вставить", "прикоснуться", "принять", 
            "отклонить", "оставить", "забрать", "изучить", "потребовать", "заплатить", 
            "выйти", "дать", "отказаться", "нажать", "извлечь", "согласиться",
            "помочь", "тренировать", "сделать", "подарить", "узнать", "купить", "продать"
        };

        public static bool IsInvalidDialogue(IEnumerable<string> rawLines)
        {
            if (rawLines == null) return true;
            var list = rawLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (list.Count == 0) return true;

            int menuMatches = list.Count(l => MenuKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (menuMatches >= 2 || list.Any(l => l.Contains("НАСТРОЙКИ", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (list.Any(l => TradeKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            if (list.Any(l => InventoryKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            int hudMatches = list.Count(l => HudKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (hudMatches >= 1 || list.Any(l => l.Contains("°C", StringComparison.OrdinalIgnoreCase) || l.Contains("ТОКС:", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        public static bool IsTitleLine(string first)
        {
            if (string.IsNullOrWhiteSpace(first)) return false;
            var words = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length <= 6 && 
                   !first.EndsWith('.') && 
                   (words.All(w => w.ToUpper() == w) || TitleKeywords.Any(k => first.Contains(k, StringComparison.OrdinalIgnoreCase)));
        }

        public static bool IsChoiceLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            if (ChoiceNumRegex.IsMatch(line)) return true;

            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 4)
            {
                string firstWord = words[0].ToLowerInvariant().TrimEnd('.', ',', '!', '?', ':');
                if (CommonActionVerbs.Contains(firstWord))
                    return true;
            }
            return false;
        }

        public static string Clean(IEnumerable<string> rawLines, bool skipTitle = true, bool skipChoices = true)
        {
            var lines = rawLines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !NoiseButtons.Contains(l))
                .ToList();

            if (lines.Count == 0 || IsInvalidDialogue(lines))
                return string.Empty;

            int startIdx = 0;

            // 3. Skip Speaker / NPC / Monolith Title
            if (skipTitle && lines.Count > 1 && IsTitleLine(lines[0]))
            {
                startIdx = 1;
            }

            var contentLines = lines.Skip(startIdx).ToList();
            if (contentLines.Count == 0)
                return string.Empty; // All lines were just an NPC name tag!

            var filtered = new List<string>();

            // 4. Skip Choices / Actions
            foreach (var line in contentLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (skipChoices && IsChoiceLine(line))
                    continue;

                filtered.Add(line);
            }

            string combined = string.Join(" ", filtered);
            combined = Regex.Replace(combined, @"\s+", " ").Trim();

            // 5. Filter out untranslated alien language (procedural Latin gibberish like 'molevynenal soknappapolyonciacolam')
            return FilterAlienGibberish(combined);
        }

        private static readonly HashSet<string> RomanNumerals = new(StringComparer.OrdinalIgnoreCase)
        {
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"
        };

        public static string FilterAlienGibberish(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var resultWords = new List<string>();

            foreach (var w in words)
            {
                // Strip punctuation to inspect core word characters
                string core = Regex.Replace(w, @"[^\p{L}\p{Nd}]", "");
                if (string.IsNullOrEmpty(core))
                    continue;

                // Check if the word is pure Latin letters
                bool isPureLatin = Regex.IsMatch(core, @"^[a-zA-Z]+$");
                if (isPureLatin)
                {
                    // If it is a Roman numeral (e.g. Mark II, Atlas IV), keep it
                    if (RomanNumerals.Contains(core))
                    {
                        resultWords.Add(w);
                    }
                    else
                    {
                        // Untranslated procedural alien word -> skip!
                        continue;
                    }
                }
                else
                {
                    // Contains Cyrillic or digits -> keep!
                    resultWords.Add(w);
                }
            }

            string result = string.Join(" ", resultWords).Trim();

            // Check if any meaningful Cyrillic letters remain
            int cyrillicCount = result.Count(c => (c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё');
            if (cyrillicCount < 3)
            {
                return string.Empty; // Pure alien gibberish -> completely ignored
            }

            return Regex.Replace(result, @"\s+", " ").Trim();
        }
    }
}
