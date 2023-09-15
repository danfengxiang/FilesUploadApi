using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace FilesUploadApi
{
    public static class Md5Util
    {
        private static readonly MD5 md5 = MD5.Create();

        public static string GetStringMd5(string text)
        {
            if(string.IsNullOrEmpty(text))
            {
                return Guid.NewGuid().ToString().ToUpper().Replace("-", "");
            }
            byte[] buf = Encoding.UTF8.GetBytes(text);
            var hBytes = md5.ComputeHash(buf);
            return BitConverter.ToString(hBytes).Replace("-", "").ToUpper();
        }
    }
}
