using AegisVault.App.Services;
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

namespace AegisVault.App;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private readonly AppPreferencesStore _preferencesStore = new();
    private AppPreferences _preferences = new();
    private bool _initialWindowAssigned;
    private VaultService? _vault;
    private MainWindow? _mainWindow;
    private MainViewModel? _mainViewModel;
    private AutoLockService? _autoLock;
    private ClipboardService? _clipboard;
    private SessionLockWatcher? _sessionWatcher;
    private SecureConfigService? _configService;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        this.UseAtomUI(builder =>
        {
            builder.UseDesktopControls();
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _preferences = _preferencesStore.Load();
            ApplyThemeVariant(_preferences.Theme);
            InitializeTray();
            ShowUnlock(desktop);
        }

        base.OnFrameworkInitializationCompleted();
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
            menu.Add(CreateMenuItem("打开 AegisVault", OnTrayOpenClicked));
            menu.Add(CreateMenuItem("锁定", OnTrayLockClicked));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(CreateMenuItem("退出", OnTrayExitClicked));
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

    private void ShowUnlock(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var viewModel = new UnlockViewModel
        {
            DeviceKeyProtector = CreateKeyProtector(),
            SecureInputAvailable = OperatingSystem.IsWindows(),
        };
        viewModel.ApplyPreferences(_preferences);
        var window = new UnlockWindow { DataContext = viewModel };

        viewModel.VaultOpened += vault => ShowMain(desktop, window, vault);

        desktop.MainWindow = window;
        if (_initialWindowAssigned)
        {
            window.Show();
        }

        _initialWindowAssigned = true;

        if (viewModel.CanRememberDevice)
        {
            _ = viewModel.TryDeviceUnlockAsync();
        }
    }

    private void ShowMain(IClassicDesktopStyleApplicationLifetime desktop, UnlockWindow unlockWindow, VaultService vault)
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
        var window = new MainWindow();

        _vault = vault;
        _mainWindow = window;
        _mainViewModel = viewModel;
        _autoLock = autoLock;
        _clipboard = clipboard;
        _sessionWatcher = sessionWatcher;

        _preferences = _preferences with { LastVaultPath = vault.VaultPath };
        SavePreferences();

        window.Attach(viewModel, clipboard, autoLock, ShowSettings);
        viewModel.LockRequested += LockVault;
        autoLock.LockTriggered += _ => LockVault();
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_mainWindow, window))
            {
                CleanupSession();
            }
        };

        desktop.MainWindow = window;
        window.Show();
        unlockWindow.Close();
    }

    private void LockVault()
    {
        if (_mainWindow is null && _vault is null)
        {
            return;
        }

        var window = _mainWindow;
        CleanupSession();

        if (_desktop is { } desktop)
        {
            ShowUnlock(desktop);
        }

        window?.Close();
    }

    private void CleanupSession()
    {
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
        _mainWindow = null;
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

    private void OnTrayLockClicked(object? sender, EventArgs e) => LockVault();

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        CleanupSession();
        _desktop?.Shutdown();
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
            _preferences.Theme);
        var window = new SettingsWindow { DataContext = viewModel };
        _ = window.ShowDialog(_mainWindow);
    }

    private void ApplyTheme(string theme)
    {
        _preferences = _preferences with { Theme = theme };
        SavePreferences();
        ApplyThemeVariant(theme);
    }

    private void ApplyThemeVariant(string theme)
    {
        RequestedThemeVariant = theme switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
