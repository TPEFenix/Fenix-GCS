namespace FenixGCSApi
{
    public enum ELogLevel { Debug,Normal, Warn, Error }
    public delegate void LogEvent(ELogLevel level, string msg);
    public interface ILogable
    {
        LogEvent OnLog { get; set; }
    }
    public static class ILogableExtensions
    {
        public static void DebugLog(this ILogable logable, string msg)
        {
            logable.OnLog?.Invoke(ELogLevel.Normal, msg);
        }
        public static void InfoLog(this ILogable logable, string msg)
        {
            logable.OnLog?.Invoke(ELogLevel.Normal, msg);
        }
        public static void WarnLog(this ILogable logable, string msg)
        {
            logable.OnLog?.Invoke(ELogLevel.Warn, msg);
        }
        public static void ErrorLog(this ILogable logable, string msg)
        {
            logable.OnLog?.Invoke(ELogLevel.Error, msg);
        }
    }
}
