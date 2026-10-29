using System;
using System.Collections.Generic;

namespace NMS_Reader.Models
{
    public class VoiceProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Пресет 1";
        public bool IsFactory { get; set; } = false;
        public Dictionary<string, VoicePresetItem> Presets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public VoicePresetItem? GetPreset(string key)
        {
            if (Presets.TryGetValue(key, out var item)) return item;
            return null;
        }

        public void SetPreset(string key, VoicePresetItem item)
        {
            if (IsFactory) return; // Factory presets cannot be overwritten
            Presets[key] = item.Clone();
        }

        public VoiceProfile Clone(string newName)
        {
            var profile = new VoiceProfile
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = newName,
                IsFactory = false,
                Presets = new Dictionary<string, VoicePresetItem>(StringComparer.OrdinalIgnoreCase)
            };

            foreach (var kvp in Presets)
            {
                profile.Presets[kvp.Key] = kvp.Value.Clone();
            }

            return profile;
        }

        public override string ToString()
        {
            return IsFactory ? $"🔒 {Name} (Только чтение)" : $"⭐ {Name}";
        }
    }
}
