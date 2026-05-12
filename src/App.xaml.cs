using System.Windows;
using Application = System.Windows.Application;

namespace TimeTracker;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon _trayIcon = null!;
    private MainWindow? _mainWindow;
    private bool _isExiting;

    public bool IsExiting => _isExiting;
    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "TimeTracker"
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;

        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApp()
    {
        _isExiting = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
