using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GMap.NET.Internals
{
    /// <summary>
    ///     etc functions...
    /// </summary>
    internal class Stuff
    {
        public static string EnumToString(Enum value)
        {
            var fi = value.GetType().GetField(value.ToString());
            var attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(
                    typeof(DescriptionAttribute),
                    false);

            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        // Nota: SetCursorPos se elimina ya que P/Invoke a user32.dll no está permitido en UWP.

        public static readonly Random Random = new Random();

        public static void Shuffle<T>(List<T> deck)
        {
            int n = deck.Count;

            for (int i = 0; i < n; ++i)
            {
                int r = i + Random.Next(n - i);
                var t = deck[r];
                deck[r] = deck[i];
                deck[i] = t;
            }
        }

        public static MemoryStream CopyStream(Stream inputStream, bool seekOriginBegin)
        {
            const int readSize = 32 * 1024;
            byte[] buffer = new byte[readSize];
            var ms = new MemoryStream();
            {
                int count;
                while ((count = inputStream.Read(buffer, 0, readSize)) > 0)
                {
                    ms.Write(buffer, 0, count);
                }
            }

            if (seekOriginBegin)
            {
                inputStream.Seek(0, SeekOrigin.Begin);
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static bool IsRunningOnVistaOrLater()
        {
            return true; // En UWP siempre corre en versiones modernas de Windows
        }

        public static bool IsRunningOnWin7OrLater()
        {
            return true;
        }

        public static void RemoveInvalidPathSymbols(ref string url)
        {
            char[] ilg = Path.GetInvalidFileNameChars();
            foreach (char c in ilg)
            {
                url = url.Replace(c, '_');
            }
        }

        #region -- encryption --

        static string EncryptString(string message, string passphrase)
        {
            byte[] results;

            using (var hashProvider = SHA1.Create())
            {
                byte[] tdesKey = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                Array.Resize(ref tdesKey, 16);

                using (var tdesAlgorithm = TripleDES.Create())
                {
                    tdesAlgorithm.Key = tdesKey;
                    tdesAlgorithm.Mode = CipherMode.ECB;
                    tdesAlgorithm.Padding = PaddingMode.PKCS7;

                    byte[] dataToEncrypt = Encoding.UTF8.GetBytes(message);

                    try
                    {
                        using (var encryptor = tdesAlgorithm.CreateEncryptor())
                        {
                            results = encryptor.TransformFinalBlock(dataToEncrypt, 0, dataToEncrypt.Length);
                        }
                    }
                    finally
                    {
                    }
                }
            }

            return Convert.ToBase64String(results);
        }

        static string DecryptString(string message, string passphrase)
        {
            byte[] results;

            using (var hashProvider = SHA1.Create())
            {
                byte[] tdesKey = hashProvider.ComputeHash(Encoding.UTF8.GetBytes(passphrase));
                Array.Resize(ref tdesKey, 16);

                using (var tdesAlgorithm = TripleDES.Create())
                {
                    tdesAlgorithm.Key = tdesKey;
                    tdesAlgorithm.Mode = CipherMode.ECB;
                    tdesAlgorithm.Padding = PaddingMode.PKCS7;

                    byte[] dataToDecrypt = Convert.FromBase64String(message);

                    try
                    {
                        using (var decryptor = tdesAlgorithm.CreateDecryptor())
                        {
                            results = decryptor.TransformFinalBlock(dataToDecrypt, 0, dataToDecrypt.Length);
                        }
                    }
                    finally
                    {
                    }
                }
            }

            return Encoding.UTF8.GetString(results, 0, results.Length);
        }

        public static string EncryptString(string message)
        {
            return EncryptString(message, manifesto);
        }

        public static string GString(string message)
        {
            string ret = DecryptString(message, manifesto);

            return ret;
        }

        static readonly string manifesto =
            "GMap.NET is great and Powerful, Free, cross platform, open source .NET control.";

        #endregion
    }
}