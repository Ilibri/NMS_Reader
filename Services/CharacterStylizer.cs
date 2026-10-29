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

                default:
                    return text;
            }
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
