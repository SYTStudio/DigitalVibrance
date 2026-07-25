using System;
using System.Runtime.InteropServices;
using System.Threading;
using DigitalVibrance.Core;
using DigitalVibrance.Interop;

namespace DigitalVibrance.Services;

/// <summary>
/// Owns the desktop colour transform.
///
/// The Magnification runtime objects are per-thread, so everything runs on one dedicated
/// thread that lives for the whole process. Requests are handed over as a target matrix and
/// the worker eases towards it, which stops slider drags from looking like a strobe light.
///
/// On exit the matrix is reset to identity. If the process is killed instead, Windows tears
/// down the magnification objects with it and the screen returns to normal anyway.
/// </summary>
public sealed class VibranceEngine : IDisposable
{
    private const int TickMs = 16;

    private readonly Thread _thread;
    private readonly AutoResetEvent _signal = new(false);
    private readonly ManualResetEventSlim _started = new(false);
    private readonly object _gate = new();

    private ColorMatrix _from = ColorMatrix.Identity;
    private ColorMatrix _to = ColorMatrix.Identity;
    private ColorMatrix _current = ColorMatrix.Identity;
    private ColorMatrix _lastPushed = ColorMatrix.Identity;
    private long _startTick;
    private int _durationMs;

    private volatile bool _running = true;
    private volatile bool _available;
    private string? _error;

    public VibranceEngine()
    {
        _thread = new Thread(Worker)
        {
            IsBackground = true,
            Name = "DigitalVibrance.Engine",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _started.Wait(3000);
    }

    /// <summary>False when Magnification.dll refused to initialise; see <see cref="Error"/>.</summary>
    public bool IsAvailable => _available;

    public string? Error => _error;

    /// <summary>Eases the screen towards <paramref name="matrix"/>. Safe from any thread.</summary>
    public void Apply(ColorMatrix matrix, int fadeMs = 200)
    {
        lock (_gate)
        {
            if (_to.Equals(matrix)) return;
            _from = _current;
            _to = matrix;
            _startTick = Environment.TickCount64;
            _durationMs = Math.Max(0, fadeMs);
        }
        _signal.Set();
    }

    private void Worker()
    {
        try
        {
            _available = NativeMethods.MagInitialize();
            if (!_available)
                _error = $"MagInitialize failed (Win32 error {Marshal.GetLastWin32Error()})";
        }
        catch (DllNotFoundException)
        {
            _available = false;
            _error = "Magnification.dll not found — Windows 8 or newer is required.";
        }
        catch (Exception ex)
        {
            _available = false;
            _error = ex.Message;
        }
        finally
        {
            _started.Set();
        }

        if (!_available) return;

        try
        {
            while (_running)
            {
                ColorMatrix next;
                bool animating;

                lock (_gate)
                {
                    long elapsed = Environment.TickCount64 - _startTick;
                    float t = _durationMs <= 0 ? 1f : Math.Clamp(elapsed / (float)_durationMs, 0f, 1f);
                    next = t >= 1f ? _to : ColorMatrix.Lerp(_from, _to, EaseOut(t));
                    _current = next;
                    animating = t < 1f;
                }

                Push(next);
                _signal.WaitOne(animating ? TickMs : Timeout.Infinite);
            }
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            TryReset();
        }
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    private void Push(ColorMatrix matrix)
    {
        if (matrix.Equals(_lastPushed)) return;

        var effect = new NativeMethods.MagColorEffect { Transform = matrix.Values };
        if (NativeMethods.MagSetFullscreenColorEffect(ref effect))
            _lastPushed = matrix;
    }

    private void TryReset()
    {
        try
        {
            var identity = new NativeMethods.MagColorEffect { Transform = ColorMatrix.Identity.Values };
            NativeMethods.MagSetFullscreenColorEffect(ref identity);
            NativeMethods.MagUninitialize();
        }
        catch
        {
            // Nothing useful to do while tearing down; the OS resets the effect on process exit.
        }
    }

    public void Dispose()
    {
        if (!_running) return;
        _running = false;
        _signal.Set();
        _thread.Join(2000);
        _signal.Dispose();
        _started.Dispose();
    }
}
