using System.Text;

namespace FenixGCSApi
{
    public class ServerEncoding
    {
        public static Encoding Encoding = Encoding.UTF8;
        public static string GetString(byte[] bytes)
        {
            return Encoding.GetString(bytes);
        }
        public static byte[] GetBytes(string str)
        {
            return Encoding.GetBytes(str);
        }
    }
}
