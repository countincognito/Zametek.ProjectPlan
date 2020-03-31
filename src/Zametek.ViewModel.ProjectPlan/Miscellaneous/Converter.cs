using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class Converter
    {
        #region Hex Colors

        public static string HexConverter(byte r, byte g, byte b)
        {
            return $@"#{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        public static string HexConverter(byte a, byte r, byte g, byte b)
        {
            return $@"#{a.ByteToHexString()}{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        private static string ByteToHexString(this byte input)
        {
            return input.ToString(@"X2", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
