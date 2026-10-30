import re

ocr_path = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\Services\OcrService.cs"
with open(ocr_path, "r", encoding="utf-8") as f:
    ocr_content = f.read()

# 1. Add _dialogueDb field and constructor param
old_ctor = """        private readonly OcrEngine? _ocrEngine;

        public OcrService()
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ru"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }"""

new_ctor = """        private readonly OcrEngine? _ocrEngine;
        private readonly DialogueDatabaseService? _dialogueDb;

        public OcrService(DialogueDatabaseService? dialogueDb = null)
        {
            _dialogueDb = dialogueDb;
            _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ru"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }"""

ocr_content = ocr_content.replace(old_ctor.replace("\r\n", "\n"), new_ctor.replace("\r\n", "\n"))
ocr_content = ocr_content.replace(old_ctor, new_ctor)

# 2. Add MatchAndCorrect in RecognizeDialogueSegmentsAsync
old_seg = """                    if (segments.Count > 0 && segments[^1].Race == lineRace)
                    {
                        segments[^1].Text += " " + cleanedLine;
                    }
                    else
                    {
                        segments.Add(new DialogueSegment(lineRace, cleanedLine));
                    }"""

new_seg = """                    string corrected = _dialogueDb?.MatchAndCorrect(cleanedLine) ?? cleanedLine;
                    if (segments.Count > 0 && segments[^1].Race == lineRace)
                    {
                        segments[^1].Text += " " + corrected;
                    }
                    else
                    {
                        segments.Add(new DialogueSegment(lineRace, corrected));
                    }"""

ocr_content = ocr_content.replace(old_seg.replace("\r\n", "\n"), new_seg.replace("\r\n", "\n"))
ocr_content = ocr_content.replace(old_seg, new_seg)

with open(ocr_path, "w", encoding="utf-8", newline="") as f:
    f.write(ocr_content)

print("Updated OcrService.cs successfully.")

# Now update MainWindow.xaml.cs
main_path = r"C:\Users\serlord\Projects Git\Разное\NMS_Reader\MainWindow.xaml.cs"
with open(main_path, "r", encoding="utf-8") as f:
    main_content = f.read()

# Add _dialogueDb field
old_fields = """        private readonly TrayIconService _trayIcon;
        private readonly AutoDetectService _autoDetect;"""

new_fields = """        private readonly TrayIconService _trayIcon;
        private readonly AutoDetectService _autoDetect;
        private readonly DialogueDatabaseService _dialogueDb;"""

main_content = main_content.replace(old_fields.replace("\r\n", "\n"), new_fields.replace("\r\n", "\n"))
main_content = main_content.replace(old_fields, new_fields)

# Initialize _dialogueDb in MainWindow()
old_init = """            _config = AppConfig.Load();
            _speechService = new SpeechService();
            _ocrService = new OcrService();"""

new_init = """            _config = AppConfig.Load();
            _dialogueDb = new DialogueDatabaseService();
            _ocrService = new OcrService(_dialogueDb);
            _speechService = new SpeechService();"""

main_content = main_content.replace(old_init.replace("\r\n", "\n"), new_init.replace("\r\n", "\n"))
main_content = main_content.replace(old_init, new_init)

# In MainWindow_Closed, dispose _dialogueDb
old_closed = """        private void MainWindow_Closed(object? sender, EventArgs e)
        {"""

new_closed = """        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _dialogueDb?.Dispose();"""

main_content = main_content.replace(old_closed.replace("\r\n", "\n"), new_closed.replace("\r\n", "\n"))
main_content = main_content.replace(old_closed, new_closed)

with open(main_path, "w", encoding="utf-8", newline="") as f:
    f.write(main_content)

print("Updated MainWindow.xaml.cs successfully.")
