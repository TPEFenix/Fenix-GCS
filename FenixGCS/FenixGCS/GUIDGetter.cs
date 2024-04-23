using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FenixGCSApi
{
    /// <summary>
    /// 預先建立好存著用的GUID產生器，速度略快直接取Guid5%
    /// </summary>
    public static class GUIDGetter
    {
        private static ConcurrentQueue<string> _guids = new ConcurrentQueue<string>();
        private static Thread _guidGenerateThread;
        private static ManualResetEvent ManualResetEvent = new ManualResetEvent(false);
        static GUIDGetter()
        {
            _guidGenerateThread = new Thread(() =>
            {
                while (true)
                {
                    if (_guids.Count < 5000)
                    {
                        _guids.Enqueue(Guid.NewGuid().ToString());
                    }
                    else
                    {
                        ManualResetEvent.WaitOne();
                        ManualResetEvent.Reset();
                    }
                }
            });
            _guidGenerateThread.IsBackground = true;
            _guidGenerateThread.Start();
        }
        public static string Get()
        {
            if (_guids.TryDequeue(out string result))
            {
                ManualResetEvent.Set();
                return result;
            }
            else
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
