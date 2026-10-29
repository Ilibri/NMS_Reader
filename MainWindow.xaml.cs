using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NMS_Reader.Models;
using NMS_Reader.Services;

namespace NMS_Reader
{
    public partial class MainWindow : Window
    {
        private readonly AppConfig _config;
        private readonly SpeechService _speechService;
        private readonly OcrService _ocrService;
        private readonly HotkeyService _hotkeyService;
        private readonly TrayIconService _trayIcon;
        private readonly AutoDetectService _autoDetect;
        private bool _isProcessing = false;
        private bool _isInitializing = true;
        private List<VoiceProfile> _profiles = new();
        private VoiceProfile _activeProfile = null!;
        private string _currentPresetKey = "suit_male";

        public MainWindow()
        {
            _config = AppConfig.Load();
            _speechService = new SpeechService();
            _ocrService = new OcrService();
            _hotkeyService = new HotkeyService();
            _trayIcon = new TrayIconService(this);
            _autoDetect = new AutoDetectService(_ocrService, _speechService, _config);

            InitializeComponent();

            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            // 0. Initialize Sound Profiles
            _profiles = VoiceProfileManager.LoadProfiles();
            _activeProfile = _profiles.FirstOrDefault(p => p.Id == _config.ActiveProfileId) ?? _profiles[0];
            ProfileComboBox.ItemsSource = _profiles;
            ProfileComboBox.SelectedItem = _activeProfile;
            UpdateProfileBadge();

            // 1. Bind Voices
            VoiceComboBox.ItemsSource = _speechService.Voices;
            var current = _speechService.Voices.FirstOrDefault(v => v.Id.Equals(_config.VoiceModel, StringComparison.OrdinalIgnoreCase))
                          ?? _speechService.Voices.FirstOrDefault(v => v.DisplayName.Equals(_config.VoiceName, StringComparison.OrdinalIgnoreCase))
                          ?? _speechService.CurrentVoice;

            if (current != null)
            {
                VoiceComboBox.SelectedItem = current;
                _speechService.SetVoice(current);
            }

            // 2. Bind Sliders & Config values
            SpeedSlider.Value = _config.SpeakingRate;
            SpeedLabel.Text = $"{_config.SpeakingRate:F2}x";

            PitchSlider.Value = _config.PitchShift;
            PitchLabel.Text = $"{_config.PitchShift:F2}x";

            BassSlider.Value = _config.BassGainDb;
            BassLabel.Text = $"{_config.BassGainDb:+0;-0;0} dB";

            TrebleSlider.Value = _config.TrebleGainDb;
            TrebleLabel.Text = $"{_config.TrebleGainDb:+0;-0;0} dB";

            NoiseSlider.Value = _config.NoiseScale;
            NoiseLabel.Text = $"{_config.NoiseScale:F2}";

            SilenceSlider.Value = _config.SentenceSilence;
            SilenceLabel.Text = $"{_config.SentenceSilence:F2} с";

            ReverbSlider.Value = _config.ReverbWet;
            ReverbLabel.Text = $"{(int)(_config.ReverbWet * 100)}%";

            ExosuitSlider.Value = _config.ExosuitMix;
            ExosuitLabel.Text = $"{(int)(_config.ExosuitMix * 100)}%";

            CyberCheck.IsChecked = _config.CyberEffect;
            GekCheck.IsChecked = _config.GekThroat;
            WarmDriveCheck.IsChecked = _config.WarmDrive;
            VykeenRoarCheck.IsChecked = _config.VykeenRoar;

            SkipTitleCheck.IsChecked = _config.SkipTitle;
            SkipChoicesCheck.IsChecked = _config.SkipChoices;
            TrayCheck.IsChecked = _config.MinimizeToTray;
            AutoDetectCheck.IsChecked = _config.AutoDetectEnabled;
            OnlyNmsCheck.IsChecked = _config.OnlyInNms;

            _isInitializing = false;

            // 3. Wire Auto-Detect Events
            _autoDetect.IsAutoDetectEnabled = _config.AutoDetectEnabled;
            _autoDetect.OnDialogueDetected += (dialogueText) =>
            {
                Dispatcher.Invoke(() =>
                {
                    LogTimestamp.Text = $"Запись от {DateTime.Now:HH:mm:ss}";
                    LoreTextBox.Text = dialogueText;
                });
            };

            _autoDetect.OnStatusChanged += (status, kind) =>
            {
                Dispatcher.Invoke(() => SetStatus(status, kind));
            };

            // 4. Initialize Tray Icon
            try
            {
                _trayIcon.Initialize();
                _trayIcon.OnRequestScan += () => Dispatcher.Invoke(() =>
                {
                    _autoDetect.Arm(6000);
                    TriggerScan();
                });
                _trayIcon.OnRequestExit += () => Dispatcher.Invoke(ExitApplication);
            }
            catch { }

            // 5. Initialize Global Hotkeys (F3 stop/toggle, F4 manual read)
            try
            {
                _hotkeyService.Initialize(this, (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    _trayIcon.HandleMessage(msg, wParam, lParam);
                    return IntPtr.Zero;
                });

                _hotkeyService.OnDialogHotkeyPressed += () => Dispatcher.Invoke(() =>
                {
                    _autoDetect.Arm(6000);
                    TriggerScan();
                });
                _hotkeyService.OnStopHotkeyPressed += () => Dispatcher.Invoke(ToggleOrStopSpeech);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка хоткеев: {ex.Message}", "error");
            }

            // 6. Start Auto-Detect Loop
            if (_config.AutoDetectEnabled)
            {
                _autoDetect.Start();
                SetStatus("NMS активна: зажмите 'E' для диалога", "ready");
            }
            else
            {
                SetStatus("Авто-режим отключен", "ready");
            }

            // 7. Update initial active preset button & header highlight
            _currentPresetKey = !string.IsNullOrEmpty(_config.ActivePresetKey) ? _config.ActivePresetKey : "suit_female";
            UpdateActivePresetHighlight(_currentPresetKey);

            // 8. Play female exosuit startup announcement
            _ = Task.Run(async () =>
            {
                await Task.Delay(350);
                var suitFemaleEffects = new AudioEffectsConfig
                {
                    PitchShift = 1.00,
                    BassGainDb = 2.0,
                    TrebleGainDb = 2.0,
                    WarmDrive = true,
                    ExosuitRadio = true,
                    ExosuitMix = 0.40,
                    ReverbWet = 0.0,
                    EchoWet = 0.0
                };

                var irinaVoice = _speechService.Voices.FirstOrDefault(v => v.Id.Equals("irina", StringComparison.OrdinalIgnoreCase));
                var savedVoice = _speechService.CurrentVoice;

                if (irinaVoice != null)
                {
                    _speechService.SetVoice(irinaVoice);
                }

                string greeting = "Системы экзокостюма активированы. Готова к исследованию.";
                await _speechService.SpeakAsync(greeting, 1.00, suitFemaleEffects, () =>
                {
                    if (savedVoice != null)
                    {
                        _speechService.SetVoice(savedVoice);
                    }
                }, 0.68, 0.40);
            });
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F3)
            {
                e.Handled = true;
                ToggleOrStopSpeech();
            }
            else if (e.Key == Key.F4)
            {
                e.Handled = true;
                _autoDetect.Arm(6000);
                TriggerScan();
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _config.MinimizeToTray)
            {
                Hide();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _autoDetect.Stop();
            _speechService.Stop();
            _trayIcon.Dispose();
            _hotkeyService.Dispose();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _autoDetect.Dispose();
            _speechService.Stop();
            _hotkeyService.Dispose();
            _trayIcon.Dispose();
        }

