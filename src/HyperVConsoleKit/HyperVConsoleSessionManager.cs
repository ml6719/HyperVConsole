using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVConsoleKit
{
    /// <summary>
    /// Manages shared per-VM console streams for agent and web-gateway scenarios.
    /// </summary>
    /// <remarks>
    /// The manager creates one frame hub per VM, reference-counts viewers, and disposes the
    /// underlying console session when the last viewer disconnects.
    /// </remarks>
    public sealed class HyperVConsoleSessionManager : IDisposable
    {
        private readonly HyperVConsoleClient _client;
        private readonly HyperVConsolePolicy _policy;
        private readonly object _lock = new object();
        private readonly Dictionary<Guid, ManagedConsoleSession> _sessions = new Dictionary<Guid, ManagedConsoleSession>();
        private bool _disposed;

        /// <summary>
        /// Creates a session manager using the client's default policy.
        /// </summary>
        public HyperVConsoleSessionManager(HyperVConsoleClient client)
            : this(client, null)
        {
        }

        /// <summary>
        /// Creates a session manager with an explicit policy applied to managed sessions.
        /// </summary>
        public HyperVConsoleSessionManager(HyperVConsoleClient client, HyperVConsolePolicy policy)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            _client = client;
            _policy = policy ?? new HyperVConsolePolicy();
        }

        /// <summary>
        /// Gets the number of VM console streams currently managed.
        /// </summary>
        public int ActiveSessionCount
        {
            get
            {
                lock (_lock)
                {
                    return _sessions.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of active viewers for a VM.
        /// </summary>
        public int GetViewerCount(Guid virtualMachineId)
        {
            lock (_lock)
            {
                ManagedConsoleSession managed;
                return _sessions.TryGetValue(virtualMachineId, out managed) ? managed.ViewerCount : 0;
            }
        }

        /// <summary>
        /// Adds a viewer to the shared stream for a VM, creating the stream if needed.
        /// </summary>
        public async Task AddViewerAsync(Guid virtualMachineId, ConsoleFrameStreamOptions options, Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (onFrame == null)
            {
                throw new ArgumentNullException("onFrame");
            }

            var managed = GetOrCreate(virtualMachineId, options);
            managed.AddViewer();
            try
            {
                var viewerTask = managed.Hub.AddViewerAsync(onFrame, cancellationToken);
                managed.EnsureStarted();
                await viewerTask.ConfigureAwait(false);
            }
            finally
            {
                if (managed.RemoveViewer() == 0)
                {
                    RemoveIfCurrent(virtualMachineId, managed);
                }
            }
        }

        /// <summary>
        /// Stops and removes the managed stream for a VM, if one exists.
        /// </summary>
        public void Stop(Guid virtualMachineId)
        {
            ManagedConsoleSession managed = null;
            lock (_lock)
            {
                if (_sessions.TryGetValue(virtualMachineId, out managed))
                {
                    _sessions.Remove(virtualMachineId);
                }
            }

            if (managed != null)
            {
                managed.Dispose();
            }
        }

        /// <summary>
        /// Stops all managed streams and releases their console sessions.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ManagedConsoleSession[] sessions;
            lock (_lock)
            {
                sessions = new ManagedConsoleSession[_sessions.Count];
                _sessions.Values.CopyTo(sessions, 0);
                _sessions.Clear();
            }

            foreach (var session in sessions)
            {
                session.Dispose();
            }
        }

        private ManagedConsoleSession GetOrCreate(Guid virtualMachineId, ConsoleFrameStreamOptions options)
        {
            lock (_lock)
            {
                ManagedConsoleSession managed;
                if (_sessions.TryGetValue(virtualMachineId, out managed))
                {
                    return managed;
                }

                var streamOptions = options ?? new ConsoleFrameStreamOptions();
                _policy.ApplyTo(streamOptions);
                var session = _client.OpenConsole(virtualMachineId, new HyperVConsoleOpenOptions
                {
                    Mode = HyperVConsoleMode.RawHostConsole,
                    Policy = _policy
                });
                var hub = new HyperVConsoleFrameHub(session, streamOptions, _policy.MaxConcurrentViewers);
                managed = new ManagedConsoleSession(session, hub);
                _sessions[virtualMachineId] = managed;
                return managed;
            }
        }

        private void RemoveIfCurrent(Guid virtualMachineId, ManagedConsoleSession managed)
        {
            lock (_lock)
            {
                ManagedConsoleSession current;
                if (_sessions.TryGetValue(virtualMachineId, out current) && ReferenceEquals(current, managed))
                {
                    _sessions.Remove(virtualMachineId);
                }
            }

            managed.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("HyperVConsoleSessionManager");
            }
        }

        private sealed class ManagedConsoleSession : IDisposable
        {
            private readonly IHyperVConsoleSession _session;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            private readonly object _lock = new object();
            private int _viewerCount;
            private bool _disposed;

            public ManagedConsoleSession(IHyperVConsoleSession session, HyperVConsoleFrameHub hub)
            {
                _session = session;
                Hub = hub;
            }

            public HyperVConsoleFrameHub Hub { get; private set; }
            public Task RunTask { get; private set; }

            public void EnsureStarted()
            {
                lock (_lock)
                {
                    if (RunTask == null)
                    {
                        RunTask = Task.Run(() => Hub.RunAsync(_cts.Token));
                    }
                }
            }

            public int ViewerCount
            {
                get
                {
                    lock (_lock)
                    {
                        return _viewerCount;
                    }
                }
            }

            public void AddViewer()
            {
                lock (_lock)
                {
                    _viewerCount++;
                }
            }

            public int RemoveViewer()
            {
                lock (_lock)
                {
                    if (_viewerCount > 0)
                    {
                        _viewerCount--;
                    }

                    return _viewerCount;
                }
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                }

                _cts.Cancel();
                try
                {
                    if (RunTask != null)
                    {
                        RunTask.Wait(TimeSpan.FromSeconds(5));
                    }
                }
                catch (AggregateException)
                {
                }

                _cts.Dispose();
                _session.Dispose();
            }
        }
    }
}
