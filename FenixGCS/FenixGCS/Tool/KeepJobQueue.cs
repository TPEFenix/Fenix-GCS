using System;
using System.Collections.Concurrent;
using System.Threading;

namespace FenixGCSApi.Tool
{
    public class KeepJobQueue<T> : IDisposable
    {
        bool _stop;
        Action<T> Job;
        Thread _thread;
        ConcurrentQueue<T> _core;
        ManualResetEvent _jobLocker;

        public KeepJobQueue(Action<T> job)
        {
            _jobLocker = new ManualResetEvent(false);
            _stop = false;
            Job = job;
            _core = new ConcurrentQueue<T>();
            _thread = new Thread(WorkingThread);
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void WorkingThread()
        {
            while (!_stop)
            {
                if (_core.Count > 0)
                {
                    T item;
                    if (!_core.TryDequeue(out item))
                        continue;
                    Job(item);
                }
                else
                {
                    _jobLocker.WaitOne();
                    _jobLocker.Reset();
                }
            }
        }
        public void Dispose()
        {
            _stop = true;
            _jobLocker.Set();
        }
        public void Enqueue(T item)
        {
            _core.Enqueue(item);
            _jobLocker.Set();
        }
    }
}
