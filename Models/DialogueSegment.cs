using System;

namespace NMS_Reader.Models
{
    public class DialogueSegment
    {
        public string Race { get; set; } = "suit"; // "suit", "gek", "vykeen", "korvax"
        public string Text { get; set; } = string.Empty;

        public DialogueSegment() { }

        public DialogueSegment(string race, string text)
        {
            Race = race;
            Text = text;
        }
    }
}
