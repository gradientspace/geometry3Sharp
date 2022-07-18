using System.Globalization;

namespace g3
{
    public static class ColorBuilder
    {
        private static Colorf DefaultColor = new Colorf(0f);

        public static Colorf ParseHtmlColor(string hexColor)
        {
            if (hexColor == null || hexColor.Length < 7 || !hexColor.StartsWith("#"))
            {
                return DefaultColor;
            }
            string hex = hexColor.Replace("#", string.Empty);
            NumberStyles h = NumberStyles.HexNumber;

            int r = int.Parse(hex.Substring(0, 2), h);
            int g = int.Parse(hex.Substring(2, 2), h);
            int b = int.Parse(hex.Substring(4, 2), h);
            int a = 255;

            if (hex.Length == 8)
            {
                a = int.Parse(hex.Substring(6, 2), h);
            }

            return new Colorf(r, g, b, a);
        }

        public static bool TryParseHtmlString(string hexColor, out Colorf color)
        {
            try
            {
                string hex = hexColor.Replace("#", string.Empty);
                NumberStyles h = NumberStyles.HexNumber;

                int r = int.Parse(hex.Substring(0, 2), h);
                int g = int.Parse(hex.Substring(2, 2), h);
                int b = int.Parse(hex.Substring(4, 2), h);
                int a = 255;

                if (hex.Length == 8)
                {
                    a = int.Parse(hex.Substring(6, 2), h);
                }

                color = new Colorf(r, g, b, a);
                return true;
            }
            catch (System.Exception)
            {
                color = DefaultColor;
                return false;
            }
        }
    }
}
