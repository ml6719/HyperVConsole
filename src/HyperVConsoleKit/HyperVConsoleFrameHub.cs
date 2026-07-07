using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVConsoleKit
{
    /// <summary>
    /// Fans out one console stream to multiple viewers.
    /// </summary>
    /// <remarks>
    /// The hub runs one producer stream from an <see cref="IHyperVConsoleSession"/> and gives each viewer
    /// a latest-frame queue. Slow viewers drop stale frames independently instead of blocking other viewers.
    /// </remarks>
    public sealed class HyperVConsoleFrameHub
    {
        private readonly IHyperVConsoleSession _session;
        private readonly ConsoleFrameStreamOptions _options;
        private readonly int? _maxViewers;
        private readonly object _lock = new object();
        private readonly List<FrameHubSubscriber> _subscribers = new List<FrameHubSubscriber>();

        /// <summary>
        /// Creates a frame hub with no explicit viewer limit.
        /// </summary>
        public HyperVConsoleFrameHub(IHyperVConsoleSession session, ConsoleFrameStreamOptions options)
            : this(session, options, null)
        {
        }

        /// <summary>
        /// Creates a frame hub with an optional maximum viewer count.
        /// </summary>
        public HyperVConsoleFrameHub(IHyperVConsoleSession session, ConsoleFrameStreamOptions options, int? maxViewers)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            _session = session;
            _options = options ?? new ConsoleFrameStreamOptions();
            _maxViewers = maxViewers;
        }

        /// <summary>
        /// Gets the number of viewers currently attached to this hub.
        /// </summary>
        public int ViewerCount
        {
            get
            {
                lock (_lock)
                {
                    return _subscribers.Count;
                }
            }
        }

        /// <summary>
        /// Runs the producer stream until cancellation, completion, or failure.
        /// </summary>
        public Task RunAsync(CancellationToken cancellationToken)
        {
            return RunCoreAsync(cancellationToken);
        }

        private async Task RunCoreAsync(CancellationToken cancellationToken)
        {
            Exception failure = null;
            try
            {
                await _session.StreamFramesAsync(_options, PublishAsync, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                FrameHubSubscriber[] subscribers;
                lock (_lock)
                {
                    subscribers = _subscribers.ToArray();
                }

                foreach (var subscriber in subscribers)
                {
                    subscriber.Complete(failure);
                }
            }
        }

        /// <summary>
        /// Adds a viewer callback that receives the latest available frames until cancellation or hub completion.
        /// </summary>
        public async Task AddViewerAsync(Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken)
        {
            if (onFrame == null)
            {
                throw new ArgumentNullException("onFrame");
            }

            var subscriber = new FrameHubSubscriber(onFrame);
            lock (_lock)
            {
                if (_maxViewers.HasValue && _subscribers.Count >= _maxViewers.Value)
                {
                    throw new HyperVConsoleException("The maximum number of console viewers has been reached.");
                }

                _subscribers.Add(subscriber);
            }

            try
            {
                await subscriber.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_lock)
                {
                    _subscribers.Remove(subscriber);
                }

                subscriber.Dispose();
            }
        }

        private Task PublishAsync(ConsoleFrame frame, CancellationToken cancellationToken)
        {
            FrameHubSubscriber[] subscribers;
            lock (_lock)
            {
                subscribers = _subscribers.ToArray();
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Publish(frame);
            }

            return Task.FromResult(0);
        }

        private sealed class FrameHubSubscriber : IDisposable
        {
            private readonly Func<ConsoleFrame, CancellationToken, Task> _onFrame;
            private readonly object _lock = new object();
            private readonly SemaphoreSlim _signal = new SemaphoreSlim(0, 1);
            private ConsoleFrame _latest;
            private bool _signalPending;
            private bool _disposed;
            private bool _completed;
            private Exception _failure;

            public FrameHubSubscriber(Func<ConsoleFrame, CancellationToken, Task> onFrame)
            {
                _onFrame = onFrame;
            }

            public void Publish(ConsoleFrame frame)
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _latest = frame;
                    if (!_signalPending)
                    {
                        _signalPending = true;
                        _signal.Release();
                    }
                }
            }

            public async Task RunAsync(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

                    ConsoleFrame frame;
                    Exception failure;
                    bool completed;
                    lock (_lock)
                    {
                        frame = _latest;
                        _latest = null;
                        _signalPending = false;
                        completed = _completed;
                        failure = _failure;
                    }

                    if (completed)
                    {
                        if (failure != null)
                        {
                            throw failure;
                        }

                        return;
                    }

                    if (frame != null)
                    {
                        await _onFrame(frame, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            public void Complete(Exception failure)
            {
                lock (_lock)
                {
                    if (_disposed || _completed)
                    {
                        return;
                    }

                    _completed = true;
                    _failure = failure;
                    if (!_signalPending)
                    {
                        _signalPending = true;
                        _signal.Release();
                    }
                }
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    _disposed = true;
                    _latest = null;
                }

                _signal.Dispose();
            }
        }
    }
}
