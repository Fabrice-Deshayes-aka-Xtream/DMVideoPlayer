using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using FluentIcons.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DMVideoPlayer
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow? _owner;

        public SettingsWindow()
        {
            InitializeComponent();
            SetWindowIcon();
        }

        public SettingsWindow(MainWindow owner) : this()
        {
            _owner = owner;
            SetupSettingsControls();
        }

        private void SetupSettingsControls()
        {
            if (_owner == null)
                return;

            var audioOutputComboBox = this.FindControl<ComboBox>("AudioOutputComboBox");
            if (audioOutputComboBox != null)
            {
                var devices = _owner.GetAudioOutputDevices().ToList();
                audioOutputComboBox.ItemsSource = devices;
                audioOutputComboBox.SelectedItem = _owner.GetSelectedAudioOutputDevice();

                audioOutputComboBox.SelectionChanged += (s, e) =>
                {
                    if (audioOutputComboBox.SelectedItem is AudioOutputDevice device)
                    {
                        _owner.SetSelectedAudioOutputDevice(device);
                    }
                };
            }

            var timecodeCheckBox = this.FindControl<CheckBox>("TimecodeCheckBox");
            if (timecodeCheckBox != null)
            {
                timecodeCheckBox.IsChecked = _owner.GetShowTimecode();

                timecodeCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _owner.SetShowTimecode(timecodeCheckBox.IsChecked == true);
                };
            }

            var bpmCheckBox = this.FindControl<CheckBox>("BpmCheckBox");
            if (bpmCheckBox != null)
            {
                bpmCheckBox.IsChecked = _owner.GetShowBpm();

                bpmCheckBox.IsCheckedChanged += (s, e) =>
                {
                    _owner.SetShowBpm(bpmCheckBox.IsChecked == true);
                };
            }

            var defaultVideoDirectoryTextBox = this.FindControl<TextBox>("DefaultVideoDirectoryTextBox");
            if (defaultVideoDirectoryTextBox != null)
            {
                defaultVideoDirectoryTextBox.Text = _owner.GetDefaultVideoDirectory();
            }

            var seekStepNumericUpDown = this.FindControl<NumericUpDown>("SeekStepNumericUpDown");
            if (seekStepNumericUpDown != null)
            {
                seekStepNumericUpDown.Value = _owner.GetSeekStepSeconds();

                seekStepNumericUpDown.ValueChanged += (s, e) =>
                {
                    if (seekStepNumericUpDown.Value.HasValue)
                    {
                        _owner.SetSeekStepSeconds((int)seekStepNumericUpDown.Value.Value);
                    }
                };
            }

            var browseButton = this.FindControl<Button>("BrowseDefaultVideoDirectoryButton");
            if (browseButton != null)
            {
                browseButton.Click += async (s, e) =>
                {
                    try
                    {
                        var storageProvider = StorageProvider;
                        if (storageProvider == null)
                            return;

                        var startPath = _owner.GetDefaultVideoDirectory();
                        IStorageFolder? startFolder = null;
                        if (!string.IsNullOrWhiteSpace(startPath) && Directory.Exists(startPath))
                        {
                            startFolder = await storageProvider.TryGetFolderFromPathAsync(startPath);
                        }

                        var options = new FolderPickerOpenOptions
                        {
                            Title = "Choisir le répertoire vidéo par défaut",
                            AllowMultiple = false,
                            SuggestedStartLocation = startFolder
                        };

                        var result = await storageProvider.OpenFolderPickerAsync(options);
                        if (result != null && result.Count > 0)
                        {
                            var folderPath = result[0].Path.LocalPath;
                            _owner.SetDefaultVideoDirectory(folderPath);
                            if (defaultVideoDirectoryTextBox != null)
                            {
                                defaultVideoDirectoryTextBox.Text = folderPath;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error choosing default video directory: {ex.Message}");
                    }
                };
            }
        }

        private void SetWindowIcon()
        {
            try
            {
                bool isDarkMode = IsDarkTheme();
                var iconBrush = isDarkMode ? Brushes.White : Brushes.Black;

                var icon = new FluentIcons.Avalonia.Fluent.SymbolIcon
                {
                    Symbol = Symbol.Settings,
                    Foreground = iconBrush,
                    FontSize = 28,
                    Width = 32,
                    Height = 32
                };

                icon.Measure(new Size(32, 32));
                icon.Arrange(new Rect(0, 0, 32, 32));

                var bitmap = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(96, 96));
                bitmap.Render(icon);

                this.Icon = new WindowIcon(bitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error creating settings window icon: " + ex.Message);
            }
        }

        private bool IsDarkTheme()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    var colorValues = Application.Current.PlatformSettings.GetColorValues();
                    return colorValues.ThemeVariant == PlatformThemeVariant.Dark;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error detecting theme: " + ex.Message);
            }

            return true;
        }
    }
}
