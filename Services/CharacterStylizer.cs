using System;
using System.Text.RegularExpressions;

namespace NMS_Reader.Services
{
    public static class CharacterStylizer
    {
        public static string Stylize(string text, string characterMode)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            switch (characterMode?.ToLowerInvariant())
            {
                case "vykeen":
                case "вайкин":
                case "вай'кин":
                case "воин вай'кин":
                    return StylizeVykeen(text);

                case "artemis":
                case "артемида":
                case "странница артемида":
                    return StylizeArtemis(text);

                case "apollo":
                case "аполлон":
                case "странник аполлон":
                    return StylizeApollo(text);

                case "null":
                case "пусто":
                case "-пусто-":
                case "- пусто -":
                case "первый странник":
                    return StylizeNull(text);

                case "nada":
                case "нада":
                case "жрец нада":
                    return StylizeNada(text);

                case "polo":
                case "поло":
                case "специалист поло":
                    return StylizePolo(text);

                default:
                    return text;
            }
        }

        private static string StylizeApollo(string text)
        {
            // Remove holoterminus transmission and static markers: [голограмма мерцает], *помехи терминала*, (треск)
            string result = Regex.Replace(text, @"[\*\[\(](?:голограмма\s+)?(?:мерцает|помехи|треск|сигнал|связь|трансляция)[\*\]\)]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();
            return result;
        }

        private static string StylizeNull(string text)
        {
            // Clean whisper / echo markers: [эхо], [шепот душ], *тишина*
            string result = Regex.Replace(text, @"[\*\[\(](?:эхо|шепот|шепот\s+душ|тишина|пауза|помехи)[\*\]\)]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();
            return result;
        }

        private static string StylizeNada(string text)
        {
            // Clean visor flicker and soft mechanical hum markers: [визор мерцает], *мягкий гул*, (электронный вздох)
            string result = Regex.Replace(text, @"[\*\[\(](?:визор\s+)?(?:мерцает|мягкий\s+гул|гул|вздох|свечение)[\*\]\)]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();
            return result;
        }

        private static string StylizePolo(string text)
        {
            // Clean friendly chirping and joy scent markers: [радостно], *щелкает клювом*, [феромоны радости]
            string result = Regex.Replace(text, @"[\*\[\(](?:радостно|щелкает|чирикает|феромоны|смеется|улыбается)[\*\]\)]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();
            return result;
        }

        private static string StylizeVykeen(string text)
        {
            // Remove in-game descriptive stage tags like [рычит], *глухой рык*, (рык), since the authentic roar audio sample is prepended automatically
            string result = Regex.Replace(text, @"[\*\[\(](?:глухой\s+)?(?:рык|рычит|рычание)[\*\]\)]", "", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();

            return result;
        }

        private static string StylizeArtemis(string text)
        {
            // Clean radio static markers like [помехи], *треск*, (сигнал прерывается)
            string result = Regex.Replace(text, @"[\*\[\(](?:помехи|треск|статический шум|сигнал прерывается)[\*\]\)]", "...", RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s{2,}", " ").Trim();
            return result;
        }
    }
}
