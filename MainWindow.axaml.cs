using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text.Json;
using IOPath = System.IO.Path;
using FluentIcons.Avalonia.Fluent;

namespace DMVideoPlayer
{
    public partial class MainWindow : Window
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private Media? _currentMedia;
        private VideoView? _videoView;
        private Border? _videoContainer;
        private TextBlock? _placeholderText;
        private Button? _playPauseButton;
        private SymbolIcon? _playPauseIcon;
        private Button? _stopButton;
        private Button? _loadButton;
        private Slider? _volumeSlider;
        private Slider? _positionSlider;
        private TextBlock? _timeLabel;
        private TextBlock? _durationLabel;
        private Grid? _audioTrackPanel;
        private ComboBox? _audioTrackComboBox;
        private ComboBox? _audioOutputComboBox;
        private DispatcherTimer? _updateTimer;
        private bool _isDraggingPosition = false;
        private bool _isUpdatingAudioTracks = false;
        private bool _isUpdatingAudioOutput = false;
        private string? _currentVideoFilePath;
        private bool _isUserInteractingWithSlider = false;
        private string? _selectedAudioDeviceId = null;
        private int? _selectedAudioTrackId = null;
        private Button? _audioOutputButton;
        private Button? _audioTrackButton;
        private Button? _subtitleButton;
        private TextBlock? _subtitleButtonText;
        private TextBlock? _audioTrackButtonText;
        private Button? _volumeButton;
        private SymbolIcon? _volumeIcon;
        private int _volumeBeforeMute = 100;
        private bool _isMuted = false;
        private TextBlock? _audioOutputButtonText;
        private Border? _controlsOverlay;
        private Border? _fileNameOverlay;
        private Border? _statusBar;
        private TextBlock? _fileNameLabel;
        private TextBlock? _statusLabel;
        private DispatcherTimer? _hideControlsTimer;
        private Button? _balanceLockButton;
        private SymbolIcon? _balanceLockIcon;
        private Slider? _balanceSlider;
        private bool _isBalanceLocked = false;
        private TextBlock? _timecodeLabel;
        private Border? _timecodeOverlay;
        private CheckBox? _timecodeCheckBox;
        private string _lastTimecodeText = string.Empty;
        private Border? _bpmOverlay;
        private TextBlock? _bpmLabelOverlay;
        private Border? _beatLed;

        // Timecode interpolation
        private long _lastVlcTime = 0;
        private System.Diagnostics.Stopwatch _interpolationTimer = new System.Diagnostics.Stopwatch();
        private bool _isInterpolating = false;

        // Tempo track beat flash
        private TempoTrack? _tempoTrack = null;
        private System.Threading.Timer? _beatTimer = null; // Timer indépendant haute précision
        private double _nextScheduledBeat = -1.0; // Le prochain beat à flasher
        private double _lastDisplayedBpm = -1.0; // Pour lisser l'affichage du BPM
        private readonly object _beatTimerLock = new object();

