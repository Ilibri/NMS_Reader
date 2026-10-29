using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NMS_Reader.Services
{
    public static class NmsTextCleaner
    {
        private static readonly Regex ChoiceNumRegex = new(@"^(\d+[\.\)]|[•\-\*Зз])\s*", RegexOptions.Compiled);
        
        private static readonly HashSet<string> NoiseButtons = new(StringComparer.OrdinalIgnoreCase)
        {
            "[E]", "E", "[TAB]", "TAB", "ESC", "[ESC]", "[C]", "C", "[X]", "X", "[F]", "F", "[W]", "W", "[S]", "S"
        };

        // System / ESC / Pause Menu keywords to reject completely
        private static readonly HashSet<string> MenuKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "НАСТРОЙКИ", "ОБЩЕЕ", "ЭКРАН И ГРАФИКА", "СЕТЬ", "СПЕЦИАЛЬНЫЕ ВОЗМОЖНОСТИ", 
            "КАМЕРА", "УПРАВЛЕНИЕ", "СЛОЖНОСТЬ", "ГРАФИКА", "ЗВУК", "ПРИВЯЗКА КЛАВИШ",
            "ОТКРЫТИЯ", "ЖУРНАЛ", "СПИСОК ИГРОКОВ", "ПЕРЕЗАГРУЗИТЬ", "СОХРАНЕНИЯ",
            "ВЫХОД В СИСТЕМУ", "ГЛАВНОЕ МЕНЮ", "ЭКСПЕДИЦИЯ", "ВИДЕО"
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
            "СТРАЖ", "АРХИВ", "БОРТОВОЙ", "СИГНАЛ", "ТОРГОВЕЦ", "ВОИН",
            "ПРОДАВЕЦ", "КАРТОГРАФ", "УТИЛИЗАТОР", "ИТЕРАЦИЯ", "ГИЛЬДИЯ",
            "ТЕХНОЛОГИЙ", "КОМПОНЕНТОВ", "ОРУЖЕЙНИК", "НАБЛЮДАТЕЛЬ"
        };

        private static readonly string[] CommonActionVerbs = new[]
        {
            "отойти", "уйти", "покинуть", "вставить", "прикоснуться", "принять", 
            "отклонить", "оставить", "забрать", "изучить", "потребовать", "заплатить", 
            "выйти", "дать", "отказаться", "нажать", "извлечь", "согласиться"
        };

        public static string Clean(IEnumerable<string> rawLines, bool skipTitle = true, bool skipChoices = true)
        {
            var lines = rawLines
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l) && !NoiseButtons.Contains(l))
                .ToList();

            if (lines.Count == 0)
                return string.Empty;

            // 1. Reject ESC / Settings / System menu
            int menuMatches = lines.Count(l => MenuKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (menuMatches >= 2 || lines.Any(l => l.Contains("НАСТРОЙКИ", StringComparison.OrdinalIgnoreCase)))
            {
                return string.Empty;
            }

            // 2. Reject In-Game HUD (temperature, radiation, quest notifications)
            int hudMatches = lines.Count(l => HudKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)));
            if (hudMatches >= 1 || lines.Any(l => l.Contains("°C", StringComparison.OrdinalIgnoreCase) || l.Contains("ТОКС:", StringComparison.OrdinalIgnoreCase)))
            {
                return string.Empty;
            }

            int startIdx = 0;

            // 3. Skip Speaker / NPC / Monolith Title
            if (skipTitle)
            {
                string first = lines[0];
                var words = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bool isTitle = words.Length <= 6 && 
                               !first.EndsWith('.') && 
                               (words.All(w => w.ToUpper() == w) || TitleKeywords.Any(k => first.Contains(k, StringComparison.OrdinalIgnoreCase)));

                if (isTitle)
                {
                    startIdx = 1;
                }
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

                if (skipChoices)
                {
                    if (ChoiceNumRegex.IsMatch(line))
                        continue;

                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length <= 4)
                    {
                        string firstWord = words[0].ToLowerInvariant().TrimEnd('.', ',', '!', '?', ':');
                        if (CommonActionVerbs.Contains(firstWord))
                            continue;
                    }
                }

                filtered.Add(line);
            }

            string combined = string.Join(" ", filtered);
            return Regex.Replace(combined, @"\s+", " ").Trim();
        }
    }
}
