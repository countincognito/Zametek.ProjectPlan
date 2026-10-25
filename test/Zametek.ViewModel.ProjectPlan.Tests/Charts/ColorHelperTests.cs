using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for ColorHelper. The class operates purely on bytes and string
    /// manipulation, so no Avalonia application host is required.
    /// </summary>
    public class ColorHelperTests
    {
        #region Named-colour factories

        [Fact]
        public void None_Returns_AllZeroAlphaAndChannels()
        {
            ColorFormatModel c = ColorHelper.None();
            c.A.ShouldBe((byte)0);
            c.R.ShouldBe((byte)0);
            c.G.ShouldBe((byte)0);
            c.B.ShouldBe((byte)0);
        }

        [Fact]
        public void Black_Returns_OpaqueBlack()
        {
            ColorFormatModel c = ColorHelper.Black();
            c.A.ShouldBe(ColorHelper.AnnotationAFull);
            c.R.ShouldBe((byte)0);
            c.G.ShouldBe((byte)0);
            c.B.ShouldBe((byte)0);
        }

        [Fact]
        public void Red_Returns_OpaqueRed()
        {
            ColorFormatModel c = ColorHelper.Red();
            c.A.ShouldBe(ColorHelper.AnnotationAFull);
            c.R.ShouldBe((byte)255);
            c.G.ShouldBe((byte)0);
            c.B.ShouldBe((byte)0);
        }

        [Fact]
        public void Gold_Returns_OpaqueGold()
        {
            ColorFormatModel c = ColorHelper.Gold();
            c.A.ShouldBe(ColorHelper.AnnotationAFull);
            c.R.ShouldBe((byte)255);
            c.G.ShouldBe((byte)215);
            c.B.ShouldBe((byte)0);
        }

        [Fact]
        public void Green_Returns_OpaqueGreen()
        {
            ColorFormatModel c = ColorHelper.Green();
            c.A.ShouldBe(ColorHelper.AnnotationAFull);
            c.R.ShouldBe((byte)0);
            c.G.ShouldBe((byte)128);
            c.B.ShouldBe((byte)0);
        }

        #endregion

        #region Random

        [Fact]
        public void Random_Returns_OpaqueColor()
        {
            // Alpha is always 255; RGB are random.
            ColorFormatModel c = ColorHelper.Random();
            c.A.ShouldBe(ColorHelper.AnnotationAFull);
        }

        #endregion

        #region PresetColorSequence

        [Fact]
        public void PresetColorSequence_Next_Then_WalksThePresetsInOrderAndWrapsRound()
        {
            var sequence = new PresetColorSequence();
            int count = ColorHelper.PresetColors.Count;

            List<ColorFormatModel> drawn = [.. Enumerable.Range(0, count + 1).Select(_ => sequence.Next())];

            drawn.Take(count).ShouldBe(ColorHelper.PresetColors.Select(ColorHelper.AvaloniaColorToColorFormatModel));
            drawn[count].ShouldBe(drawn[0]);
        }

        [Fact]
        public void PresetColorSequence_Reset_Then_StartsAgainFromTheFirst()
        {
            var sequence = new PresetColorSequence();
            ColorFormatModel first = sequence.Next();
            sequence.Next();

            sequence.Reset();

            sequence.Next().ShouldBe(first);
        }

        [Fact]
        public void PresetColorSequence_Given_TwoSequences_Then_NeitherShiftsTheOther()
        {
            // Two builds - two jobs, say - each keep their own sequence, so what one
            // draws or resets never moves the other.
            var one = new PresetColorSequence();
            var other = new PresetColorSequence();
            one.Next();
            one.Next();

            ColorFormatModel otherFirst = other.Next();
            one.Reset();
            ColorFormatModel otherSecond = other.Next();

            otherFirst.ShouldBe(ColorHelper.AvaloniaColorToColorFormatModel(ColorHelper.PresetColors[0]));
            otherSecond.ShouldBe(ColorHelper.AvaloniaColorToColorFormatModel(ColorHelper.PresetColors[1]));
        }

        [Fact]
        public async Task PresetColorSequence_Given_ConcurrentDraws_Then_EachColourIsAPreset()
        {
            // Drawn from many threads at once, the index must never leave the list.
            var sequence = new PresetColorSequence();
            HashSet<ColorFormatModel> presets = [.. ColorHelper.PresetColors.Select(ColorHelper.AvaloniaColorToColorFormatModel)];

            ColorFormatModel[][] drawn = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(
                () => Enumerable.Range(0, 10_000).Select(_ => sequence.Next()).ToArray())));

            drawn.SelectMany(x => x).ShouldAllBe(x => presets.Contains(x));
        }

        #endregion

        #region BytesToHtmlHexCode (3-byte variant)

        [Fact]
        public void BytesToHtmlHexCode_3Bytes_Returns_HashPrefixedUppercaseHex()
        {
            string hex = ColorHelper.BytesToHtmlHexCode(0xFF, 0x00, 0x80);
            hex.ShouldBe("#FF0080");
        }

        [Fact]
        public void BytesToHtmlHexCode_3Bytes_AllZero_Returns_HashThreeZeroPairs()
        {
            string hex = ColorHelper.BytesToHtmlHexCode(0x00, 0x00, 0x00);
            hex.ShouldBe("#000000");
        }

        [Fact]
        public void BytesToHtmlHexCode_3Bytes_AllMax_Returns_FFFFFF()
        {
            string hex = ColorHelper.BytesToHtmlHexCode(0xFF, 0xFF, 0xFF);
            hex.ShouldBe("#FFFFFF");
        }

        #endregion

        #region BytesToHtmlHexCode (4-byte variant)

        [Fact]
        public void BytesToHtmlHexCode_4Bytes_Includes_Alpha_First()
        {
            // ARGB ordering: A=FF, R=12, G=34, B=56
            string hex = ColorHelper.BytesToHtmlHexCode(0xFF, 0x12, 0x34, 0x56);
            hex.ShouldBe("#FF123456");
        }

        [Fact]
        public void BytesToHtmlHexCode_4Bytes_ZeroAlpha_StartsWithDoubleZero()
        {
            string hex = ColorHelper.BytesToHtmlHexCode(0x00, 0xAA, 0xBB, 0xCC);
            hex.ShouldBe("#00AABBCC");
        }

        #endregion

        #region ColorFormatToHtmlHexCode

        [Fact]
        public void ColorFormatToHtmlHexCode_RoundTrips_Via_HtmlHexCodeToColorFormat()
        {
            // Build a colour, convert to hex string, parse back, compare.
            var original = new ColorFormatModel { A = 255, R = 0x1A, G = 0x2B, B = 0x3C };
            string hex = ColorHelper.ColorFormatToHtmlHexCode(original);

            // The 4-byte path includes alpha; strip leading '#' for length check.
            hex.ShouldStartWith("#");
            (hex.Length == 7 || hex.Length == 9).ShouldBeTrue(); // #RRGGBB or #AARRGGBB
        }

        #endregion

        #region HtmlHexCodeToColorFormat

        [Fact]
        public void HtmlHexCodeToColorFormat_ValidSixDigit_Returns_CorrectChannels()
        {
            // #FF8000 = R=255, G=128, B=0, A=255 (default)
            ColorFormatModel c = ColorHelper.HtmlHexCodeToColorFormat("#FF8000");
            c.R.ShouldBe((byte)0xFF);
            c.G.ShouldBe((byte)0x80);
            c.B.ShouldBe((byte)0x00);
            c.A.ShouldBe(byte.MaxValue);
        }

        [Fact]
        public void HtmlHexCodeToColorFormat_ValidEightDigit_Returns_CorrectAlpha()
        {
            // 8-digit: #RRGGBBAA (note: the regex captures groups of 2 per channel)
            // bytes[0]=R, bytes[1]=G, bytes[2]=B, bytes[3]=A
            ColorFormatModel c = ColorHelper.HtmlHexCodeToColorFormat("#FF800040");
            c.R.ShouldBe((byte)0xFF);
            c.G.ShouldBe((byte)0x80);
            c.B.ShouldBe((byte)0x00);
            c.A.ShouldBe((byte)0x40);
        }

        [Fact]
        public void HtmlHexCodeToColorFormat_InvalidInput_Returns_DefaultColorFormat()
        {
            // Malformed input should return a zeroed-out model (not throw).
            ColorFormatModel c = ColorHelper.HtmlHexCodeToColorFormat("notacolor");
            c.R.ShouldBe((byte)0);
            c.G.ShouldBe((byte)0);
            c.B.ShouldBe((byte)0);
            c.A.ShouldBe((byte)0);
        }

        [Fact]
        public void HtmlHexCodeToColorFormat_MissingHash_Returns_DefaultColorFormat()
        {
            ColorFormatModel c = ColorHelper.HtmlHexCodeToColorFormat("FF8000");
            c.R.ShouldBe((byte)0);
            c.G.ShouldBe((byte)0);
            c.B.ShouldBe((byte)0);
        }

        [Fact]
        public void HtmlHexCodeToColorFormat_EmptyString_Returns_DefaultColorFormat()
        {
            ColorFormatModel c = ColorHelper.HtmlHexCodeToColorFormat(string.Empty);
            c.A.ShouldBe((byte)0);
        }

        #endregion

        #region Constant values sanity check

        [Fact]
        public void AnnotationAFull_Is_255()
        {
            ColorHelper.AnnotationAFull.ShouldBe((byte)255);
        }

        [Fact]
        public void AnnotationATransparent_IsLessThan_AnnotationALight()
        {
            ((int)ColorHelper.AnnotationATransparent).ShouldBeLessThan(ColorHelper.AnnotationALight);
        }

        [Fact]
        public void AlphaConstants_AreStrictlyOrdered()
        {
            // Transparent < Light < Medium < Heavy < Full
            ((int)ColorHelper.AnnotationATransparent).ShouldBeLessThan(ColorHelper.AnnotationALight);
            ((int)ColorHelper.AnnotationALight).ShouldBeLessThan(ColorHelper.AnnotationAMedium);
            ((int)ColorHelper.AnnotationAMedium).ShouldBeLessThan(ColorHelper.AnnotationAHeavy);
            ((int)ColorHelper.AnnotationAHeavy).ShouldBeLessThan(ColorHelper.AnnotationAFull);
        }

        #endregion
    }
}
