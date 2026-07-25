using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;
using DigitalVibrance.Interop;

namespace DigitalVibrance.Services;

/// <summary>
/// Reports which executable currently owns the foreground window and which are running at all.
///
/// Foreground changes arrive through a WinEvent hook so switching into a game is instant; a
/// slower timer refreshes the running-process set and covers the rare case where the hook
/// misses an event. Must be constructed on a thread with a message pump (the UI thread).
/// </summary>
public sealed class GameWatcher : IDisposable
{
    private readonly NativeMethods.WinEventProc _callback; // kept alive for the hook's lifetime
    private readonly IntPtr _hook;
    private readonly DispatcherTimer _timer;

    private HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private string? _foreground;
    private bool _disposed;

    public GameWatcher()
    {
        _callback = OnForegroundChanged;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _callback, 0, 0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1500),
        };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    /// <summary>Raised when the foreground executable or the running set changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Lower-cased file name of the foreground process, e.g. "cs2.exe".</summary>
    public string? ForegroundExecutable => _foreground;

    public bool IsRunning(string executableName) =>
        !string.IsNullOrEmpty(executableName) && _running.Contains(executableName);

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero) return;
        if (UpdateForeground()) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        bool changed = UpdateForeground();

        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try { running.Add(process.ProcessName + ".exe"); }
                catch { /* process died mid-enumeration */ }
                finally { process.Dispose(); }
            }
        }
        catch
        {
            return; // keep the previous snapshot rather than reporting nothing is running
        }

        if (!running.SetEquals(_running))
        {
            _running = running;
            changed = true;
        }

        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool UpdateForeground()
    {
        string? name = ResolveForegroundExecutable();
        if (string.Equals(name, _foreground, StringComparison.OrdinalIgnoreCase)) return false;
        _foreground = name;
        return true;
    }

    private static string? ResolveForegroundExecutable()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        // PROCESS_QUERY_LIMITED_INFORMATION is granted across integrity levels, so this still
        // resolves games running elevated under an anti-cheat without elevating ourselves.
        IntPtr handle = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);

        if (handle != IntPtr.Zero)
        {
            try
            {
                var buffer = new StringBuilder(1024);
                uint size = (uint)buffer.Capacity;
                if (NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size))
                    return Path.GetFileName(buffer.ToString(0, (int)size)).ToLowerInvariant();
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        // Fall back to the name only; it needs no handle and is all the matcher uses anyway.
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return (process.ProcessName + ".exe").ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        if (_hook != IntPtr.Zero) NativeMethods.UnhookWinEvent(_hook);
    }
}
