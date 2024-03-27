using FenixGCSApi.Tool;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FenixGCSApi.ByteFormatter
{
    public class FGCSByteFormatter : IDisposable
    {
        private const byte SOH = 0x01; // Start of Heading
        private const byte STX = 0x02; // Start of Text
        private const byte ETX = 0x03; // End of Text

        private ConcurrentQueue<byte[]> _output = new ConcurrentQueue<byte[]>();
        private List<byte> _buffer = new List<byte>();
        private object receiveLock = new object();
        private KeepJobQueue<byte[]> _insertJobs;
        private ManualResetEvent recvAvailable = new ManualResetEvent(false);
        private int MaxPackSize = 10485760;//10MByte

        public FGCSByteFormatter(int maxPackSize = 10485760)
        {
            _insertJobs = new KeepJobQueue<byte[]>(ProcessOutput);
            MaxPackSize = maxPackSize;
        }

        /// <summary>
        /// 產生FGCSByte穩定傳輸格式
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static byte[] GenerateSendArray(byte[] source)
        {
            string lengthStr = source.Length.ToString();
            byte[] lengthBytes = new byte[lengthStr.Length];
            for (int i = 0; i < lengthStr.Length; i++)
                lengthBytes[i] = (byte)lengthStr[i];

            // 計算總長度
            int totalSize = 1 + lengthBytes.Length + 1 + source.Length + 1; // SOH + 长度的ASCII表示 + STX + 数据 + ETX
            byte[] result = new byte[totalSize];

            int pos = 0;
            result[pos++] = SOH; // 添加SOH

            //填充長度
            foreach (byte b in lengthBytes)
                result[pos++] = b;

            result[pos++] = STX;//正文

            //插入原始內容
            Array.Copy(source, 0, result, pos, source.Length);
            pos += source.Length;

            result[pos] = ETX; //結束

            return result;
        }
        /// <summary>
        /// 把接收到的資訊放入Buffer區使其解碼
        /// </summary>
        /// <param name="sourceData"></param>
        public void InsertSourceData(byte[] sourceData)
        {
            _insertJobs.Enqueue(sourceData);
        }
        /// <summary>
        /// 獲取一筆整理後的資料
        /// </summary>
        /// <returns></returns>
        public byte[] Receive()
        {
            while (true)
            {
                lock (receiveLock)
                {
                    if (_output.TryDequeue(out byte[] result))
                        return result;
                }
                recvAvailable.WaitOne();
                recvAvailable.Reset();
            }
        }

        #region Private
        private void ProcessOutput(byte[] sourceData)
        {
            _buffer.AddRange(sourceData);
            while (true)
            {
                //找到SOH
                int SOHIndex = _buffer.IndexOf(SOH);
                if (SOHIndex < 0)//找不到代表這一輪不可能有合理資料，退出
                {
                    _buffer.Clear();
                    return;
                }
                _buffer.RemoveRange(0, SOHIndex);

                int workingIndex = 1;
                int STXIndex = -1;//>0=有找到 -1=沒找到 -2=解析錯誤 (workingIndex == 1 && temp == STX)的情況會被視為錯誤
                int PredictETXPosition = -1;
                int MaxSTXPosition = CountDigits(MaxPackSize) + 1;
                //找STX
                while (workingIndex < _buffer.Count)
                {
                    byte temp = _buffer[workingIndex];
                    if ((!IsDigit(temp) && temp != STX) || (workingIndex > MaxSTXPosition))
                    {
                        _buffer.RemoveAt(0);//移除標頭(即SOH)
                        STXIndex = -2;
                        break;
                    }
                    if (temp == STX)
                    {
                        byte[] extractedData = GetBytes(_buffer, 0, workingIndex);
                        if (extractedData == null || extractedData.Length <= 0)
                        {
                            _buffer.RemoveAt(0);//移除標頭(即SOH)
                            STXIndex = -2;
                            break;
                        }
                        int dataLength = ParseLength(extractedData);//原始資料長度
                        if (dataLength > MaxPackSize) //超過允許的資料長度，遺棄資料
                        {
                            _buffer.RemoveAt(0);//移除標頭(即SOH)
                            STXIndex = -2;
                            break;
                        }
                        STXIndex = workingIndex;
                        PredictETXPosition = STXIndex + dataLength + 1;
                        break;
                    }
                    workingIndex++;
                }

                if (STXIndex == -2)//代表解析錯誤，從頭來
                    continue;
                if (STXIndex == -1)//代表SOH長度還沒補完，等下一次
                    return;

                //找ETX
                if (_buffer.Count >= PredictETXPosition + 1)//表示資料足夠
                {
                    if (_buffer[PredictETXPosition] == ETX)//代表資料完整，輸出
                    {
                        lock (receiveLock)
                        {
                            byte[] extractedData = GetBytes(_buffer, STXIndex, PredictETXPosition);
                            _output.Enqueue(extractedData);
                            _buffer.RemoveRange(0, PredictETXPosition + 1);
                            recvAvailable.Set();
                        }
                        continue;
                    }
                    else //代表資料錯誤，回朔到SOH
                    {
                        // 删除SOH
                        _buffer.RemoveRange(0, 1);
                        continue;
                    }
                }
                else//代表資料不足，等下一次
                    return;

            }
        }
        private int ParseLength(byte[] lengthBytes)
        {
            int length = 0;
            foreach (byte b in lengthBytes)
                length = length * 10 + (b - '0');
            return length;
        }
        private byte[] GetBytes(List<byte> data, int start, int end)
        {
            //取得中間的Bytes，不含start跟end的Poisition
            if (start < 0 || end <= start || end >= data.Count)
                return null;
            int length = end - start - 1;
            byte[] extractedData = new byte[length];
            if (length > 0)
                data.CopyTo(start + 1, extractedData, 0, length);
            return extractedData;
        }
        private bool IsDigit(byte b)
        {
            return b >= 48 && b <= 57;  // ASCII values for '0' to '9' are 48 to 57
        }
        private int CountDigits(int number)
        {
            // 處理數字為0的特殊情況
            if (number == 0) return 1;

            // 確保數字為正數
            number = Math.Abs(number);

            int digits = 0;
            while (number > 0)
            {
                number /= 10; // 去掉數字的最後一位
                digits++; // 增加位數計數
            }

            return digits;
        }
        public void Dispose()
        {

            try
            {
                recvAvailable.Set();
                recvAvailable.Dispose();
                _insertJobs.Dispose();
            }
            catch (AggregateException)
            {

            }
        }
        #endregion
    }
}
