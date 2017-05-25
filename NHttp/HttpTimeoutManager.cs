using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NHttp
{
    internal class HttpTimeoutManager : IDisposable
    {
        private Thread _thread;
        private ManualResetEvent _closeEvent = new ManualResetEvent(false);

        public TimeoutQueue ReadQueue { get; private set; }
        public TimeoutQueue WriteQueue { get; private set; }

        public HttpTimeoutManager(HttpServer server)
        {
            if (server == null)
                /* YJ Park Change Begin
                throw new ArgumentNullException(nameof(server));
                 */
                throw new ArgumentNullException("server");
                /* YJ Park Change End */

            ReadQueue = new TimeoutQueue(server.ReadTimeout);
            WriteQueue = new TimeoutQueue(server.WriteTimeout);

            _thread = new Thread(ThreadProc);
            _thread.Start();
        }

        private void ThreadProc()
        {
            while (!_closeEvent.WaitOne(TimeSpan.FromSeconds(1)))
            {
                ProcessQueue(ReadQueue);
                ProcessQueue(WriteQueue);
            }
        }

        private void ProcessQueue(TimeoutQueue queue)
        {
            while (true)
            {
                var item = queue.DequeueExpired();
                if (item == null)
                    return;

                if (!item.AsyncResult.IsCompleted)
                {
                    try
                    {
                        item.Disposable.Dispose();
                    }
                    catch
                    {
                        // Ignore exceptions.
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_thread != null)
            {
                _closeEvent.Set();
                _thread.Join();
                _thread = null;
            }
            if (_closeEvent != null)
            {
                _closeEvent.Close();
                _closeEvent = null;
            }
        }

        public class TimeoutQueue
        {
            private readonly object _syncRoot = new object();
            private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
            private readonly long _timeout;
            private readonly Queue<TimeoutItem> _items = new Queue<TimeoutItem>();

            public TimeoutQueue(TimeSpan timeout)
            {
                _timeout = (long)(timeout.TotalSeconds * Stopwatch.Frequency);
            }

            public void Add(IAsyncResult asyncResult, IDisposable disposable)
            {
                if (asyncResult == null)
                    /* YJ Park Change Begin
                    throw new ArgumentNullException(nameof(asyncResult));
                     */
                    throw new ArgumentNullException("asyncResult");
                    /* YJ Park Change End */
                if (disposable == null)
                    /* YJ Park Change Begin
                    throw new ArgumentNullException(nameof(disposable));
                     */
                    throw new ArgumentNullException("disposable");
                    /* YJ Park Change End */

                lock (_syncRoot)
                {
                    _items.Enqueue(new TimeoutItem(_stopwatch.ElapsedTicks + _timeout, asyncResult, disposable));
                }
            }

            public TimeoutItem DequeueExpired()
            {
                lock (_syncRoot)
                {
                    if (_items.Count == 0)
                        return null;

                    var item = _items.Peek();
                    if (item.Expires < _stopwatch.ElapsedTicks)
                        return _items.Dequeue();

                    return null;
                }
            }
        }

        public class TimeoutItem
        {
            public long Expires { get; private set; }
            public IAsyncResult AsyncResult { get; private set; }
            public IDisposable Disposable { get; private set; }

            public TimeoutItem(long expires, IAsyncResult asyncResult, IDisposable disposable)
            {
                if (asyncResult == null)
                    /* YJ Park Change Begin
                    throw new ArgumentNullException(nameof(asyncResult));
                     */
                    throw new ArgumentNullException("asyncResult");
                    /* YJ Park Change End */

                Expires = expires;
                AsyncResult = asyncResult;
                Disposable = disposable;
            }
        }
    }
}
