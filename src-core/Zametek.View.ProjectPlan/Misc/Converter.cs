namespace Zametek.View.ProjectPlan
{
    public static class Converter
    {
        #region Hex Colors

        public static string HexConverter(byte r, byte g, byte b)
        {
            return $@"#{r.ToString("X2")}{g.ToString("X2")}{b.ToString("X2")}";
        }

        public static string HexConverter(byte a, byte r, byte g, byte b)
        {
            return $@"#{a.ToString("X2")}{r.ToString("X2")}{g.ToString("X2")}{b.ToString("X2")}";
        }

        #endregion
    }
}
