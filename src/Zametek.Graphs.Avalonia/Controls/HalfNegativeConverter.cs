using Avalonia;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Zametek.Graphs.Avalonia
{
    // Returns minus half of a numeric value. Used to centre a Canvas-positioned control on its
    // anchor point: bound to the control's own Bounds.Width / Bounds.Height through a
    // TranslateTransform, it shifts the control left/up by half its size so the anchor lands at
    // its centre rather than its top-left corner.
    public class HalfNegativeConverter
        : IValueConverter
    {
        #region IValueConverter Members

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double size)
            {
                return -size / 2.0;
            }

            return AvaloniaProperty.UnsetValue;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
