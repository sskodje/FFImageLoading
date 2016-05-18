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
        private readonly List<Action> _queue;
        private readonly object _mutex;

        private MainThreadBatcher(IMainThreadDispatcher dispatcher, IMiniLogger logger)
        {
            _queue = new List<Action>();
            _mutex = new object();
            _dispatcher = dispatcher;
            _logger = logger;
            Initialize();
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

        public void Add(Action mainThreadAction)
        {
            lock (_mutex)
            {
                _queue.Add(mainThreadAction);
            }
        }

        private async void Initialize()
        {
            await PollAsync().ConfigureAwait(false);
        }

        private async Task PollAsync()
        {
            await Task.Delay(100).ConfigureAwait(false);
            List<Action> actions;
            lock (_mutex)
            {
                actions = _queue.ToList();
                _queue.Clear();
            }

            if (actions.Count > 0)
            {
                await _dispatcher.PostAsync(() =>
                {
                    foreach (var action in actions)
                    {
                        action();
                    }
                }).ConfigureAwait(false);

                _logger.Debug(string.Format("Batched {0} actions to main thread.", actions.Count));
            }

            await PollAsync().ConfigureAwait(false);
        }
    }
}

