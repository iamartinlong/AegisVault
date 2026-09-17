using System.Diagnostics;
using AegisVault.App.Localization;
using AegisVault.App.Services;
using AegisVault.App.Theme;
using AegisVault.App.ViewModels;
using AegisVault.App.Views;
using AegisVault.Core.Models;
using AegisVault.Core.Services;
using AegisVault.Platform;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AegisVault.App;

public partial class App : Application
{
    /// <summary>Shortest time the splash stays up, so it never just flickers.</summary>
    private const int MinimumSplashMilliseconds = 500;

    /// <summary>
    /// Short yield after showing the splash so the compositor picks the window
    /// up before the (blocking) AtomUI initialisation starts. The first frame
    /// itself is rendered by the compositor thread, so it also arrives while
    /// the UI thread is busy.
    /// </summary>
    private const int SplashPresentSettleMilliseconds = 200;

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private readonly AppPreferencesStore _preferencesStore = new();
    private AppPreferences _preferences = new();
    private SplashWindow? _splash;
    private VaultService? _vault;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private AutoLockService? _autoLock;
    private ClipboardService? _clipboard;
    private SessionLockWatcher? _sessionWatcher;
    private SecureConfigService? _configService;
    private TrayIcon? _trayIcon;
    private QuickAccessWindow? _quickAccess;
    private FloatingBallWindow? _floatingBall;
    private HotKeyService? _hotKey;
    private readonly IAppRestarter _restarter = ProcessAppRestarter.Instance;
    private bool _unlockDialogOpen;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _preferences = _preferencesStore.Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            Loc.ApplyPreference(_preferences.Language);
            ApplyThemeVariant(_preferences.Theme);

            if (PlatformSettings is { } platformSettings)
            {
                platformSettings.ColorValuesChanged += (_, _) =>
                {
                    if (_preferences.Theme == "system")
                    {
                        ApplyThemeVariant("system");
                    }
                };
            }

