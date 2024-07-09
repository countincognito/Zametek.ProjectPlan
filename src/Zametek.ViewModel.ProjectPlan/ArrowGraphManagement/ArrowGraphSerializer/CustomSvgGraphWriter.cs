using Microsoft.Msagl.Drawing;
using System.Text.RegularExpressions;

namespace Zametek.ViewModel.ProjectPlan
{
    public class CustomSvgGraphWriter
        : SvgGraphWriter
    {
        public CustomSvgGraphWriter()
            : base()
        {
        }

        public CustomSvgGraphWriter(Stream streamPar, Graph graphP)
            : base(streamPar, graphP)
        {
        }

        protected override void WriteLabel(Label label)
        {
            if (LabelIsValid(label))
            {
                double num = label.Center.X - label.Width / 2.0;
                double num2 = label.Center.Y + label.Height / 3.0;
                WriteStartElement(@"text");
                WriteAttribute(@"x", num);
                WriteAttribute(@"y", num2);
                WriteAttribute(@"font-family", label.FontName);
                WriteAttribute(@"font-size", label.FontSize);
                WriteAttribute(@"fill", MsaglColorToSvgColor(label.FontColor));
                WriteLabelText(label.Text, num, label.FontSize);
                WriteEndElement();
            }
        }

        private static bool LabelIsValid(Label label)
        {
            if (label is null || string.IsNullOrEmpty(label.Text) || label.Width == 0.0)
            {
                return false;
            }

            return true;
        }

        private void WriteLabelText(string text, double xContainer, double fontSize)
        {
            List<string> endOfLines =
            [
                "\r\n",
                "\r",
                "\n"
            ];
            List<string> textLines = (from it in Regex.Split(NodeSanitizer(text), "(\r\n|\r|\n)")
                                      where !endOfLines.Contains(it)
                                      select it).ToList();
            bool isFirstLine = true;
            textLines.ForEach(delegate (string line)
            {
                WriteStartElement(@"tspan");
                WriteAttribute(@"x", xContainer);
                if (isFirstLine)
                {
                    isFirstLine = false;
                    WriteAttribute(@"dy", -1.0 * fontSize * (double)(textLines.Count - 1));
                }
                else
                {
                    WriteAttribute(@"dy", fontSize);
                }

                XmlWriter.WriteRaw(line);
                WriteEndElement();
            });
        }
    }
}
