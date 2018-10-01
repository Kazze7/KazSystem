using System.Text;
using System.Security.Cryptography;

namespace KazNet
{
    public static class Functions
    {        
        public static string MD5ToString(string _string)
        {
            byte[] MD5Data = new MD5CryptoServiceProvider().ComputeHash(Encoding.ASCII.GetBytes(_string));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < MD5Data.Length; i++)
            {
                sb.Append(MD5Data[i].ToString("X2"));
            }
            return sb.ToString();
        }
        public static string UppercaseFirst(string _string)
        {
            _string = _string.ToLower();
            char[] str = _string.ToCharArray();
            str[0] = char.ToUpper(str[0]);
            return new string(str);
        }
    }
}
