using System;
using System.Threading;

namespace StartupSelector.Services
{
    /// <summary>Keeps one Startup Selector per user session; a second launch just brings the first to the front.</summary>
    public sealed class SingleInstance : IDisposable
    {
        private const string MutexName = @"Local\StartupSelector.SingleInstance";
        private const string ActivateEventName = @"Local\StartupSelector.Activate";

        private readonly Mutex _mutex;
        private readonly bool _ownsMutex;
        private EventWaitHandle? _activateEvent;
        private RegisteredWaitHandle? _registration;

        public SingleInstance()
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
        }

        public bool IsFirstInstance => _ownsMutex;

        /// <summary>Asks the already running instance to show its window.</summary>
        public static void SignalFirstInstance()
        {
            try
            {
                using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
                activate.Set();
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Could not signal the running instance: {ex.Message}");
            }
        }

        /// <summary>Invokes <paramref name="onActivate"/> (on a thread-pool thread) whenever another launch signals.</summary>
        public void ListenForActivation(Action onActivate)
        {
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        public void Dispose()
        {
            _registration?.Unregister(null);
            _activateEvent?.Dispose();
            if (_ownsMutex)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Released from a different thread than the one that acquired it; the OS frees it on exit.
                }
            }

            _mutex.Dispose();
        }
    }
}
