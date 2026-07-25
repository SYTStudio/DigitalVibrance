using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DigitalVibrance;

public partial class App : Application
{
    // Two instances would fight over the one system-wide colour effect, so only one may run.
    private const string InstanceMutexName = @"Local\DigitalVibrance.SingleInstance";

    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isFirst);
        if (!isFirst)
        {
            // Runs before any window exists, so the language comes straight from the saved config.
            Localization.Loc.Instance.Use(
                Services.ConfigStore.Load().Language ?? Localization.Loc.Instance.DetectSystemCode());

            MessageBox.Show(
                Localization.Loc.Instance["AlreadyRunningBody"],
                "Digital Vibrance", MessageBoxButton.OK, MessageBoxImage.Information,
                MessageBoxResult.OK, Localization.Loc.Instance.DialogOptions);
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // Created by hand rather than via StartupUri so the single-instance check above can
        // bail out before any window exists.
        var window = new Views.MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"{Localization.Loc.Instance["CrashBody"]}\n\n{e.Exception.Message}",
            "Digital Vibrance", MessageBoxButton.OK, MessageBoxImage.Error,
            MessageBoxResult.OK, Localization.Loc.Instance.DialogOptions);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _instanceMutex?.ReleaseMutex();
            _instanceMutex?.Dispose();
        }
        catch (ApplicationException)
        {
            // mutex was never owned by this thread; nothing to release
        }

        base.OnExit(e);
    }
}
