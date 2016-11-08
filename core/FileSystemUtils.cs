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
            return Path.GetInvalidFileNameChars().Contains(c) == false;
        }
        static public bool IsValidFilenameString(string s)
        {
            for (int i = 0; i < s.Length; ++i)
                if (Path.GetInvalidFileNameChars().Contains(s[i]))
                    return false;
            return true;
        }

    }
}
