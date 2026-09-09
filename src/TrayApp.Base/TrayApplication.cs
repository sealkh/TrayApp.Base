using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using TrayApp.Base.Configuration;
using TrayApp.Base.Interop;
using TrayApp.Base.Updates;

namespace TrayApp.Base;

public abstract class TrayApplication : IDisposable
{
    private const nuint CommandOpen = 1001;
    private const nuint CommandSettings = 1002;
    private const nuint CommandExit = 1003;

    private readonly TrayApplicationOptions _options;
    private readonly IUpdateService? _updateService;
    private readonly NativeMethods.WndProc _wndProc;
    private Mutex? _singleInstanceMutex;
    private nint _windowHandle;
    private nint _iconHandle;
    private bool _trayIconAdded;
    private bool _disposed;

    protected TrayApplication(
        TrayApplicationOptions? options = null,
        IUpdateService? updateService = null)
    {
        _options = options ?? new TrayApplicationOptions();
        _updateService = updateService;
        _wndProc = WindowProcedure;
    }

    protected TrayApplicationOptions Options => _options;

    protected IUpdateService? UpdateService => _updateService;

    public int Run()
    {
        ThrowIfDisposed();
        ApplyCulture();

        if (!TryAcquireSingleInstance())
        {
            OnSecondInstanceDetected();
            return 2;
        }

        try
        {
            CreateMessageWindow();
            AddTrayIcon();
            OnStarted();
            return RunMessageLoop();
        }
        finally
        {
            RemoveTrayIcon();
            if (_windowHandle != 0)
            {
                NativeMethods.DestroyWindow(_windowHandle);
                _windowHandle = 0;
            }

            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
            OnStopped();
        }
    }

    protected virtual void OnStarted() { }

    protected virtual void OnStopped() { }

    protected virtual void OnSecondInstanceDetected() { }

    protected abstract void OnOpen();

    protected virtual void OnSettings() { }

    protected virtual void OnExit() { }

    protected virtual string GetOpenMenuText() => "Open";

    protected virtual string GetSettingsMenuText() => "Settings";

    protected virtual string GetExitMenuText() => "Exit";

    protected virtual nint GetTrayIconHandle() =>
        NativeMethods.LoadIcon(0, NativeMethods.IDI_APPLICATION);

    protected void Exit()
    {
        OnExit();
        if (_windowHandle != 0)
        {
            NativeMethods.DestroyWindow(_windowHandle);
        }
    }

    protected Task<UpdateCheckResult?> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        _updateService is null
            ? Task.FromResult<UpdateCheckResult?>(null)
            : CheckCoreAsync(cancellationToken);

    private async Task<UpdateCheckResult?> CheckCoreAsync(CancellationToken cancellationToken)
    {
        return await _updateService!.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ApplyCulture()
    {
        CultureInfo.CurrentCulture = _options.Culture;
        CultureInfo.CurrentUICulture = _options.Culture;
    }

    private bool TryAcquireSingleInstance()
    {
        var id = string.IsNullOrWhiteSpace(_options.SingleInstanceId)
            ? $"Local\\{GetType().Assembly.GetName().Name ?? _options.ApplicationName}"
            : _options.SingleInstanceId;

        _singleInstanceMutex = new Mutex(initiallyOwned: true, id, out var createdNew);
        return createdNew;
    }

    private void CreateMessageWindow()
    {
        var module = NativeMethods.GetModuleHandle(null);
        var className = $"{GetType().FullName}.TrayWindow.{Environment.ProcessId}";

        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = module,
            lpszClassName = className
        };

        if (NativeMethods.RegisterClassEx(ref wc) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to register tray message window class.");
        }

        _windowHandle = NativeMethods.CreateWindowEx(
            0,
            className,
            _options.ApplicationName,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            module,
            0);

        if (_windowHandle == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create tray message window.");
        }
    }

    private void AddTrayIcon()
    {
        _iconHandle = GetTrayIconHandle();
        var data = CreateNotifyIconData();

        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data))
        {
            throw new Win32Exception("Unable to create tray icon.");
        }

        _trayIconAdded = true;
        data.uTimeoutOrVersion = NativeMethods.NOTIFYICON_VERSION_4;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_SETVERSION, ref data);
    }

    private NativeMethods.NOTIFYICONDATA CreateNotifyIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _windowHandle,
        uID = 1,
        uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
        uCallbackMessage = NativeMethods.WM_TRAYICON,
        hIcon = _iconHandle,
        szTip = Truncate(_options.Tooltip, 127),
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private void RemoveTrayIcon()
    {
        if (!_trayIconAdded || _windowHandle == 0)
            return;

        var data = CreateNotifyIconData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
        _trayIconAdded = false;
    }

    private int RunMessageLoop()
    {
        while (true)
        {
            var result = NativeMethods.GetMessage(out var message, 0, 0, 0);
            if (result == 0)
                return (int)message.wParam;

            if (result == -1)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Win32 message loop failed.");

            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }
    }

    private nint WindowProcedure(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_COMMAND:
                HandleCommand(wParam & 0xFFFF);
                return 0;

            case NativeMethods.WM_TRAYICON:
                HandleTrayMessage(unchecked((uint)(long)lParam));
                return 0;

            case NativeMethods.WM_DESTROY:
                NativeMethods.PostQuitMessage(0);
                return 0;

            default:
                return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    private void HandleTrayMessage(uint message)
    {
        switch (message)
        {
            case NativeMethods.WM_LBUTTONDBLCLK:
                OnOpen();
                break;
            case NativeMethods.WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
    }

    private void HandleCommand(nuint command)
    {
        switch (command)
        {
            case CommandOpen:
                OnOpen();
                break;
            case CommandSettings:
                OnSettings();
                break;
            case CommandExit:
                Exit();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
            return;

        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandOpen, GetOpenMenuText());
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandSettings, GetSettingsMenuText());
            NativeMethods.AppendMenu(menu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, CommandExit, GetExitMenuText());

            if (!NativeMethods.GetCursorPos(out var point))
                return;

            NativeMethods.SetForegroundWindow(_windowHandle);
            NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN,
                point.X,
                point.Y,
                0,
                _windowHandle,
                0);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        RemoveTrayIcon();
        if (_windowHandle != 0)
        {
            NativeMethods.DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
