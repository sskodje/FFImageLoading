using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FFImageLoading.Helpers
{
    public class MainThreadBatcher
    {
        private const int DELAY = 100;
        private readonly IMainThreadDispatcher _dispatcher;
        private readonly IMiniLogger _logger;
        private readonly Queue<Action> _queue;
        private readonly SemaphoreSlim _mutex;
        private Task _currentPoll;
        private bool _pollInProgress;

        private MainThreadBatcher(IMainThreadDispatcher dispatcher, IMiniLogger logger)
        {
            _queue = new Queue<Action>();
            _mutex = new SemaphoreSlim(1);
            _dispatcher = dispatcher;
            _logger = logger;
            _currentPoll = Task.FromResult<byte>(1);
        }

        private static object _mutexInstance = new object();
        private static MainThreadBatcher _instance;
        internal static void CreateInstance(IMainThreadDispatcher dispatcher, IMiniLogger logger)
        {
            if (_instance != null)
                return;

            lock (_mutexInstance)
            {
                if (_instance != null)
                    return;

                _instance = new MainThreadBatcher(dispatcher, logger);
            }
        }

        public static MainThreadBatcher Instance
        {
            get
            {
                return _instance;
            }
        }

        public async Task AddAsync(Action mainThreadAction)
        {
            await _mutex.WaitAsync().ConfigureAwait(false);
            try
            {
                _queue.Enqueue(mainThreadAction);

                if (!_pollInProgress)
                {
                    _currentPoll = PollAsync();
                }
            }
            finally
            {
                _mutex.Release();
            }

            await _currentPoll.ConfigureAwait(false);
        }

        private async Task PollAsync()
        {
            await _mutex.WaitAsync().ConfigureAwait(false);
            try
            {
                _pollInProgress = true;
            }
            finally
            {
                _mutex.Release();
            }

            await Task.Delay(DELAY).ConfigureAwait(false);

            List<Action> actions;
            await _mutex.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_queue.Count == 0)
                {
                    _pollInProgress = false;
                    return;
                }

                actions = new List<Action>(_queue.Count);
                while (_queue.Count > 0)
                {
                    actions.Add(_queue.Dequeue());
                }
            }
            finally
            {
                _mutex.Release();
            }

            try
            {
                await _dispatcher.PostAsync(() =>
                    {
                        foreach (var action in actions)
                        {
                            try
                            {
                                action();
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Error while batching an action to main thread.", ex);
                            }
                        }
                    }).ConfigureAwait(false);
            }
            finally
            {
                await _mutex.WaitAsync().ConfigureAwait(false);
                _pollInProgress = false;
                _mutex.Release();
            }

            _logger.Debug(string.Format("Batched {0} actions to main thread.", actions.Count));
        }
    }
}

