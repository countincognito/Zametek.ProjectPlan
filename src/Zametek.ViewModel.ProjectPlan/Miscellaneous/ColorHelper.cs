﻿using Avalonia.Media;
using System.Globalization;
using System.Text.RegularExpressions;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ColorHelper
    {
        public const byte AnnotationAMedium = 40;
        public const byte AnnotationALight = 10;
        public const byte AnnotationATrackerOverlay = 232;
        public const byte AnnotationALegend = 200;

        private static readonly Random s_Rnd = new();
        private static readonly Regex s_HtmlHexMatch = new(@"^#(([A-Fa-f0-9]{2}){3,4})$", RegexOptions.Compiled);

        public static ColorFormatModel None()
        {
            return new ColorFormatModel
            {
                A = 0,
                R = 0,
                G = 0,
                B = 0
            };
        }

        public static ColorFormatModel Black()
        {
            return new ColorFormatModel
            {
                A = 255,
                R = 0,
                G = 0,
                B = 0
            };
        }

        public static ColorFormatModel Red()
        {
            return new ColorFormatModel
            {
                A = 255,
                R = 255,
                G = 0,
                B = 0
            };
        }
        public static ColorFormatModel Gold()
        {
            return new ColorFormatModel
            {
                A = 255,
                R = 255,
                G = 215,
                B = 0
            };
        }
        public static ColorFormatModel Green()
        {
            return new ColorFormatModel
            {
                A = 255,
                R = 0,
                G = 128,
                B = 0
            };
        }

        public static ColorFormatModel Random()
        {
            var b = new byte[3];
            s_Rnd.NextBytes(b);
            return new ColorFormatModel
            {
                A = 255,
                R = b[0],
                G = b[1],
                B = b[2]
            };
        }

        public static string ColorToHtmlHexCode(Color color)
        {
            return BytesToHtmlHexCode(color.R, color.G, color.B, color.A);
        }

        public static string ColorFormatToHtmlHexCode(ColorFormatModel color)
        {
            return BytesToHtmlHexCode(color.R, color.G, color.B, color.A);
        }

        public static ColorFormatModel HtmlHexCodeToColorFormat(string input)
        {
            MatchCollection matches = s_HtmlHexMatch.Matches(input);

            if (matches.Count > 0
                && matches[0].Groups.Count > 1)
            {
                string value = matches[0].Groups[1].Value;
                byte[] bytes = Convert.FromHexString(value);

                if (bytes.Length == 3)
                {
                    return new ColorFormatModel
                    {
                        R = bytes[0],
                        G = bytes[1],
                        B = bytes[2],
                        A = byte.MaxValue
                    };
                }
                else if (bytes.Length == 4)
                {
                    return new ColorFormatModel
                    {
                        R = bytes[0],
                        G = bytes[1],
                        B = bytes[2],
                        A = bytes[3]
                    };
                }
            }

            return new ColorFormatModel();
        }

        public static string BytesToHtmlHexCode(byte r, byte g, byte b)
        {
            return $@"#{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        public static string BytesToHtmlHexCode(byte a, byte r, byte g, byte b)
        {
            return $@"#{a.ByteToHexString()}{r.ByteToHexString()}{g.ByteToHexString()}{b.ByteToHexString()}";
        }

        private static string ByteToHexString(this byte input)
        {
            return input.ToString(@"X2", CultureInfo.InvariantCulture);
        }
    }
}