        private void ExitApplication()
        {
            Close();
        }

        private void SetStatus(string text, string kind = "ready")
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"● {text.ToUpper()}";
                switch (kind)
                {
                    case "busy":
                        StatusPill.Background = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                        break;
                    case "error":
                        StatusPill.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(252, 165, 165));
                        break;
                    default:
                        StatusPill.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                        break;
                }
            });
        }

        private async void TriggerScan()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            SetStatus("Сканирование диалога...", "busy");

            try
            {
                var t0 = DateTime.Now;
                using var bmp = _ocrService.CaptureScreen(fullscreen: false, _config);
                var rawLines = await _ocrService.RecognizeFromBitmapAsync(bmp, _config.SkipChoices);

                string cleaned = NmsTextCleaner.Clean(rawLines, _config.SkipTitle, _config.SkipChoices);
                var elapsed = (DateTime.Now - t0).TotalSeconds;

                if (string.IsNullOrWhiteSpace(cleaned))
                {
                    SetStatus("Текст не найден", "ready");
                    return;
                }

                SetStatus($"Чтение ({elapsed:F2} с)", "busy");
                LogTimestamp.Text = $"Запись от {DateTime.Now:HH:mm:ss}";
                LoreTextBox.Text = cleaned;

                string spokenText = CharacterStylizer.Stylize(cleaned, _config.CharacterMode);
                await _speechService.SpeakAsync(spokenText, _config.SpeakingRate, _config.GetAudioEffectsConfig(), () =>
                {
                    SetStatus("Готов к чтению", "ready");
                }, _config.NoiseScale, _config.SentenceSilence);
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка: {ex.Message}", "error");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void ToggleOrStopSpeech()
        {
            if (_speechService.IsSpeaking)
            {
                _speechService.Stop();
                _autoDetect.SuppressCurrentDialogue();
                SetStatus("Озвучка остановлена (F3)", "ready");
            }
            else
            {
                _autoDetect.ResetSuppression();
                _autoDetect.Arm(6000);
                TriggerScan();
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            ToggleOrStopSpeech();
        }

        private void BtnManualScan_Click(object sender, RoutedEventArgs e)
        {
            _autoDetect.Arm(6000);
            TriggerScan();
        }

        private async void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            if (_speechService.IsSpeaking)
            {
                _speechService.Stop();
                SetStatus("Воспроизведение остановлено (F3)", "ready");
                return;
            }

            SetStatus("Воспроизведение теста...", "busy");
            string testPhrase;
            if (_config.CharacterMode == "artemis")
            {
                testPhrase = "Ты меня слышишь?.. Мой передатчик едва ловит сигнал. Пожалуйста, не уходи, ответь мне...";
            }
            else if (_config.GekThroat)
            {
                testPhrase = "Друг! Торговец Гек рад тебя видеть! Приготовь наниты, у меня есть редкие технологии!";
            }
            else if (_config.VoiceModel == "ruslan" || _config.ExosuitMix > 0.15)
            {
                testPhrase = _config.VoiceModel == "irina"
                    ? "Системы жизнеобеспечения в норме. Кольца местной луны зафиксированы на сканере."
                    : "Внеземной артефакт обнаружен. Системы экзокостюма в норме.";
            }
            else if (_config.CyberEffect && _config.ReverbWet < 0.25)
            {
                testPhrase = "Схождение Корвакс анализирует информационные потоки. Отклонений не обнаружено.";
            }
            else if (_config.MonolithChorus || _config.ReverbWet > 0.25)
            {
                testPhrase = "Твоя цель здесь... Иди ко мне...";
            }
            else if (_config.VykeenRoar || _config.CharacterMode == "vykeen" || (_config.BassGainDb > 5.0 && _config.TrebleGainDb > 3.0))
            {
                testPhrase = "Грах! Смерть стражам! Покажи мне свою силу, презренный нарушитель!";
            }
            else
            {
                testPhrase = "Древний обелиск вибрирует космической энергией. Символы на плите складываются в предостережение.";
            }

            string spoken = CharacterStylizer.Stylize(testPhrase, _config.CharacterMode);

            await _speechService.SpeakAsync(spoken, _config.SpeakingRate, _config.GetAudioEffectsConfig(), () =>
            {
                SetStatus("Готов к игре", "ready");
            }, _config.NoiseScale, _config.SentenceSilence);
        }

        // --- PRESETS ---

        private void PresetSuitFemale_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("suit_female");
        private void PresetSuitMale_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("suit_male");
        private void PresetGek_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("gek");
        private void PresetKorvax_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("korvax");
        private void PresetVykeen_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("vykeen");
        private void PresetWanderer_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("wanderer");
        private void PresetArtemis_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("artemis");
        private void PresetMonolith_Click(object sender, RoutedEventArgs e) => LoadCharacterFromProfile("monolith");

        private void LoadCharacterFromProfile(string characterKey)
        {
            _currentPresetKey = characterKey;
            _config.ActivePresetKey = characterKey;

            var preset = _activeProfile?.GetPreset(characterKey)
                         ?? VoiceProfileManager.GetFactoryProfile().GetPreset(characterKey);

            if (preset != null)
            {
                SetPreset(preset.CharacterKey, preset.VoiceModel, preset.SpeakingRate, preset.PitchShift,
                          preset.BassGainDb, preset.TrebleGainDb, preset.ReverbWet, preset.ExosuitMix,
                          preset.CyberEffect, preset.GekThroat, preset.WarmDrive, preset.MonolithChorus,
                          preset.NoiseScale, preset.SentenceSilence, preset.VykeenRoar);
                UpdateActivePresetHighlight(characterKey);
            }
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || ProfileComboBox.SelectedItem is not VoiceProfile selected) return;

            _activeProfile = selected;
            _config.ActiveProfileId = _activeProfile.Id;
            _config.Save();

            UpdateProfileBadge();
            LoadCharacterFromProfile(_currentPresetKey);
            SetStatus($"Активный профиль: {_activeProfile.Name}", "ready");
        }

        private void UpdateProfileBadge()
        {
            if (_activeProfile == null || ProfileActiveBadge == null || ProfileActiveText == null) return;

            if (_activeProfile.IsFactory)
            {
                ProfileActiveBadge.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                ProfileActiveText.Text = "🔒 ЗАВОДСКОЙ";
                ProfileActiveText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                if (BtnSaveProfile != null) BtnSaveProfile.IsEnabled = false;
                if (BtnDeleteProfile != null) BtnDeleteProfile.IsEnabled = false;
            }
            else
            {
                ProfileActiveBadge.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                ProfileActiveText.Text = "● АКТИВЕН";
                ProfileActiveText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                if (BtnSaveProfile != null) BtnSaveProfile.IsEnabled = true;
                if (BtnDeleteProfile != null) BtnDeleteProfile.IsEnabled = true;
            }
        }

        private void BtnCreateProfile_Click(object sender, RoutedEventArgs e)
        {
            int num = 1;
            while (_profiles.Any(p => p.Name.Equals($"Пресет {num}", StringComparison.OrdinalIgnoreCase)))
            {
                num++;
            }
            string newName = $"Пресет {num}";

            var source = _activeProfile ?? VoiceProfileManager.GetFactoryProfile();
            var newProfile = source.Clone(newName);

            _profiles.Add(newProfile);
            VoiceProfileManager.SaveProfiles(_profiles);

            _isInitializing = true;
            ProfileComboBox.ItemsSource = null;
            ProfileComboBox.ItemsSource = _profiles;
            ProfileComboBox.SelectedItem = newProfile;
            _activeProfile = newProfile;
            _config.ActiveProfileId = newProfile.Id;
            _config.Save();
            _isInitializing = false;

            UpdateProfileBadge();
            SetStatus($"Создан профиль «{newName}» (активен)", "ready");
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null || _activeProfile.IsFactory)
            {
                MessageBox.Show("Заводской профиль защищен от изменений.\n\nНажмите «➕ Создать», чтобы создать свой профиль и свободно настраивать любые голоса под себя.", "Заводской профиль", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var item = new VoicePresetItem
            {
                CharacterKey = _currentPresetKey,
                DisplayName = _currentPresetKey,
                VoiceModel = _config.VoiceModel,
                SpeakingRate = _config.SpeakingRate,
                PitchShift = _config.PitchShift,
                BassGainDb = _config.BassGainDb,
                TrebleGainDb = _config.TrebleGainDb,
                ReverbWet = _config.ReverbWet,
                ExosuitMix = _config.ExosuitMix,
                CyberEffect = _config.CyberEffect,
                GekThroat = _config.GekThroat,
                WarmDrive = _config.WarmDrive,
                MonolithChorus = _config.MonolithChorus,
                VykeenRoar = _config.VykeenRoar,
                NoiseScale = _config.NoiseScale,
                SentenceSilence = _config.SentenceSilence
            };

            _activeProfile.SetPreset(_currentPresetKey, item);
            VoiceProfileManager.SaveProfiles(_profiles);
            SetStatus($"Настройки сохранены в профиль «{_activeProfile.Name}»", "ready");
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null || _activeProfile.IsFactory) return;

            var res = MessageBox.Show($"Удалить пользовательский профиль «{_activeProfile.Name}»?", "Удаление профиля", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            string delName = _activeProfile.Name;
            _profiles.Remove(_activeProfile);
            VoiceProfileManager.SaveProfiles(_profiles);

            _isInitializing = true;
            _activeProfile = _profiles[0];
            _config.ActiveProfileId = _activeProfile.Id;
            _config.Save();

            ProfileComboBox.ItemsSource = null;
            ProfileComboBox.ItemsSource = _profiles;
            ProfileComboBox.SelectedItem = _activeProfile;
            _isInitializing = false;

            UpdateProfileBadge();
            LoadCharacterFromProfile(_currentPresetKey);
            SetStatus($"Профиль «{delName}» удален", "ready");
        }

        private void UpdateActivePresetHighlight(string presetKey)
        {
            var normalBorder = new SolidColorBrush(Color.FromRgb(51, 65, 85));   // #334155
            var normalBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));       // #1e293b
            var normalFg = new SolidColorBrush(Color.FromRgb(226, 232, 240));    // #e2e8f0
            var normalThickness = new Thickness(1);

            Button[] allButtons = { BtnSuitFemale, BtnSuitMale, BtnGek, BtnKorvax, BtnVykeen, BtnWanderer, BtnArtemis, BtnMonolith };
            foreach (var b in allButtons)
            {
                if (b == null) continue;
                b.BorderBrush = normalBorder;
                b.Background = normalBg;
                b.Foreground = normalFg;
                b.BorderThickness = normalThickness;
            }

            bool isThoughts = presetKey is "suit_female" or "suit_male";

            if (isThoughts)
            {
                // Green highlight for Thoughts / Suit header
                ThoughtsHeader.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // #34d399 Emerald green
                ThoughtsHeader.FontWeight = FontWeights.Bold;
                UniverseHeader.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94a3b8 Slate
                UniverseHeader.FontWeight = FontWeights.SemiBold;

                Button? activeBtn = presetKey switch
                {
                    "suit_female" => BtnSuitFemale,
                    "suit_male" => BtnSuitMale,
                    _ => null
                };

                if (activeBtn != null)
                {
                    activeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // #10b981
                    activeBtn.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));     // #064e3b
                    activeBtn.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // #34d399
                    activeBtn.BorderThickness = new Thickness(1.5);
                }
            }
            else
            {
                // Normal Thoughts header, accent Universe header
                ThoughtsHeader.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // #94a3b8
                ThoughtsHeader.FontWeight = FontWeights.SemiBold;
                UniverseHeader.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));  // #38bdf8 Cyan
                UniverseHeader.FontWeight = FontWeights.Bold;

                Button? activeBtn = presetKey switch
                {
                    "gek" => BtnGek,
                    "korvax" => BtnKorvax,
                    "vykeen" => BtnVykeen,
                    "wanderer" => BtnWanderer,
                    "artemis" => BtnArtemis,
                    "monolith" => BtnMonolith,
                    _ => null
                };

                if (activeBtn != null)
                {
                    activeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // #38bdf8
                    activeBtn.Background = new SolidColorBrush(Color.FromRgb(12, 74, 110));   // #0c4a6e
                    activeBtn.Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)); // #bae6fd
                    activeBtn.BorderThickness = new Thickness(1.5);
                }
            }
        }

        private void SetPreset(string charMode, string? preferredVoiceId, double rate, double pitch, double bass, double treble, double reverb, double exosuitMix, bool cyber, bool gekThroat, bool warmDrive, bool monolithChorus = false, double noise = 0.67, double silence = 0.25, bool vykeenRoar = false)
        {
            _isInitializing = true;
            _config.CharacterMode = charMode;

            // Pick preferred voice if available, otherwise keep current or fallback
            if (!string.IsNullOrEmpty(preferredVoiceId))
            {
                var voice = _speechService.Voices.FirstOrDefault(v => v.Id.Equals(preferredVoiceId, StringComparison.OrdinalIgnoreCase))
                            ?? _speechService.Voices.FirstOrDefault(v => v.Id.Equals("dmitri", StringComparison.OrdinalIgnoreCase));
                if (voice != null)
                {
                    VoiceComboBox.SelectedItem = voice;
                    _speechService.SetVoice(voice);
                    _config.VoiceModel = voice.Id;
                    _config.VoiceName = voice.DisplayName;
                }
            }

            SpeedSlider.Value = rate;
            SpeedLabel.Text = $"{rate:F2}x";
            _config.SpeakingRate = rate;

            PitchSlider.Value = pitch;
            PitchLabel.Text = $"{pitch:F2}x";
            _config.PitchShift = pitch;

            BassSlider.Value = bass;
            BassLabel.Text = $"{bass:+0;-0;0} dB";
            _config.BassGainDb = bass;

            TrebleSlider.Value = treble;
            TrebleLabel.Text = $"{treble:+0;-0;0} dB";
            _config.TrebleGainDb = treble;

            ReverbSlider.Value = reverb;
            ReverbLabel.Text = $"{(int)(reverb * 100)}%";
            _config.ReverbWet = reverb;

            ExosuitSlider.Value = exosuitMix;
            ExosuitLabel.Text = $"{(int)(exosuitMix * 100)}%";
            _config.ExosuitMix = exosuitMix;
            _config.ExosuitRadio = exosuitMix > 0.04;

            _config.EchoWet = 0.0; // Echo completely disabled

            NoiseSlider.Value = noise;
            NoiseLabel.Text = $"{noise:F2}";
            _config.NoiseScale = noise;

            SilenceSlider.Value = silence;
            SilenceLabel.Text = $"{silence:F2} с";
            _config.SentenceSilence = silence;

            CyberCheck.IsChecked = cyber;
            _config.CyberEffect = cyber;

            GekCheck.IsChecked = gekThroat;
            _config.GekThroat = gekThroat;

            WarmDriveCheck.IsChecked = warmDrive;
            _config.WarmDrive = warmDrive;
            _config.MonolithChorus = monolithChorus;

            VykeenRoarCheck.IsChecked = vykeenRoar;
            _config.VykeenRoar = vykeenRoar;

            _isInitializing = false;
            _config.Save();
        }

        // --- VALUE CHANGE HANDLERS ---

        private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _config == null || _speechService == null) return;
            if (VoiceComboBox?.SelectedItem is VoiceItem selected)
            {
                _speechService.SetVoice(selected);
                _config.VoiceModel = selected.Id;
                _config.VoiceName = selected.DisplayName;
                _config.Save();
            }
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || SpeedLabel == null) return;
            double rate = Math.Round(e.NewValue, 2);
            SpeedLabel.Text = $"{rate:F2}x";
            _config.SpeakingRate = rate;
            _config.Save();
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || PitchLabel == null) return;
            double val = Math.Round(e.NewValue, 2);
            PitchLabel.Text = $"{val:F2}x";
            _config.PitchShift = val;
            _config.Save();
        }

        private void BassSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || BassLabel == null) return;
            double val = Math.Round(e.NewValue, 0);
            BassLabel.Text = $"{val:+0;-0;0} dB";
            _config.BassGainDb = val;
            _config.Save();
        }

        private void TrebleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || TrebleLabel == null) return;
            double val = Math.Round(e.NewValue, 0);
            TrebleLabel.Text = $"{val:+0;-0;0} dB";
            _config.TrebleGainDb = val;
            _config.Save();
        }

        private void NoiseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || NoiseLabel == null) return;
            double val = Math.Round(e.NewValue, 2);
            NoiseLabel.Text = $"{val:F2}";
            _config.NoiseScale = val;
            _config.Save();
        }

        private void SilenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || SilenceLabel == null) return;
            double val = Math.Round(e.NewValue, 2);
            SilenceLabel.Text = $"{val:F2} с";
            _config.SentenceSilence = val;
            _config.Save();
        }

        private void ReverbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || ReverbLabel == null) return;
            double val = Math.Round(e.NewValue, 2);
            ReverbLabel.Text = $"{(int)(val * 100)}%";
            _config.ReverbWet = val;
            _config.Save();
        }

        private void ExosuitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _config == null || ExosuitLabel == null) return;
            double val = Math.Round(e.NewValue, 2);
            ExosuitLabel.Text = $"{(int)(val * 100)}%";
            _config.ExosuitMix = val;
            _config.ExosuitRadio = val > 0.04;
            _config.Save();
        }

        private void FxChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _config == null) return;
            _config.CyberEffect = CyberCheck.IsChecked ?? false;
            _config.GekThroat = GekCheck.IsChecked ?? false;
            _config.WarmDrive = WarmDriveCheck.IsChecked ?? false;
            _config.VykeenRoar = VykeenRoarCheck.IsChecked ?? false;
            _config.Save();
        }

        private void AutoDetect_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _config == null || _autoDetect == null || AutoDetectCheck == null) return;
            bool enabled = AutoDetectCheck.IsChecked ?? false;
            _config.AutoDetectEnabled = enabled;
            _autoDetect.IsAutoDetectEnabled = enabled;

            if (enabled)
            {
                _autoDetect.Start();
                SetStatus("NMS активна: зажмите 'E' для диалога", "ready");
            }
            else
            {
                _autoDetect.Stop();
                SetStatus("Авто-режим отключен", "ready");
            }

            _config.Save();
        }

        private void OnlyNmsCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _config == null || OnlyNmsCheck == null) return;
            _config.OnlyInNms = OnlyNmsCheck.IsChecked ?? true;
            _config.Save();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _config == null || SkipTitleCheck == null || SkipChoicesCheck == null) return;
            _config.SkipTitle = SkipTitleCheck.IsChecked ?? true;
            _config.SkipChoices = SkipChoicesCheck.IsChecked ?? true;
            _config.Save();
        }

        private void TrayCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _config == null || TrayCheck == null) return;
            _config.MinimizeToTray = TrayCheck.IsChecked ?? true;
            _config.Save();
        }
    }
}