            _ = StartAsync(desktop);
        }
        else
        {
            // Headless test host and design-time: no interactive startup, but
            // the AtomUI themes must be registered so controls render.
            InitializeAtomUI();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Staged startup: the brand splash goes up first, then AtomUI and the
    /// shell are initialised, then the vault is opened and the session hands
    /// over to the destination window (main or unlock). The frame yields let
    /// the compositor present the splash before the next stage blocks the UI
    /// thread (AtomUI theme initialisation takes several seconds on a cold
    /// start; the window itself must never flash before it is ready).
    /// </summary>
    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var splash = new SplashWindow(_preferences.Theme);
        _splash = splash;
        var splashShownAt = Stopwatch.GetTimestamp();

        await YieldFrameAsync();
        splash.Show();
        // The first frame is rendered by the compositor thread (so it arrives
        // even while the UI thread is busy); the yield below is just courtesy.
        await Task.Delay(SplashPresentSettleMilliseconds);
        await YieldFrameAsync();

        splash.SetStatus(Loc.T("Splash_LoadingShell"), 0.45);
        await YieldFrameAsync();

        InitializeAtomUI();
        InitializeTray();
        InitializeHotKey();
        await YieldFrameAsync();

        splash.SetStatus(Loc.T("Splash_OpeningVault"), 0.75);
        await ShowUnlockAsync(desktop);
        await CloseSplashAsync(splash, splashShownAt);
    }

    private void InitializeAtomUI()
    {
        this.UseAtomUI(builder =>
        {
            builder.UseDesktopControls();
            builder.UseDesktopColorPicker();
            AppTheme.ConfigureInitial(builder, _preferences.Theme);
        });
    }

    /// <summary>
    /// Yields at a priority below rendering, so the pending layout/animation
    /// frame is composited before the next (blocking) startup stage runs.
    /// </summary>
    private static async Task YieldFrameAsync()
        => await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

    private async Task CloseSplashAsync(SplashWindow splash, long shownAt)
    {
        var remaining = TimeSpan.FromMilliseconds(MinimumSplashMilliseconds)
            - Stopwatch.GetElapsedTime(shownAt);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        if (ReferenceEquals(_splash, splash))
        {
            _splash = null;
        }

        await splash.FadeOutAndCloseAsync();
    }

    private void InitializeTray()
    {
        try
        {
            var trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://AegisVault.App/Assets/tray.png"))),
                ToolTipText = "AegisVault",
            };
            trayIcon.Clicked += OnTrayClicked;

            var menu = new NativeMenu();
            menu.Add(CreateMenuItem(Loc.T("Tray_Open"), OnTrayOpenClicked));
            menu.Add(CreateMenuItem(Loc.T("Tray_QuickAccess"), OnTrayQuickAccessClicked));
            menu.Add(CreateMenuItem(Loc.T("Tray_Settings"), OnTraySettingsClicked));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(CreateMenuItem(Loc.T("Tray_Lock"), OnTrayLockClicked));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(CreateMenuItem(Loc.T("Tray_Exit"), OnTrayExitClicked));
            trayIcon.Menu = menu;

            TrayIcon.SetIcons(this, new TrayIcons { trayIcon });
            _trayIcon = trayIcon;
        }
        catch (Exception)
        {
            // Tray integration is best effort; the application stays usable without it.
        }
    }

    private static NativeMenuItem CreateMenuItem(string header, EventHandler handler)
    {
        var item = new NativeMenuItem(header);
        item.Click += handler;
        return item;
    }

    private async Task ShowUnlockAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var viewModel = new UnlockViewModel
        {
            DeviceKeyProtector = CreateKeyProtector(),
            SecureInputAvailable = OperatingSystem.IsWindows(),
        };
        viewModel.ApplyPreferences(_preferences);

        // Try the remembered device key before anything is shown: on success
        // the vault goes straight to the main window and the unlock window is
        // never created (creating it first made it flash before the main one).
        if (viewModel.CanRememberDevice)
        {
            var vault = await viewModel.TryDeviceUnlockAsync();
            if (vault is not null)
            {
                ShowMain(desktop, unlockWindow: null, vault);
                return;
            }
        }

        CreateUnlockWindow(desktop, viewModel);
    }

    private void CreateUnlockWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        UnlockViewModel viewModel)
    {
        var window = new UnlockWindow { DataContext = viewModel };
        viewModel.VaultOpened += vault => ShowMain(desktop, window, vault);

        desktop.MainWindow = window;
        window.Show();
    }

    private void ShowMain(
        IClassicDesktopStyleApplicationLifetime desktop,
        UnlockWindow? unlockWindow,
        VaultService vault)
    {
        var config = new SecureConfigService(vault);
        _configService = config;
        var clipboard = new ClipboardService(
            new AvaloniaClipboardAccess(() => (TopLevel?)_mainWindow ?? unlockWindow),
            () => config.Current);
        var autoLock = new AutoLockService(() => config.Current);
        var sessionWatcher = new SessionLockWatcher();
        sessionWatcher.ScreenLocked += autoLock.ReportScreenLocked;
        sessionWatcher.Suspended += autoLock.ReportSuspended;
        var viewModel = new MainViewModel(vault, clipboard);
        viewModel.ThemePreference = _preferences.Theme;
        viewModel.LanguagePreference = _preferences.Language;
        viewModel.AttachAppearanceCallbacks(ApplyTheme, ApplyLanguage);
        var window = _mainWindow;

        if (window is null)
        {
            window = new MainWindow();
            _mainWindow = window;
            // Subscribe exactly once per window: the lock overlay must be able
            // to raise UnlockRequested while the vault is locked (the handler
            // is deliberately NOT removed in LockVault).
            window.UnlockRequested += OnUnlockRequested;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_mainWindow, window))
                {
                    CleanupSession();
                }
            };
            desktop.MainWindow = window;
        }

        _vault = vault;
        _mainViewModel = viewModel;
        _autoLock = autoLock;
        _clipboard = clipboard;
        _sessionWatcher = sessionWatcher;

        _preferences = _preferences with { LastVaultPath = vault.VaultPath };
        SavePreferences();

        window.Attach(viewModel, clipboard, autoLock, ShowSettings);
        viewModel.LockRequested += LockVault;
        autoLock.LockTriggered += _ => LockVault();

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
        unlockWindow?.Close();

        ApplyScreenGuard(window);
        UpdateFloatingBall();
    }

    private void LockVault()
    {
        if (_mainWindow is null && _vault is null)
        {
            return;
        }

        _quickAccess?.Hide();

        var window = _mainWindow;
        if (window is null)
        {
            CleanupSession();
            if (_desktop is { } desktop)
            {
                _ = ShowUnlockAsync(desktop);
            }
            return;
        }

        CleanupSession(keepWindow: true);
        window.DataContext = new LockViewModel();
        window.ShowLockOverlay();
        window.Activate();
    }

    private async void OnUnlockRequested()
    {
        if (_mainWindow is null || _unlockDialogOpen)
        {
            return;
        }

        _unlockDialogOpen = true;
        try
        {
            var viewModel = new UnlockViewModel
            {
                DeviceKeyProtector = CreateKeyProtector(),
                SecureInputAvailable = OperatingSystem.IsWindows(),
            };
            viewModel.ApplyPreferences(_preferences);
            var window = new UnlockWindow { DataContext = viewModel };
            viewModel.VaultOpened += vault => ShowMain(_desktop!, window, vault);
            await window.ShowDialog(_mainWindow);
        }
        catch (Exception)
        {
        }
        finally
        {
            _unlockDialogOpen = false;
        }
    }

    private void CleanupSession(bool keepWindow = false)
    {
        _quickAccess?.Hide();
        _quickAccess = null;

        _sessionWatcher?.Dispose();
        _sessionWatcher = null;

        _configService = null;

        _autoLock?.Dispose();
        _autoLock = null;

        _clipboard?.Dispose();
        _clipboard = null;

        if (_mainViewModel is not null)
        {
            _mainViewModel.LockRequested -= LockVault;
            _mainViewModel.Dispose();
            _mainViewModel = null;
        }

        _vault?.Dispose();
        _vault = null;
        if (!keepWindow)
        {
            _mainWindow = null;
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayOpenClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayQuickAccessClicked(object? sender, EventArgs e) => ToggleQuickAccess();

    private void OnTraySettingsClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
        ShowSettings();
    }

    private void OnTrayLockClicked(object? sender, EventArgs e) => LockVault();

    private void OnTrayExitClicked(object? sender, EventArgs e) => ExitApplication(restart: false);

    /// <summary>Closes the session and optionally spawns a fresh instance.</summary>
    private void ExitApplication(bool restart)
    {
        if (restart && !_restarter.TryStartNewInstance())
        {
            // Keep the current session alive rather than leaving the user with nothing.
            _mainViewModel?.StatusMessage = Loc.T("Main_RestartManualHint");
            return;
        }

        CleanupSession();
        _hotKey?.Dispose();
        _floatingBall?.Close();
        _splash?.Close();
        _splash = null;
        _desktop?.Shutdown();
    }

    private void InitializeHotKey()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var hotKey = new HotKeyService();
            if (hotKey.TryRegister(HotKeyService.ModControl | HotKeyService.ModShift, 0x20 /* VK_SPACE */))
            {
                hotKey.HotKeyPressed += () => Dispatcher.UIThread.Post(ToggleQuickAccess);
                _hotKey = hotKey;
            }
            else
            {
                hotKey.Dispose();
            }
        }
        catch (Exception)
        {
            // Global hotkey is best effort; quick access stays available via tray.
        }
    }

    private void ToggleQuickAccess()
    {
        if (_mainViewModel is null || _mainWindow is null)
        {
            // Locked or not yet unlocked: bring the main window up instead.
            ShowMainWindow();
            return;
        }

        if (_quickAccess is null)
        {
            var viewModel = new QuickAccessViewModel(_mainViewModel, _clipboard);
            viewModel.EntryActivated += ShowMainWindow;
            _quickAccess = new QuickAccessWindow { DataContext = viewModel };
            _quickAccess.Closed += (_, _) => viewModel.Dispose();
        }

        if (_quickAccess.IsVisible)
        {
            _quickAccess.Hide();
            _mainWindow.Activate();
            return;
        }

        _quickAccess.Show();
        _quickAccess.Activate();
        ApplyScreenGuard(_quickAccess);
    }

    private void UpdateFloatingBall()
    {
        if (_mainWindow is null || !_preferences.ShowFloatingBall)
        {
            _floatingBall?.Hide();
            return;
        }

        if (_floatingBall is null)
        {
            _floatingBall = new FloatingBallWindow();
            _floatingBall.BallClicked += ToggleQuickAccess;
            _floatingBall.PlacementChanged += OnBallPlacementChanged;
        }

        if (!_floatingBall.IsVisible)
        {
            _floatingBall.Show();

            if (BallPlacement.TryParse(_preferences.BallPosition, out var saved))
            {
                _floatingBall.ApplyPlacement(saved, _preferences.BallDockedSide);
            }
        }
    }

    private void OnBallPlacementChanged(PixelPoint position, string? dockedSide)
    {
        _preferences = _preferences with
        {
            BallPosition = BallPlacement.Format(position),
            BallDockedSide = dockedSide,
        };
        _preferencesStore.Save(_preferences);
    }

    private void ApplyScreenGuard(Avalonia.Controls.Window window)
    {
        // null config (locked) keeps the guard on; only an explicit opt-out lifts it.
        var exclude = _configService?.Current.DisableScreenCapture != false;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? 0;
            ScreenCaptureGuard.TrySetExcluded(handle, exclude);
        }
        catch (Exception)
        {
            // Screen capture exclusion is best effort.
        }
    }

    private static IKeyProtector? CreateKeyProtector()
        => OperatingSystem.IsWindows() ? new DpapiKeyProtector() : null;

    private void SavePreferences()
    {
        try
        {
            _preferencesStore.Save(_preferences);
        }
        catch (Exception)
        {
            // Preferences are non-critical; never block the session on them.
        }
    }

    private void ShowSettings()
    {
        if (_mainWindow is null || _vault is null || _configService is null)
        {
            return;
        }

        var viewModel = new SettingsViewModel(
            _vault,
            _configService,
            CreateKeyProtector(),
            ApplyTheme,
            _preferences.Theme,
            screenGuardSupported: ScreenCaptureGuard.IsSupported,
            applyScreenGuard: _ =>
            {
                if (_mainWindow is { } window)
                {
                    ApplyScreenGuard(window);
                }

                if (_quickAccess is { IsVisible: true } quickAccess)
                {
                    ApplyScreenGuard(quickAccess);
                }
            },
            showFloatingBall: _preferences.ShowFloatingBall,
            applyFloatingBall: show =>
            {
                _preferences = _preferences with { ShowFloatingBall = show };
                SavePreferences();
                UpdateFloatingBall();
            },
            imported: () => _mainViewModel?.ReloadFromVault(),
            currentLanguage: _preferences.Language,
            applyLanguage: language =>
            {
                _preferences = _preferences with { Language = language };
                SavePreferences();
            });
        var window = new SettingsWindow { DataContext = viewModel };
        _ = window.ShowDialog(_mainWindow);
    }

    private void ApplyTheme(string theme)
    {
        _preferences = _preferences with { Theme = theme };
        SavePreferences();
        ApplyThemeVariant(theme);

        // Keep the toolbar icon in sync when the theme is changed elsewhere
        // (e.g. from the settings window).
        if (_mainViewModel is { } viewModel)
        {
            viewModel.ThemePreference = theme;
        }
    }

    /// <summary>
    /// Persists the language preference and offers an immediate restart, since
    /// the string tables are resolved once at startup.
    /// </summary>
    private async void ApplyLanguage(string language)
    {
        var changed = !string.Equals(_preferences.Language, language, StringComparison.Ordinal);
        _preferences = _preferences with { Language = language };
        SavePreferences();

        if (_mainViewModel is { } viewModel)
        {
            viewModel.LanguagePreference = language;
        }

        if (!changed || _mainWindow is not { } owner)
        {
            return;
        }

        var dialog = new ConfirmWindow(
            Loc.T("Main_LanguageSwitchTitle"),
            Loc.T("Main_LanguageSwitchMessage"),
            Loc.T("Main_RestartNow"),
            Loc.T("Main_RestartLater"));

        if (await dialog.ShowDialog<bool>(owner))
        {
            ExitApplication(restart: true);
        }
        else if (_mainViewModel is { } current)
        {
            current.StatusMessage = Loc.T("Main_LanguageRestartHint");
        }
    }

    private void ApplyThemeVariant(string theme) => AppTheme.Apply(this, theme);
}
