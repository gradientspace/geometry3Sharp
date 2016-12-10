using System;
using System.Linq;
using System.IO;

namespace g3
{
    public static class FileSystemUtils
    {
        static public bool CanAccessFolder(string sPath)
        {
            try {
                Directory.GetDirectories(sPath);
            } catch (Exception) {
                return false;
            }
            return true;
        }


        static public bool IsValidFilenameCharacter(char c)
        {
            return Path.GetInvalidPathChars().Contains(c) == false;
        }
        static public bool IsValidFilenameString(string s)
        {
            for (int i = 0; i < s.Length; ++i)
                if (Path.GetInvalidPathChars().Contains(s[i]))
                    return false;
            return true;
        }


        static public bool IsWebURL(string s)
        {
            Uri uriResult;
            bool bResult = Uri.TryCreate(s, UriKind.Absolute, out uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            return bResult;
        }

        static public bool IsFullFilesystemPath(string s)
        {
            return Path.IsPathRooted(s);
        }


        public static string GetTempFilePathWithExtension(string extension)
        {
            var path = Path.GetTempPath();
            var fileName = Guid.NewGuid().ToString() + extension;
            return Path.Combine(path, fileName);
        }

    }
}