        public MainWindow()
        {
            InitializeComponent();
            SetWindowIcon();
            InitializeVLC();
            SetupControls();
            SetupDragAndDrop();
            SetupKeyboardHandling();

            // Timer haute précision pour les beats (indépendant de la vidéo)
            _beatTimer = new System.Threading.Timer(OnBeatTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        
        private void SetWindowIcon()
        {
            try
            {
                bool isDarkMode = IsDarkTheme();
                uint iconColor = isDarkMode ? 0xFFFFFFFF : 0xFF000000;
                
                var bitmap = new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                
                using (var fb = bitmap.Lock())
                {
                    unsafe
                    {
                        var ptr = (uint*)fb.Address.ToPointer();
                        var width = fb.Size.Width;
                        var height = fb.Size.Height;
                        
                        for (int i = 0; i < width * height; i++)
                        {
                            ptr[i] = 0x00000000;
                        }
                        
                        for (int y = 6; y < 24; y++)
                        {
                            for (int x = 18; x < 21; x++)
                            {
                                if (x >= 0 && x < width && y >= 0 && y < height)
                                    ptr[y * width + x] = iconColor;
                            }
                        }
                        
                        DrawCircle(ptr, width, height, 14, 24, 4, iconColor);
                        
                        for (int y = 6; y < 14; y++)
                        {
                            int x = 21 + (y - 6) / 2;
                            if (x >= 0 && x < width && y >= 0 && y < height)
                            {
                                ptr[y * width + x] = iconColor;
                                if (x + 1 < width)
                                    ptr[y * width + x + 1] = iconColor;
                            }
                        }
                    }
                }
                
                this.Icon = new WindowIcon(bitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error creating window icon: " + ex.Message);
            }
        }
        
        private bool IsDarkTheme()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    var colorValues = Application.Current.PlatformSettings.GetColorValues();
                    var themeVariant = colorValues.ThemeVariant;
                    
                    return themeVariant == PlatformThemeVariant.Dark;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error detecting theme: " + ex.Message);
            }
            
            return true;
        }
        
        private unsafe void DrawCircle(uint* ptr, int width, int height, int cx, int cy, int radius, uint color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int px = cx + x;
                        int py = cy + y;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            ptr[py * width + px] = color;
                        }
                    }
                }
            }
        }

        private void InitializeVLC()
        {
            Core.Initialize();
            
            // Add audio output device option if one has been selected
            var options = new List<string>();
            if (!string.IsNullOrEmpty(_selectedAudioDeviceId))
            {
                options.Add($"--mmdevice-audio-device={_selectedAudioDeviceId}");
                Debug.WriteLine($"Initializing VLC with audio device: {_selectedAudioDeviceId}");
            }
            
            _libVLC = options.Count > 0 ? new LibVLC(options.ToArray()) : new LibVLC();

            _videoView = this.FindControl<VideoView>("VideoViewControl");
            if (_videoView != null)
            {
                _videoView.Initialize(_libVLC);
                _mediaPlayer = _videoView.MediaPlayer;

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = 100;

                    _mediaPlayer.Playing += OnMediaPlayerPlaying;
                    _mediaPlayer.Paused += OnMediaPlayerPaused;
                    _mediaPlayer.Stopped += OnMediaPlayerStopped;
                    _mediaPlayer.EndReached += OnMediaPlayerEndReached;
                }
            }

            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS for smooth timecode
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        private void SetupControls()
        {
            _videoContainer = this.FindControl<Border>("VideoContainer");
            _placeholderText = this.FindControl<TextBlock>("PlaceholderText");
            _playPauseButton = this.FindControl<Button>("PlayPauseButton");
            _playPauseIcon = this.FindControl<SymbolIcon>("PlayPauseIcon");
            _stopButton = this.FindControl<Button>("StopButton");
            _loadButton = this.FindControl<Button>("LoadButton");
            _volumeSlider = this.FindControl<Slider>("VolumeSlider");
            _positionSlider = this.FindControl<Slider>("PositionSlider");
            _timeLabel = this.FindControl<TextBlock>("TimeLabel");
            _durationLabel = this.FindControl<TextBlock>("DurationLabel");
            _audioOutputButton = this.FindControl<Button>("AudioOutputButton");
            _audioTrackButton = this.FindControl<Button>("AudioTrackButton");
            _subtitleButton = this.FindControl<Button>("SubtitleButton");
            _subtitleButtonText = this.FindControl<TextBlock>("SubtitleButtonText");
            _audioTrackButtonText = this.FindControl<TextBlock>("AudioTrackButtonText");
            _audioOutputButtonText = this.FindControl<TextBlock>("AudioOutputButtonText");
            _volumeButton = this.FindControl<Button>("VolumeButton");
            _volumeIcon = this.FindControl<SymbolIcon>("VolumeIcon");
            _controlsOverlay = this.FindControl<Border>("ControlsOverlay");
            _fileNameOverlay = this.FindControl<Border>("FileNameOverlay");
            _statusBar = this.FindControl<Border>("StatusBar");
            _fileNameLabel = this.FindControl<TextBlock>("FileNameLabel");
            _statusLabel = this.FindControl<TextBlock>("StatusLabel");
            _balanceLockButton = this.FindControl<Button>("BalanceLockButton");
            _balanceLockIcon = this.FindControl<SymbolIcon>("BalanceLockIcon");
            _balanceSlider = this.FindControl<Slider>("BalanceSlider");
            // Ajout pour la case à cocher Timecode
            _timecodeCheckBox = this.FindControl<CheckBox>("TimecodeCheckBox");
            // Ajout pour le timecode overlay
            _timecodeLabel = this.FindControl<TextBlock>("TimecodeLabel");
            _timecodeOverlay = this.FindControl<Border>("TimecodeOverlay");
            // Ajout pour BPM overlay et beat LED
            _bpmOverlay = this.FindControl<Border>("BpmOverlay");
            _bpmLabelOverlay = this.FindControl<TextBlock>("BpmLabelOverlay");
            _beatLed = this.FindControl<Border>("BeatLed");

            if (_timecodeCheckBox != null)
            {
                _timecodeCheckBox.IsCheckedChanged += (s, e) => { UpdateTimecodeVisibility(); SaveSettings(); };
            }

            var balanceSlider = this.FindControl<Slider>("BalanceSlider");

            // Create invisible ComboBoxes for data storage (not in XAML)
            _audioTrackComboBox = new ComboBox { IsVisible = false };
            _audioOutputComboBox = new ComboBox { IsVisible = false };
            _audioTrackPanel = new Grid { IsVisible = false };

            // Setup hide controls timer
            _hideControlsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideControlsTimer.Tick += (s, e) =>
            {
                HideOverlays();
                _hideControlsTimer.Stop();
            };

            // Setup mouse move handler for showing controls
            if (_videoContainer != null)
            {
                _videoContainer.PointerMoved += VideoContainer_PointerMoved;
                _videoContainer.PointerExited += VideoContainer_PointerExited;
            }

            if (_playPauseButton != null)
                _playPauseButton.Click += PlayPauseButton_Click;

            if (_stopButton != null)
                _stopButton.Click += StopButton_Click;

            if (_loadButton != null)
                _loadButton.Click += LoadButton_Click;

            if (_volumeSlider != null)
                _volumeSlider.PropertyChanged += VolumeSlider_PropertyChanged;

            if (_positionSlider != null)
            {
                _positionSlider.AddHandler(PointerPressedEvent, PositionSlider_PointerPressed, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerReleasedEvent, PositionSlider_PointerReleased, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerMovedEvent, PositionSlider_PointerMoved, RoutingStrategies.Tunnel);
                _positionSlider.AddHandler(PointerCaptureLostEvent, PositionSlider_PointerCaptureLost, RoutingStrategies.Tunnel);
            }

            if (_audioTrackComboBox != null)
            {
                _audioTrackComboBox.SelectionChanged += AudioTrackComboBox_SelectionChanged;
            }

            if (_audioOutputComboBox != null)
            {
                _audioOutputComboBox.SelectionChanged += AudioOutputComboBox_SelectionChanged;
                LoadAudioOutputDevices();
            }

            if (_audioOutputButton != null)
            {
                _audioOutputButton.Click += AudioOutputButton_Click;
            }

            if (_audioTrackButton != null)
            {
                _audioTrackButton.Click += AudioTrackButton_Click;
            }

            if (_subtitleButton != null)
            {
                _subtitleButton.Click += SubtitleButton_Click;
            }

            if (_volumeButton != null)
            {
                _volumeButton.Click += VolumeButton_Click;
            }

            if (_balanceLockButton != null)
            {
                _balanceLockButton.Click += BalanceLockButton_Click;
            }

            if (balanceSlider != null)
            {
                balanceSlider.PropertyChanged += BalanceSlider_PropertyChanged;
                balanceSlider.AddHandler(DoubleTappedEvent, BalanceSlider_DoubleTapped, RoutingStrategies.Tunnel);
            }

            // Load and apply saved settings
            var settings = LoadSettings();
            _selectedAudioDeviceId = settings.AudioOutputDeviceId;
            ApplySettings(settings);
        }

        private void ApplySettings(AppSettings settings)
        {
            // Apply volume
            if (_volumeSlider != null)
            {
                _volumeSlider.Value = settings.Volume;
                Debug.WriteLine($"✓ Applied saved volume: {settings.Volume}");
            }

            // Apply balance
            var balanceSlider = this.FindControl<Slider>("BalanceSlider");
            if (balanceSlider != null)
            {
                balanceSlider.Value = settings.Balance;
                // Apply balance to Windows audio
                float balance = (float)(settings.Balance / 100.0);
                if (WindowsAudio.SetAudioBalance(balance))
                {
                    Debug.WriteLine($"✓ Applied saved balance: {settings.Balance} ({balance:F2})");
                }
            }

            // Apply balance lock state
            _isBalanceLocked = settings.IsBalanceLocked;
            if (_balanceSlider != null)
            {
                _balanceSlider.IsEnabled = !_isBalanceLocked;
            }
            if (_balanceLockIcon != null)
            {
                _balanceLockIcon.Symbol = _isBalanceLocked 
                    ? FluentIcons.Common.Symbol.LockClosed 
                    : FluentIcons.Common.Symbol.LockOpen;
            }
            Debug.WriteLine($"✓ Applied saved balance lock state: {(_isBalanceLocked ? "Locked" : "Unlocked")}");

            // Appliquer l'état de la case à cocher timecode
            if (_timecodeCheckBox != null)
            {
                _timecodeCheckBox.IsChecked = settings.ShowTimecode;
            }
        }

        private void LoadAudioOutputDevices()
        {
            if (_libVLC == null || _audioOutputComboBox == null)
                return;

            try
            {
                _isUpdatingAudioOutput = true;

                var audioOutputDevices = new List<AudioOutputDevice>();
                
                // Get Windows default audio device ID
                string? windowsDefaultDeviceId = WindowsAudio.GetWindowsDefaultAudioDeviceId();
                string? windowsDefaultDeviceName = WindowsAudio.GetWindowsDefaultAudioDeviceName();
                
                Debug.WriteLine($"Windows default device - ID: {windowsDefaultDeviceId}, Name: {windowsDefaultDeviceName}");
                
                // Use LibVLC to enumerate audio output modules and devices
                using (var tempPlayer = new MediaPlayer(_libVLC))
                {
                    var devices = tempPlayer.AudioOutputDeviceEnum;

                    if (devices != null)
                    {
                        foreach (var device in devices)
                        {
                            if (!string.IsNullOrWhiteSpace(device.DeviceIdentifier))
                            {
                                var displayName = !string.IsNullOrWhiteSpace(device.Description) 
                                    ? device.Description 
                                    : device.DeviceIdentifier;

                                audioOutputDevices.Add(new AudioOutputDevice
                                {
                                    DeviceId = device.DeviceIdentifier,
                                    DeviceName = displayName
                                });

                                Debug.WriteLine($"Audio device found: {displayName} (ID: {device.DeviceIdentifier})");
                            }
                        }
                    }
                }

                if (audioOutputDevices.Count > 0)
                {
                    _audioOutputComboBox.ItemsSource = audioOutputDevices;
                    
                    AudioOutputDevice? defaultDevice = null;
                    
                    // Try to restore saved device first
                    if (!string.IsNullOrEmpty(_selectedAudioDeviceId))
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault(d => 
                            d.DeviceId.Equals(_selectedAudioDeviceId, StringComparison.OrdinalIgnoreCase));
                        
                        if (defaultDevice != null)
                        {
                            Debug.WriteLine($"✓ Restored saved audio device: {defaultDevice.DeviceName}");
                        }
                        else
                        {
                            Debug.WriteLine($"⚠ Saved device not found (ID: {_selectedAudioDeviceId}), using system default");
                        }
                    }
                    
                    // If saved device not found, try to match by Windows default device name
                    if (defaultDevice == null && !string.IsNullOrEmpty(windowsDefaultDeviceName))
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault(d => 
                            d.DeviceName.Contains(windowsDefaultDeviceName, StringComparison.OrdinalIgnoreCase));
                        
                        if (defaultDevice != null)
                        {
                            Debug.WriteLine($"Found default device by name match: {defaultDevice.DeviceName}");
                        }
                    }
                    
                    // If not found by name, try to match by partial device ID
                    if (defaultDevice == null && !string.IsNullOrEmpty(windowsDefaultDeviceId))
                    {
                        // Extract the GUID part from Windows device ID (format: {0.0.0.00000000}.{GUID})
                        var guidMatch = System.Text.RegularExpressions.Regex.Match(windowsDefaultDeviceId, @"\{([a-fA-F0-9\-]+)\}$");
                        if (guidMatch.Success)
                        {
                            string guid = guidMatch.Groups[1].Value;
                            defaultDevice = audioOutputDevices.FirstOrDefault(d => 
                                d.DeviceId.Contains(guid, StringComparison.OrdinalIgnoreCase));
                            
                            if (defaultDevice != null)
                            {
                                Debug.WriteLine($"Found default device by GUID match: {defaultDevice.DeviceName}");
                            }
                        }
                    }
                    
                    // Fallback to first device if no match found
                    if (defaultDevice == null)
                    {
                        defaultDevice = audioOutputDevices.FirstOrDefault();
                        Debug.WriteLine($"Using fallback (first device): {defaultDevice?.DeviceName ?? "none"}");
                    }
                    
                    if (defaultDevice != null)
                    {
                        _audioOutputComboBox.SelectedItem = defaultDevice;
                        UpdateAudioOutputButtonText(defaultDevice.DeviceName);
                        Debug.WriteLine($"Selected audio output device: {defaultDevice.DeviceName}");
                    }

                    Debug.WriteLine($"Loaded {audioOutputDevices.Count} audio output devices");
                }
                else
                {
                    Debug.WriteLine("No audio output devices found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading audio output devices: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _isUpdatingAudioOutput = false;
            }
        }

        private void AudioOutputComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAudioOutput || _mediaPlayer == null || _audioOutputComboBox == null)
                return;

            if (_audioOutputComboBox.SelectedItem is AudioOutputDevice selectedDevice)
            {
                try
                {
                    Debug.WriteLine($"Attempting to change audio output to: {selectedDevice.DeviceName} (ID: {selectedDevice.DeviceId})");
                    
                    // Store the current playback state
                    bool wasPlaying = _mediaPlayer.IsPlaying;
                    long currentTime = _mediaPlayer.Time;
                    int currentVolume = _mediaPlayer.Volume;
                    var currentMedia = _mediaPlayer.Media;
                    int? currentAudioTrackId = _selectedAudioTrackId ?? _mediaPlayer.AudioTrack;
                    
                    Debug.WriteLine($"Saving current audio track ID: {currentAudioTrackId}");
                    
                    // Save the selected device
                    _selectedAudioDeviceId = selectedDevice.DeviceId;
                    
                    // Update the button text
                    UpdateAudioOutputButtonText(selectedDevice.DeviceName);
                    
                    // Stop current playback
                    if (wasPlaying)
                    {
                        _mediaPlayer.Stop();
                    }
                    
                    // Dispose and recreate LibVLC with new audio device
                    _mediaPlayer.Playing -= OnMediaPlayerPlaying;
                    _mediaPlayer.Paused -= OnMediaPlayerPaused;
                    _mediaPlayer.Stopped -= OnMediaPlayerStopped;
                    _mediaPlayer.EndReached -= OnMediaPlayerEndReached;
                    
                    _libVLC?.Dispose();
                    
                    // Reinitialize with new audio device
                    var options = new List<string>
                    {
                        $"--mmdevice-audio-device={_selectedAudioDeviceId}"
                    };
                    
                    Debug.WriteLine($"Reinitializing VLC with audio device: {_selectedAudioDeviceId}");
                    _libVLC = new LibVLC(options.ToArray());
                    
                    // Reinitialize video view
                    if (_videoView != null)
                    {
                        _videoView.Initialize(_libVLC);
                        _mediaPlayer = _videoView.MediaPlayer;
                        
                        if (_mediaPlayer != null)
                        {
                            _mediaPlayer.Volume = currentVolume;
                            
                            _mediaPlayer.Playing += OnMediaPlayerPlaying;
                            _mediaPlayer.Paused += OnMediaPlayerPaused;
                            _mediaPlayer.Stopped += OnMediaPlayerStopped;
                            _mediaPlayer.EndReached += OnMediaPlayerEndReached;
                            
                            // Resume playback if it was playing
                            if (wasPlaying && currentMedia != null && !string.IsNullOrEmpty(_currentVideoFilePath))
                            {
                                // Reload the video and restore audio track
                                _ = LoadAndPlayVideoWithAudioTrack(_currentVideoFilePath, currentTime, currentAudioTrackId);
                            }
                        }
                    }
                    
                    Debug.WriteLine($"? Audio output successfully changed to: {selectedDevice.DeviceName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error changing audio output: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        private async Task LoadAndPlayVideoWithAudioTrack(string filePath, long restoreTime, int? audioTrackId)
        {
            await LoadAndPlayVideo(filePath);
            
            // Wait for the video to be fully loaded and audio tracks available
            await Task.Delay(2000);
            
            if (_mediaPlayer != null && audioTrackId.HasValue)
            {
                Debug.WriteLine($"Restoring audio track ID: {audioTrackId.Value}");
                _mediaPlayer.SetAudioTrack(audioTrackId.Value);
                _selectedAudioTrackId = audioTrackId.Value;
                
                // Update the UI selection
                UpdateAudioTrackSelection();
            }
            
            // Restore playback position
            if (_mediaPlayer != null && _mediaPlayer.Length > 0 && restoreTime > 0)
            {
                await Task.Delay(300);
                _mediaPlayer.Time = restoreTime;
                Debug.WriteLine($"Restored playback position to: {restoreTime}ms");
            }
        }

        private void SetupDragAndDrop()
        {
            if (_videoContainer != null)
            {
                _videoContainer.AddHandler(DragDrop.DropEvent, Drop);
                _videoContainer.AddHandler(DragDrop.DragOverEvent, DragOver);
            }
        }

        private void SetupKeyboardHandling()
        {
            this.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
            
            // Ensure the window can receive keyboard input
            this.Focusable = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            Debug.WriteLine($"OnKeyDown: Key pressed = {e.Key}, Handled = {e.Handled}");
            
            if (e.Key == Key.Space)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: Space key pressed, calling TogglePlayPause");
                TogglePlayPause();
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                e.Handled = true;
                Debug.WriteLine("OnKeyDown: 0 key pressed, calling StopAndResetPosition");
                StopAndResetPosition();
            }
        }

        private void DragOver(object? sender, Avalonia.Input.DragEventArgs e)
        {
            // Always allow copy operation for file drops
            e.DragEffects = DragDropEffects.Copy;
        }

        private async void Drop(object? sender, Avalonia.Input.DragEventArgs e)
        {
            try
            {
                // Access Data through reflection (Avalonia 12 breaking change workaround)
                // In future, this should be updated to use proper Avalonia 12 API
                var dataProperty = e.GetType().GetProperty("Data");
                if (dataProperty != null)
                {
                    dynamic? data = dataProperty.GetValue(e);
                    if (data != null)
                    {
                        // Try to get files from the data object
                        var files = data.GetFiles() as IEnumerable<IStorageItem>;
                        if (files != null)
                        {
                            var file = files.FirstOrDefault();
                            if (file != null)
                            {
                                var filePath = file.TryGetLocalPath();
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    await LoadAndPlayVideo(filePath);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling file drop: {ex.Message}");
            }
        }

        private async Task LoadAndPlayVideo(string filePath)
        {
            if (_mediaPlayer == null || _libVLC == null) return;

            try
            {
                if (_mediaPlayer.IsPlaying)
                {
                    _mediaPlayer.Stop();
                }

                _currentMedia?.Dispose();
                _currentVideoFilePath = filePath;

                // Load tempo track if .smt file exists
                var smtFilePath = IOPath.ChangeExtension(filePath, ".smt");
                _tempoTrack = TempoTrack.LoadFromFile(smtFilePath);
                if (_tempoTrack != null && _tempoTrack.IsLoaded)
                {
                    _lastDisplayedBpm = -1.0;
                }
                else
                {
                    _tempoTrack = null;
                }

                // Update window title with video filename
                var videoFileName = IOPath.GetFileName(filePath);
                this.Title = $"{videoFileName} - DM Video Player";
                
                // Update file name overlay
                UpdateFileNameDisplay(filePath);

                var externalAudioFiles = FindExternalAudioFiles(filePath);

                if (externalAudioFiles.Count > 0)
                {
                    _currentMedia = new Media(_libVLC, filePath, FromType.FromPath);
                    
                    var audiouris = externalAudioFiles.Select(af => new Uri(af.FilePath).AbsoluteUri);
                    var inputSlaveOption = string.Join("#", audiouris);
                    
                    Debug.WriteLine($"Input-slave option: :input-slave={inputSlaveOption}");
                    _currentMedia.AddOption($":input-slave={inputSlaveOption}");
                }
                else
                {
                    _currentMedia = new Media(_libVLC, filePath, FromType.FromPath);
                }

                await _currentMedia.Parse(MediaParseOptions.ParseNetwork);

                _mediaPlayer.Media = _currentMedia;
                
                if (_volumeSlider != null)
                {
                    _mediaPlayer.Volume = (int)_volumeSlider.Value;
                }

                if (externalAudioFiles.Count > 0)
                {
                    _mediaPlayer.Play();
                    await Task.Delay(100);
                    _mediaPlayer.Pause();
                    
                    await Task.Delay(1500);

                    var audioTracks = _mediaPlayer.AudioTrackDescription;
                    if (audioTracks != null)
                    {
                        if (audioTracks.Length > 1)
                        {
                            var lastTrack = audioTracks[audioTracks.Length - 1];
                            _mediaPlayer.SetAudioTrack(lastTrack.Id);
                            _selectedAudioTrackId = lastTrack.Id;
                            Debug.WriteLine($"Initial audio track set to: {lastTrack.Id}");
                        
                            await Task.Delay(200);
                        }
                    }

                    UpdateAudioTrackList(externalAudioFiles);
                    UpdateAudioTrackSelection();

                    _mediaPlayer.Play();

                    // Synchroniser le beat timer après un court délai
                    await Task.Delay(50);
                    SyncBeatTimer();
                }
                else
                {
                    _mediaPlayer.Play();
                    await Task.Delay(1000);
                    UpdateAudioTrackList(externalAudioFiles);

                    // Synchroniser le beat timer
                    SyncBeatTimer();
                }

                if (_placeholderText != null)
                    _placeholderText.IsVisible = false;
                
                // Initialize subtitle button text
                UpdateSubtitleButtonText("Désactivé");
                
                // Show overlays initially when video loads
                ShowOverlays();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading video: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateAudioTrackList(List<ExternalAudioFile> externalAudioFiles)
        {
            if (_mediaPlayer == null || _audioTrackComboBox == null || _audioTrackPanel == null)
                return;

            try
            {
                _isUpdatingAudioTracks = true;

                var audioTracks = _mediaPlayer.AudioTrackDescription;
                Debug.WriteLine($"=== UpdateAudioTrackList ===");
                Debug.WriteLine($"Track count from VLC: {audioTracks?.Length ?? 0}");
                Debug.WriteLine($"External audio files count: {externalAudioFiles.Count}");
                
                if (audioTracks != null && audioTracks.Length > 0)
                {
                    var trackList = new List<AudioTrackItem>();
                    
                    var reorderedFiles = new List<ExternalAudioFile>();
                    if (externalAudioFiles.Count > 1)
                    {
                        for (int i = 1; i < externalAudioFiles.Count; i++)
                        {
                            reorderedFiles.Add(externalAudioFiles[i]);
                        }
                        reorderedFiles.Add(externalAudioFiles[0]);
                    }
                    else if (externalAudioFiles.Count == 1)
                    {
                        reorderedFiles.Add(externalAudioFiles[0]);
                    }
                    
                    Debug.WriteLine($"Reordered external files (VLC order):");
                    for (int i = 0; i < reorderedFiles.Count; i++)
                    {
                        Debug.WriteLine($"  [{i}]: {reorderedFiles[i].DisplayName}");
                    }
                    
                    for (int i = 0; i < audioTracks.Length; i++)
                    {
                        var track = audioTracks[i];
                        
                        if (track.Id == -1)
                        {
                            Debug.WriteLine($"  Skipping track {i} (Id=-1, disabled)");
                            continue;
                        }
                        
                        string displayName;
                        int validTrackIndex = trackList.Count;
                        
                        if (validTrackIndex == 0)
                        {
                            displayName = string.IsNullOrEmpty(track.Name) ? "Video audio" : track.Name;
                            Debug.WriteLine($"  Track {i} (Id={track.Id}): Video track -> '{displayName}'");
                        }
                        else
                        {
                            int externalIndex = validTrackIndex - 1;
                            if (externalIndex < reorderedFiles.Count)
                            {
                                displayName = reorderedFiles[externalIndex].DisplayName;
                                Debug.WriteLine($"  Track {i} (Id={track.Id}): Reordered[{externalIndex}] -> '{displayName}'");
                            }
                            else
                            {
                                displayName = string.IsNullOrEmpty(track.Name) ? $"Track {track.Id}" : track.Name;
                                Debug.WriteLine($"  Track {i} (Id={track.Id}): Fallback -> '{displayName}'");
                            }
                        }
                        
                        trackList.Add(new AudioTrackItem
                        {
                            Id = track.Id,
                            Name = displayName
                        });
                    }

                    Debug.WriteLine($"Final track list:");
                    for (int i = 0; i < trackList.Count; i++)
                    {
                        Debug.WriteLine($"  [{i}] Id={trackList[i].Id}, Name={trackList[i].Name}");
                    }
                    
                    _audioTrackComboBox.ItemsSource = trackList;
                    
                    // Always show audio track selection if there are any tracks
                    _audioTrackPanel.IsVisible = trackList.Count > 0;
                    Debug.WriteLine($"Audio panel visible: {_audioTrackPanel.IsVisible}");

                    UpdateAudioTrackSelection();
                }
                else
                {
                    Debug.WriteLine("No audio tracks available");
                    _audioTrackPanel.IsVisible = false;
                }
            }
            finally
            {
                _isUpdatingAudioTracks = false;
            }
        }

        private void UpdateAudioTrackSelection()
        {
            if (_mediaPlayer == null || _audioTrackComboBox == null || _audioTrackComboBox.ItemsSource == null)
                return;

            try
            {
                _isUpdatingAudioTracks = true;

                var currentTrackId = _mediaPlayer.AudioTrack;
                var tracks = _audioTrackComboBox.ItemsSource as List<AudioTrackItem>;
                
                if (tracks != null)
                {
                    var selectedTrack = tracks.FirstOrDefault(t => t.Id == currentTrackId);
                    if (selectedTrack != null)
                    {
                        _audioTrackComboBox.SelectedItem = selectedTrack;
                        UpdateAudioTrackButtonText(selectedTrack.Name);
                    }
                }
            }
            finally
            {
                _isUpdatingAudioTracks = false;
            }
        }

        private void UpdateAudioTrackButtonText(string trackName)
        {
            if (_audioTrackButtonText != null)
            {
                _audioTrackButtonText.Text = trackName;
            }
        }

        private void UpdateAudioOutputButtonText(string deviceName)
        {
            if (_audioOutputButtonText != null)
            {
                _audioOutputButtonText.Text = deviceName;
            }
        }

        private void AudioTrackComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingAudioTracks || _mediaPlayer == null || _audioTrackComboBox == null)
                return;

            if (_audioTrackComboBox.SelectedItem is AudioTrackItem selectedTrack)
            {
                _mediaPlayer.SetAudioTrack(selectedTrack.Id);
                _selectedAudioTrackId = selectedTrack.Id;
                UpdateAudioTrackButtonText(selectedTrack.Name);
                Debug.WriteLine($"Audio track changed to: {selectedTrack.Name} (ID: {selectedTrack.Id})");
            }
        }

        private void AudioOutputButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_audioOutputButton == null || _audioOutputComboBox == null)
                return;

            var menu = new ContextMenu();

            if (_audioOutputComboBox.ItemsSource != null)
            {
                foreach (var item in _audioOutputComboBox.ItemsSource)
                {
                    var menuItem = new MenuItem
                    {
                        Header = item.ToString(),
                        Tag = item
                    };

                    menuItem.Click += (s, args) =>
                    {
                        if (menuItem.Tag is AudioOutputDevice device)
                        {
                            _audioOutputComboBox.SelectedItem = device;
                        }
                    };

                    menu.Items.Add(menuItem);
                }
            }

            menu.PlacementTarget = _audioOutputButton;
            menu.Placement = PlacementMode.Top;
            menu.Open(_audioOutputButton);
        }

        private void AudioTrackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_audioTrackButton == null || _audioTrackComboBox == null)
                return;

            var menu = new ContextMenu();
            var selectedItem = _audioTrackComboBox.SelectedItem;

            if (_audioTrackComboBox.ItemsSource != null)
            {
                foreach (var item in _audioTrackComboBox.ItemsSource)
                {
                    var menuItem = new MenuItem
                    {
                        Header = item.ToString(),
                        Tag = item
                    };

                    menuItem.Click += (s, args) =>
                    {
                        if (menuItem.Tag is AudioTrackItem track)
                        {
                            _audioTrackComboBox.SelectedItem = track;
                        }
                    };

                    menu.Items.Add(menuItem);
                }
            }

            menu.PlacementTarget = _audioTrackButton;
            menu.Placement = PlacementMode.Top;
            menu.Open(_audioTrackButton);
        }

        private void ShowContextMenuFromComboBox(Control button, ComboBox comboBox, Action<object> onItemSelected)
        {
            if (comboBox.ItemsSource == null)
                return;

            var menu = new ContextMenu();
            var selectedItem = comboBox.SelectedItem;
            var accentColor = TryGetSystemAccentColor();

            foreach (var item in comboBox.ItemsSource)
            {
                var menuItem = new MenuItem
                {
                    Header = item.ToString(),
                    Tag = item
                };

                if (item == selectedItem)
                {
                    menuItem.Foreground = new SolidColorBrush(accentColor);
                    menuItem.FontWeight = FontWeight.Bold;
                }

                menuItem.Click += (s, args) =>
                {
                    if (menuItem.Tag != null)
                    {
                        onItemSelected(menuItem.Tag);
                    }
                };

                menu.Items.Add(menuItem);
            }

            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Top;
            menu.Open(button);
        }

        private void SubtitleButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_subtitleButton == null || _mediaPlayer == null)
                return;

            var subtitleTracks = new List<SubtitleTrackItem>();
            subtitleTracks.Add(new SubtitleTrackItem { Id = -1, Name = "Désactivé" });

            var spuDescriptions = _mediaPlayer.SpuDescription;
            if (spuDescriptions != null && spuDescriptions.Length > 0)
            {
                foreach (var desc in spuDescriptions)
                {
                    if (desc.Id != -1)
                    {
                        subtitleTracks.Add(new SubtitleTrackItem
                        {
                            Id = desc.Id,
                            Name = string.IsNullOrEmpty(desc.Name) ? $"Sous-titre {desc.Id}" : desc.Name
                        });
                    }
                }
            }

            var menu = new ContextMenu();
            var currentSpu = _mediaPlayer.Spu;

            foreach (var track in subtitleTracks)
            {
                var menuItem = new MenuItem
                {
                    Header = track.Name,
                    Tag = track
                };

                menuItem.Click += (s, args) =>
                {
                    if (menuItem.Tag is SubtitleTrackItem subTrack)
                    {
                        _mediaPlayer.SetSpu(subTrack.Id);
                        UpdateSubtitleButtonText(subTrack.Name);
                        Debug.WriteLine($"Subtitle track changed to: {subTrack.Name} (ID: {subTrack.Id})");
                    }
                };

                menu.Items.Add(menuItem);
            }

            menu.PlacementTarget = _subtitleButton;
            menu.Placement = PlacementMode.Top;
            menu.Open(_subtitleButton);
        }

        private void UpdateSubtitleButtonText(string trackName)
        {
            if (_subtitleButtonText != null)
            {
                _subtitleButtonText.Text = trackName;
            }
        }

        private Color TryGetSystemAccentColor()
        {
            try
            {
                if (Application.Current?.PlatformSettings != null)
                {
                    var accentColor = Application.Current.PlatformSettings.GetColorValues().AccentColor1;
                    if (accentColor != default)
                    {
                        return accentColor;
                    }
                }
            }
            catch
            {
                // Fallback silently
            }

            return Color.FromRgb(0, 120, 215);
        }

        private List<ExternalAudioFile> FindExternalAudioFiles(string videoFilePath)
        {
            var result = new List<ExternalAudioFile>();
            
            if (string.IsNullOrEmpty(videoFilePath) || !File.Exists(videoFilePath))
                return result;

            var directory = IOPath.GetDirectoryName(videoFilePath);
            var fileNameWithoutExtension = IOPath.GetFileNameWithoutExtension(videoFilePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
                return result;

            string[] audioExtensions = { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma" };

            var allFiles = Directory.GetFiles(directory, $"{fileNameWithoutExtension}_*.*");
            
            foreach (var file in allFiles)
            {
                var extension = IOPath.GetExtension(file).ToLowerInvariant();
                
                if (audioExtensions.Contains(extension))
                {
                    var fileName = IOPath.GetFileNameWithoutExtension(file);
                    
                    if (fileName.StartsWith(fileNameWithoutExtension + "_"))
                    {
                        var suffix = fileName.Substring(fileNameWithoutExtension.Length + 1);
                        
                        var displayName = char.ToUpper(suffix[0]) + suffix.Substring(1) + " track";
                        
                        result.Add(new ExternalAudioFile
                        {
                            FilePath = file,
                            Suffix = suffix,
                            DisplayName = displayName
                        });
                        
                        Debug.WriteLine($"Found external audio: {file} -> {displayName}");
                    }
                }
            }

            Debug.WriteLine($"Files in alphabetical order:");
            for (int i = 0; i < result.Count; i++)
            {
                Debug.WriteLine($"  [{i}]: {result[i].DisplayName} from {IOPath.GetFileName(result[i].FilePath)}");
            }

            return result;
        }

        private void TogglePlayPause()
        {
            if (_mediaPlayer == null)
            {
                Debug.WriteLine("TogglePlayPause: _mediaPlayer is null");
                return;
            }

            if (_mediaPlayer.Media == null)
            {
                Debug.WriteLine("TogglePlayPause: _mediaPlayer.Media is null");
                return;
            }

            // Check if media is actually playing by checking the state
            var state = _mediaPlayer.State;
            Debug.WriteLine($"TogglePlayPause: Current state = {state}");

            if (state == VLCState.Playing)
            {
                Debug.WriteLine("TogglePlayPause: Pausing playback");
                _mediaPlayer.Pause();
                _updateTimer?.Stop();
                StopBeatTimer();
            }
            else
            {
                Debug.WriteLine($"TogglePlayPause: Starting playback from state {state}");
                _mediaPlayer.Play();
                _updateTimer?.Start();

                // Synchroniser le beat timer après un court délai pour laisser le player changer d'état
                Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => SyncBeatTimer());
                });

                Debug.WriteLine("TogglePlayPause: Play() called and timer started");
            }
        }

        private void PlayPauseButton_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("PlayPauseButton_Click: Button clicked");
            TogglePlayPause();
        }

        private void StopButton_Click(object? sender, RoutedEventArgs e)
        {
            Debug.WriteLine("StopButton_Click: Stop button clicked");
            StopAndResetPosition();
        }

        private async void LoadButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var storageProvider = StorageProvider;
                
                if (storageProvider == null)
                {
                    Debug.WriteLine("StorageProvider is not available");
                    return;
                }

                var fileTypes = new FilePickerFileType[]
                {
                    new("Fichiers vidéo")
                    {
                        Patterns = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.wmv", "*.flv", "*.webm", "*.m4v", "*.mpg", "*.mpeg", "*.3gp", "*.ts" }
                    },
                    FilePickerFileTypes.All
                };

                var options = new FilePickerOpenOptions
                {
                    Title = "Sélectionner une vidéo",
                    AllowMultiple = false,
                    FileTypeFilter = fileTypes
                };

                var result = await storageProvider.OpenFilePickerAsync(options);

                if (result != null && result.Count > 0)
                {
                    var filePath = result[0].Path.LocalPath;
                    await LoadAndPlayVideo(filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening file dialog: {ex.Message}");
            }
        }

        private void StopAndResetPosition()
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null) return;

            Debug.WriteLine($"StopAndResetPosition: Current state = {_mediaPlayer.State}");
            
            // Pause the playback first
            _mediaPlayer.Pause();
            
            Debug.WriteLine("StopAndResetPosition: Paused, now resetting position");
            
            // Reset position to beginning
            _mediaPlayer.Time = 0;

            // Stop the update timer
            _updateTimer?.Stop();

            // Reset UI controls
            if (_positionSlider != null)
                _positionSlider.Value = 0;

            if (_timeLabel != null)
                _timeLabel.Text = "00:00:00";
                
            Debug.WriteLine($"StopAndResetPosition: Complete. New state = {_mediaPlayer.State}");
        }

        private void VolumeSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Value" && _volumeSlider != null && _mediaPlayer != null)
            {
                _mediaPlayer.Volume = (int)_volumeSlider.Value;
                
                // Update mute state based on volume
                if (_volumeSlider.Value > 0 && _isMuted)
                {
                    _isMuted = false;
                    UpdateVolumeIcon();
                }
                else if (_volumeSlider.Value == 0 && !_isMuted)
                {
                    _isMuted = true;
                    UpdateVolumeIcon();
                }
            }
        }

        private void VolumeButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null || _volumeSlider == null)
                return;

            if (_isMuted)
            {
                // Unmute: restore previous volume
                _volumeSlider.Value = _volumeBeforeMute;
                _mediaPlayer.Volume = _volumeBeforeMute;
                _isMuted = false;
                Debug.WriteLine($"Volume unmuted: restored to {_volumeBeforeMute}");
            }
            else
            {
                // Mute: save current volume and set to 0
                _volumeBeforeMute = (int)_volumeSlider.Value;
                _volumeSlider.Value = 0;
                _mediaPlayer.Volume = 0;
                _isMuted = true;
                Debug.WriteLine($"Volume muted: saved volume {_volumeBeforeMute}");
            }

            UpdateVolumeIcon();
        }

        private void BalanceLockButton_Click(object? sender, RoutedEventArgs e)
        {
            _isBalanceLocked = !_isBalanceLocked;

            if (_balanceSlider != null)
            {
                _balanceSlider.IsEnabled = !_isBalanceLocked;
            }

            if (_balanceLockIcon != null)
            {
                _balanceLockIcon.Symbol = _isBalanceLocked 
                    ? FluentIcons.Common.Symbol.LockClosed 
                    : FluentIcons.Common.Symbol.LockOpen;
            }

            Debug.WriteLine($"Balance lock toggled: {(_isBalanceLocked ? "Locked" : "Unlocked")}");
            
            // Save settings
            SaveSettings();
        }

        private void BalanceSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Value" && sender is Slider slider)
            {
                double value = slider.Value;
                
                // Snap to center when value is between -25 and +25
                if (value > -25 && value < 25 && value != 0)
                {
                    slider.Value = 0;
                    Debug.WriteLine("Balance auto-centrée (snap to center)");
                    return;
                }
                
                float balance = (float)(slider.Value / 100.0);
                if (WindowsAudio.SetAudioBalance(balance))
                {
                    Debug.WriteLine($"Balance audio ajustée: {balance:F2} ({slider.Value})");
                }
            }
        }

        private void BalanceSlider_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.Value = 0;
                Debug.WriteLine("Balance audio réinitialisée au centre");
            }
        }

        private void UpdateVolumeIcon()
        {
            if (_volumeIcon == null || _volumeSlider == null)
                return;

            var volume = (int)_volumeSlider.Value;

            if (volume == 0 || _isMuted)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.SpeakerMute;
            }
            else if (volume < 33)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker0;
            }
            else if (volume < 66)
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker1;
            }
            else
            {
                _volumeIcon.Symbol = FluentIcons.Common.Symbol.Speaker2;
            }
        }
        
        private void PositionSlider_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isDraggingPosition = true;
            _isUserInteractingWithSlider = true;
            Debug.WriteLine("PositionSlider: PointerPressed - Timer désactivé");
        }

        private void PositionSlider_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                if (!_isDraggingPosition)
                {
                    _isDraggingPosition = true;
                    _isUserInteractingWithSlider = true;
                    Debug.WriteLine("PositionSlider: PointerMoved with button pressed - Timer désactivé");
                }
                UpdateTimecodeLabelFromSlider();
            }
        }

        private void PositionSlider_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            Debug.WriteLine("PositionSlider: PointerReleased - Mise à jour de la position");

            Dispatcher.UIThread.Post(() =>
            {
                if (_mediaPlayer != null && _positionSlider != null && _isUserInteractingWithSlider)
                {
                    float position = (float)(_positionSlider.Value / 100.0);
                    _mediaPlayer.Position = position;
                    Debug.WriteLine($"Position vidéo mise à jour: {position * 100:F1}%");

                    // Resynchroniser le beat timer après le seek avec un court délai
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        Dispatcher.UIThread.Post(() => SyncBeatTimer());
                    });
                }
                UpdateTimecodeLabelFromSlider();
                _isDraggingPosition = false;
                _isUserInteractingWithSlider = false;
                Debug.WriteLine("PositionSlider: Timer réactivé");
            }, DispatcherPriority.Background);
        }

        private void PositionSlider_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            Debug.WriteLine("PositionSlider: PointerCaptureLost - Mise à jour de la position");
            
            Dispatcher.UIThread.Post(() =>
            {
                if (_mediaPlayer != null && _positionSlider != null && _isUserInteractingWithSlider)
                {
                    float position = (float)(_positionSlider.Value / 100.0);
                    _mediaPlayer.Position = position;
                    Debug.WriteLine($"Position vidéo mise à jour: {position * 100:F1}%");
                }
                UpdateTimecodeLabelFromSlider();
                _isDraggingPosition = false;
                _isUserInteractingWithSlider = false;
                Debug.WriteLine("PositionSlider: Timer réactivé");
            }, DispatcherPriority.Background);
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaPlayer == null) return;

            if (!_isDraggingPosition && _positionSlider != null)
            {
                _positionSlider.Value = _mediaPlayer.Position * 100;
            }

            if (_timeLabel != null)
            {
                var time = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
                _timeLabel.Text = $"{time:hh\\:mm\\:ss}";
            }

            if (_durationLabel != null && _mediaPlayer.Length > 0)
            {
                var duration = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
                _durationLabel.Text = $"{duration:hh\\:mm\\:ss}";
            }

            // Update timecode at 60 FPS for smooth display
            var isPlaying = _mediaPlayer.IsPlaying;
            if (_timecodeLabel != null && isPlaying)
            {
                var vlcTime = _mediaPlayer.Time;
                var fps = _mediaPlayer.Fps;

                // Interpolate time between VLC updates for smooth display
                long interpolatedTime;

                if (vlcTime != _lastVlcTime)
                {
                    // VLC time changed - reset interpolation
                    _lastVlcTime = vlcTime;
                    _interpolationTimer.Restart();
                    interpolatedTime = vlcTime;
                    _isInterpolating = true;
                }
                else if (_isInterpolating && _interpolationTimer.IsRunning)
                {
                    // Interpolate based on elapsed time
                    interpolatedTime = _lastVlcTime + _interpolationTimer.ElapsedMilliseconds;
                }
                else
                {
                    // Fallback to VLC time
                    interpolatedTime = vlcTime;
                }

                // Calculate timecode HH:MM:SS:FF using interpolated time
                var totalSeconds = (int)(interpolatedTime / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((interpolatedTime % 1000) * fps / 1000.0) : 0;

                var timecodeText = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2}";

                // Only update if text changed (avoid unnecessary layout)
                if (timecodeText != _lastTimecodeText)
                {
                    _timecodeLabel.Text = timecodeText;
                    _lastTimecodeText = timecodeText;
                }
            }

            // Update BPM display for tempo track
            if (isPlaying && _mediaPlayer.Time > 0)
            {
                var currentTimeInSeconds = _mediaPlayer.Time / 1000.0;
                UpdateBeatFlash(currentTimeInSeconds);
            }
        }

        private void OnMediaPlayerPlaying(object? sender, EventArgs e)
        {
            // Reset interpolation when playback starts
            _interpolationTimer.Reset();
            _isInterpolating = false;

            _updateTimer?.Start();

            Dispatcher.UIThread.Post(() => 
            {
                UpdatePlayPauseIcon(true);
                // Start auto-hide timer when playing
                _hideControlsTimer?.Start();
            });
        }

        private void OnMediaPlayerPaused(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();

            // Stop interpolation when paused
            _interpolationTimer.Stop();
            _isInterpolating = false;

            Dispatcher.UIThread.Post(() => 
            {
                UpdatePlayPauseIcon(false);
                // Show controls when paused
                ShowOverlays();
                _hideControlsTimer?.Stop();
            });
        }

        private void OnMediaPlayerStopped(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();

            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(false);
                if (_positionSlider != null)
                    _positionSlider.Value = 0;
                if (_timeLabel != null)
                    _timeLabel.Text = "00:00:00";
                // Show controls when stopped
                ShowOverlays();
                _hideControlsTimer?.Stop();
            });
        }

        private void OnMediaPlayerEndReached(object? sender, EventArgs e)
        {
            _updateTimer?.Stop();
            
            Dispatcher.UIThread.Post(() =>
            {
                UpdatePlayPauseIcon(false);
                
                // Reset position to beginning so the video can be replayed
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                }
                
                if (_positionSlider != null)
                    _positionSlider.Value = 0;
                    
                if (_timeLabel != null)
                    _timeLabel.Text = "00:00:00";
            });
        }

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (_playPauseIcon != null)
            {
                _playPauseIcon.Symbol = isPlaying ? FluentIcons.Common.Symbol.Pause : FluentIcons.Common.Symbol.Play;
            }
        }

        private System.Timers.Timer? _singleClickTimer;
        private const int SingleClickDelay = 150; // ms
        private bool _doubleClickDetected = false;

        private void VideoContainer_Tapped(object? sender, TappedEventArgs e)
        {
            // Démarre un timer pour différer l'action du simple clic
            _doubleClickDetected = false;
            _singleClickTimer?.Stop();
            _singleClickTimer = new System.Timers.Timer(SingleClickDelay);
            _singleClickTimer.Elapsed += (s, args) =>
            {
                _singleClickTimer?.Stop();
                if (!_doubleClickDetected)
                {
                    Dispatcher.UIThread.Post(() => TogglePlayPause());
                }
            };
            _singleClickTimer.AutoReset = false;
            _singleClickTimer.Start();
        }

        private void VideoContainer_DoubleTapped(object? sender, TappedEventArgs e)
        {
            _doubleClickDetected = true;
            _singleClickTimer?.Stop();
            ToggleFullScreen();
        }

        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.FullScreen)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.FullScreen;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Save settings before closing
            SaveSettings();

            _updateTimer?.Stop();
            
            _currentMedia?.Dispose();
            _libVLC?.Dispose();
        }

        private void VideoContainer_PointerMoved(object? sender, PointerEventArgs e)
        {
            ShowOverlays();
            _hideControlsTimer?.Stop();
            
            // Only start auto-hide timer if media is loaded and playing
            if (_currentMedia != null && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _hideControlsTimer?.Start();
            }
        }

        private void VideoContainer_PointerExited(object? sender, PointerEventArgs e)
        {
            _hideControlsTimer?.Stop();
            
            // Only start auto-hide timer if media is loaded and playing
            if (_currentMedia != null && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                _hideControlsTimer?.Start();
            }
        }

        private void ShowOverlays()
        {
            // Always show controls overlay (even without media)
            if (_controlsOverlay != null)
                _controlsOverlay.IsVisible = true;
            
            // Only show file name overlay if there's a video loaded
            if (_fileNameOverlay != null && !string.IsNullOrEmpty(_currentVideoFilePath))
                _fileNameOverlay.IsVisible = true;
            
            // Only show status bar if there's media playing
            if (_statusBar != null && _currentMedia != null)
                _statusBar.IsVisible = true;

            // Afficher le timecode overlay selon la case à cocher (jamais caché automatiquement)
            UpdateTimecodeVisibility();
        }

        private void HideOverlays()
        {
            // Don't hide if media player is paused or stopped
            if (_mediaPlayer != null && !_mediaPlayer.IsPlaying)
                return;

            // Don't hide controls if no media is loaded (to allow audio output selection)
            if (_currentMedia == null)
                return;

            if (_controlsOverlay != null)
                _controlsOverlay.IsVisible = false;
            
            if (_fileNameOverlay != null)
                _fileNameOverlay.IsVisible = false;
            
            if (_statusBar != null)
                _statusBar.IsVisible = false;

            // Ne plus masquer le timecode ici : sa visibilité est gérée par UpdateTimecodeVisibility()
        }

        private void UpdateFileNameDisplay(string filePath)
        {
            if (_fileNameLabel != null && !string.IsNullOrEmpty(filePath))
            {
                _fileNameLabel.Text = IOPath.GetFileName(filePath);
                if (_fileNameOverlay != null)
                    _fileNameOverlay.IsVisible = true;
            }
        }

        private void UpdateStatusLabel(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = text;
            }
        }

        private static string GetSettingsFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = IOPath.Combine(appDataPath, "DMVideoPlayer");
            Directory.CreateDirectory(appFolder);
            return IOPath.Combine(appFolder, "settings.json");
        }

        private AppSettings LoadSettings()
        {
            try
            {
                string settingsPath = GetSettingsFilePath();
                
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        Debug.WriteLine($"Settings loaded: AudioDeviceId={settings.AudioOutputDeviceId}, Volume={settings.Volume}, Balance={settings.Balance}, IsBalanceLocked={settings.IsBalanceLocked}");
                        return settings;
                    }
                }
                
                Debug.WriteLine("No settings file found, using defaults");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            
            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    AudioOutputDeviceId = _selectedAudioDeviceId,
                    Volume = _volumeSlider != null ? (int)_volumeSlider.Value : 100,
                    Balance = 0.0,
                    IsBalanceLocked = _isBalanceLocked,
                    ShowTimecode = _timecodeCheckBox != null && _timecodeCheckBox.IsChecked == true // Sauvegarde de l'état timecode
                };

                var balanceSlider = this.FindControl<Slider>("BalanceSlider");
                if (balanceSlider != null)
                {
                    settings.Balance = balanceSlider.Value;
                }

                string settingsPath = GetSettingsFilePath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, json);
                
                Debug.WriteLine($"Settings saved: AudioDeviceId={settings.AudioOutputDeviceId}, Volume={settings.Volume}, Balance={settings.Balance}, IsBalanceLocked={settings.IsBalanceLocked}, ShowTimecode={settings.ShowTimecode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        private void UpdateTimecodeVisibility()
        {
            if (_timecodeOverlay != null && _timecodeCheckBox != null)
            {
                _timecodeOverlay.IsVisible = _timecodeCheckBox.IsChecked == true;
            }

            // Lier la visibilité du BPM overlay au timecode
            if (_bpmOverlay != null && _timecodeCheckBox != null)
            {
                _bpmOverlay.IsVisible = _timecodeCheckBox.IsChecked == true && _tempoTrack != null && _tempoTrack.IsLoaded;
            }
        }

        private void UpdateTimecodeLabelFromSlider()
        {
            if (_timecodeLabel != null && _mediaPlayer != null && _positionSlider != null)
            {
                var duration = _mediaPlayer.Length;
                var sliderPosition = _positionSlider.Value / 100.0;
                var ms = duration > 0 ? (long)(duration * sliderPosition) : 0;
                var fps = _mediaPlayer.Fps;

                // Calculate timecode
                var totalSeconds = (int)(ms / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((ms % 1000) * fps / 1000.0) : 0;

                _timecodeLabel.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2}";
            }
        }

        private void UpdateTimecodeLabelFromPlayer()
        {
            if (_timecodeLabel != null && _mediaPlayer != null)
            {
                var ms = _mediaPlayer.Time;
                var fps = _mediaPlayer.Fps;

                // Calculate timecode
                var totalSeconds = (int)(ms / 1000);
                var hours = totalSeconds / 3600;
                var minutes = (totalSeconds % 3600) / 60;
                var seconds = totalSeconds % 60;
                var frameInSecond = fps > 0 ? (int)((ms % 1000) * fps / 1000.0) : 0;

                _timecodeLabel.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frameInSecond:D2}";
            }
        }
    }

    public class AudioOutputDevice
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;

        public override string ToString() => DeviceName;
    }

    public class AudioTrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class SubtitleTrackItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class ExternalAudioFile
    {
        public string FilePath { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        public string? AudioOutputDeviceId { get; set; }
        public int Volume { get; set; } = 100;
        public double Balance { get; set; } = 0.0;
        public bool IsBalanceLocked { get; set; } = false;
        public bool ShowTimecode { get; set; } = false; // Ajout pour la case à cocher timecode
    }

    public partial class MainWindow
    {
        /// <summary>
        /// Synchronise le timer de beats avec la position vidéo actuelle.
        /// À appeler lors du play initial, après un seek, ou après pause/stop.
        /// </summary>
        private void SyncBeatTimer()
        {
            lock (_beatTimerLock)
            {
                // Arrêter le timer actuel
                _beatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _nextScheduledBeat = -1.0;

                if (_mediaPlayer == null || _tempoTrack == null || !_tempoTrack.IsLoaded)
                    return;

                // Note: On ne vérifie pas IsPlaying ici car cette méthode est appelée juste après Play()
                // et l'état peut ne pas être encore synchronisé

                var currentTime = _mediaPlayer.Time / 1000.0;

                // Trouver le prochain beat à partir de la position actuelle
                var nextBeat = _tempoTrack.GetNextBeatTime(currentTime);

                if (nextBeat < 0)
                    return; // Pas de beats à venir

                // Calculer le délai jusqu'au prochain beat
                var delay = nextBeat - currentTime;

                if (delay <= 0)
                {
                    // Beat déjà passé, chercher le suivant
                    nextBeat = _tempoTrack.GetNextBeatTime(nextBeat + 0.001);
                    if (nextBeat < 0)
                        return;
                    delay = nextBeat - currentTime;
                }

                if (delay > 0)
                {
                    _nextScheduledBeat = nextBeat;
                    int delayMs = Math.Max(1, (int)(delay * 1000.0));
                    _beatTimer?.Change(delayMs, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Callback du timer de beats (haute précision, indépendant des frames vidéo)
        /// </summary>
        private void OnBeatTimerCallback(object? state)
        {
            if (_mediaPlayer == null || !_mediaPlayer.IsPlaying)
                return;

            lock (_beatTimerLock)
            {
                var currentTime = _mediaPlayer.Time / 1000.0;
                var beatTime = _nextScheduledBeat;

                if (beatTime < 0)
                    return;

                // Flash le beat
                var currentBpm = _tempoTrack?.GetBpmAtTime(beatTime) ?? 120.0;
                var secondsPerBeat = 60.0 / currentBpm;
                var flashDuration = (int)((secondsPerBeat * 0.15) * 1000.0);

                Dispatcher.UIThread.Post(() => FlashBeatLed(true));

                // Auto-off après la durée du flash
                Task.Delay(flashDuration).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => FlashBeatLed(false));
                });

                // Planifier le prochain beat
                ScheduleNextBeat();
            }
        }

        /// <summary>
        /// Planifie le prochain beat après celui qui vient de se produire
        /// </summary>
        private void ScheduleNextBeat()
        {
            if (_tempoTrack == null || !_tempoTrack.IsLoaded)
                return;

            var currentBeat = _nextScheduledBeat;
            if (currentBeat < 0)
                return;

            // Trouver le beat suivant dans la timeline
            var nextBeat = _tempoTrack.GetNextBeatTime(currentBeat + 0.001);

            if (nextBeat < 0)
            {
                _nextScheduledBeat = -1.0;
                return; // Plus de beats
            }

            // Calculer le délai exact entre les deux beats
            var delay = nextBeat - currentBeat;

            if (delay > 0)
            {
                _nextScheduledBeat = nextBeat;
                int delayMs = Math.Max(1, (int)(delay * 1000.0));
                _beatTimer?.Change(delayMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Arrête le timer de beats
        /// </summary>
        private void StopBeatTimer()
        {
            lock (_beatTimerLock)
            {
                _beatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _nextScheduledBeat = -1.0;
                FlashBeatLed(false);
            }
        }

        private void UpdateBeatFlash(double currentTimeInSeconds)
        {
            if (_tempoTrack == null || !_tempoTrack.IsLoaded)
            {
                return;
            }

            // Afficher et mettre à jour le BPM (avec lissage)
            var currentBpm = _tempoTrack.GetBpmAtTime(currentTimeInSeconds);
            var roundedBpm = Math.Round(currentBpm);

            if (_bpmLabelOverlay != null && Math.Abs(roundedBpm - _lastDisplayedBpm) >= 1.0)
            {
                _lastDisplayedBpm = roundedBpm;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_bpmLabelOverlay != null)
                    {
                        _bpmLabelOverlay.Text = $"{roundedBpm:F0} BPM";
                    }
                    UpdateTimecodeVisibility();
                });
            }
            else if (_lastDisplayedBpm < 0)
            {
                _lastDisplayedBpm = roundedBpm;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_bpmLabelOverlay != null)
                    {
                        _bpmLabelOverlay.Text = $"{roundedBpm:F0} BPM";
                    }
                    UpdateTimecodeVisibility();
                });
            }

            // Les beats sont gérés par le timer indépendant, pas ici
        }

        private void FlashBeatLed(bool flash)
        {
            if (_beatLed == null)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                if (_beatLed == null)
                    return;

                if (flash)
                {
                    // LED allumée avec glow intense
                    _beatLed.Opacity = 1.0;
                }
                else
                {
                    // LED éteinte (mais visible avec opacité réduite)
                    _beatLed.Opacity = 0.3;
                }
            });
        }

            }
        }
