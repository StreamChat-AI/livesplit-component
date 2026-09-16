using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StreamChatAI.LiveSplit
{
    /// <summary>
    /// Where the connection token lives.
    ///
    /// ⚠️ Deliberately NOT in the layout file. Runners share .lsl layouts with
    /// each other all the time, and a token in there would hand whoever
    /// downloads it the ability to post to the original runner's chat. It is
    /// kept per Windows user instead, encrypted with DPAPI so it only opens
    /// for the account that saved it.
    /// </summary>
    public static class TokenStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StreamChatAI.LiveSplit.v1");

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StreamChatAI", "livesplit-connection.bin");

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }
                var bytes = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                var token = Encoding.UTF8.GetString(bytes);
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
            catch (Exception)
            {
                // Unreadable is the same as absent: the runner connects again.
                return null;
            }
        }

        public static void Save(string token)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, bytes);
        }

        public static void Clear()
        {
            try
            {
                File.Delete(FilePath);
            }
            catch (Exception)
            {
            }
        }
    }
}
