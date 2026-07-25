using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using DigitalVibrance.Localization;
using DigitalVibrance.ViewModels;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace DigitalVibrance.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly WinForms.NotifyIcon _tray;
    private readonly WinForms.ToolStripMenuItem _trayShowItem;
    private readonly WinForms.ToolStripMenuItem _trayExitItem;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        _trayShowItem = new WinForms.ToolStripMenuItem();
        _trayExitItem = new WinForms.ToolStripMenuItem();
        _tray = BuildTrayIcon();

        // The tray menu lives outside WPF's binding system, so it is refreshed by hand.
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        ApplyTrayText();

        if (!_vm.EngineAvailable)
        {
            Loaded += (_, _) => MessageBox.Show(this,
                Loc.Instance["EngineFailedBody"] + "\n\n" + _vm.EngineError,
                "Digital Vibrance", MessageBoxButton.OK, MessageBoxImage.Warning,
                MessageBoxResult.OK, Loc.Instance.DialogOptions);
        }
    }

    // ---------- tray ----------

    private WinForms.NotifyIcon BuildTrayIcon()
    {
        _trayShowItem.Click += (_, _) => RestoreFromTray();
        _trayExitItem.Click += (_, _) => ExitApplication();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(_trayShowItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_trayExitItem);

        var icon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "Digital Vibrance",
            Visible = true,
            ContextMenuStrip = menu,
        };
        icon.DoubleClick += (_, _) => RestoreFromTray();
        return icon;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyTrayText();

    private void ApplyTrayText()
    {
        _trayShowItem.Text = Loc.Instance["TrayShow"];
        _trayExitItem.Text = Loc.Instance["TrayExit"];
    }

    private static Drawing.Icon LoadAppIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var extracted = Drawing.Icon.ExtractAssociatedIcon(exe);
                if (extracted is not null) return extracted;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            // fall through to the stock icon
        }

        return Drawing.SystemIcons.Application;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exiting = true;
        Close();
    }

    // ---------- window chrome ----------

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // E922 = maximize glyph, E923 = restore glyph
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _vm.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _tray.Visible = false;
        _tray.Dispose();
        _vm.Dispose(); // saves settings and resets the screen to neutral
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    // ---------- drag & drop ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedExecutables(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        foreach (string path in GetDroppedExecutables(e))
            _vm.AddGameFromPath(path);

        e.Handled = true;
    }

    private static string[] GetDroppedExecutables(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return Array.Empty<string>();
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return Array.Empty<string>();

        return paths
            .Where(p => string.Equals(Path.GetExtension(p), ".exe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
