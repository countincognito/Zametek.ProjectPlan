using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ClosedXML.Graphics;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The graphic engine that the library of the workbooks is given. The library measures text and pictures with an engine
    /// of its own that needs the fonts of the system (it takes one that it has not got from the system when a font is not
    /// there, and fails when that one is not there either, as it is not in a container, or in the browser). The workbooks of
    /// the application are never fitted to their text, and no picture of them is drawn, so the engine here measures nothing
    /// and gives the measures of a cell of the default font of Excel (Calibri 11 point, at 96 dots to the inch) instead.
    /// </summary>
    /// <remarks>
    /// The library asks it only once, when it reads a workbook, for the width of a digit, to turn the widths of the columns
    /// of the file into the unit that it uses. Nothing that the importer reads depends on the answer.
    /// </remarks>
    internal sealed class XlsxGraphicEngine : IXLGraphicEngine
    {
        private const double c_DotsToTheInch = 96.0;
        private const double c_DigitWidth = 7.0;
        private const double c_TextHeight = 15.0;
        private const double c_Descent = 3.0;

        private XlsxGraphicEngine()
        {
        }

        public static XlsxGraphicEngine Instance { get; } = new();

        /// <summary>
        /// The options to make or read a workbook with. They are made for each use, because the library may change them.
        /// </summary>
        public static LoadOptions CreateLoadOptions() => new() { GraphicEngine = Instance };

        // A picture in a workbook is not drawn, and so it is a picture of one dot, in the format that was said (a picture of
        // a format that was not said is taken for a PNG).
        public XLPictureInfo GetPictureInfo(Stream imageStream, XLPictureFormat expectedFormat) =>
            new(expectedFormat == XLPictureFormat.Unknown ? XLPictureFormat.Png : expectedFormat, 1u, 1u, 0.0, 0.0);

        public double GetTextHeight(IXLFontBase font, double dpiY) => c_TextHeight * dpiY / c_DotsToTheInch;

        public double GetTextWidth(string text, IXLFontBase font, double dpiX) =>
            text.Length * c_DigitWidth * dpiX / c_DotsToTheInch;

        public double GetMaxDigitWidth(IXLFontBase font, double dpiX) => c_DigitWidth * dpiX / c_DotsToTheInch;

        public double GetDescent(IXLFontBase font, double dpiY) => c_Descent * dpiY / c_DotsToTheInch;

        public GlyphBox GetGlyphBox(ReadOnlySpan<int> graphemeCluster, IXLFontBase font, Dpi dpi) =>
            new(
                (float)(c_DigitWidth * dpi.X / c_DotsToTheInch),
                (float)(c_TextHeight * dpi.Y / c_DotsToTheInch),
                (float)(c_Descent * dpi.Y / c_DotsToTheInch));
    }
}
