using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for RecentProjectFileHelper. The helper is pure list and string
    /// logic; the platform-dependent default comparer is bypassed by passing
    /// explicit comparers, so both the case-insensitive (Windows/macOS) and
    /// case-sensitive (Linux) behaviours run on every platform.
    /// </summary>
    public class RecentProjectFileHelperTests
    {
        private static readonly string s_BaseDirectory =
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), @"zametek-recents"));

        private static string BasePath(string filename) => Path.Combine(s_BaseDirectory, filename);

        #region NormalizePath

        [Fact]
        public void NormalizePath_Given_RelativeSegments_Then_Collapsed()
        {
            string input = Path.Combine(s_BaseDirectory, @"sub", @"..", @"alpha.zpp");
            RecentProjectFileHelper.NormalizePath(input).ShouldBe(BasePath(@"alpha.zpp"));
        }

        [Fact]
        public void NormalizePath_Given_SurroundingWhitespace_Then_Trimmed()
        {
            string input = $@"  {BasePath(@"alpha.zpp")}  ";
            RecentProjectFileHelper.NormalizePath(input).ShouldBe(BasePath(@"alpha.zpp"));
        }

        [Fact]
        public void NormalizePath_Given_Whitespace_Then_Throws()
        {
            Should.Throw<ArgumentException>(() => RecentProjectFileHelper.NormalizePath(@" "));
        }

        #endregion

        #region Record

        [Fact]
        public void Record_Given_NullList_Then_SingleEntry()
        {
            List<string> result = RecentProjectFileHelper.Record(null, BasePath(@"alpha.zpp"), 10);

            result.ShouldBe([BasePath(@"alpha.zpp")]);
        }

        [Fact]
        public void Record_Given_ExistingEntries_Then_NewestFirst()
        {
            List<string> existing = [BasePath(@"beta.zpp"), BasePath(@"gamma.zpp")];

            List<string> result = RecentProjectFileHelper.Record(existing, BasePath(@"alpha.zpp"), 10);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"beta.zpp"), BasePath(@"gamma.zpp")]);
        }

        [Fact]
        public void Record_Given_RelativeDuplicateSpelling_Then_Deduplicated()
        {
            List<string> existing = [BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")];
            string input = Path.Combine(s_BaseDirectory, @"sub", @"..", @"alpha.zpp");

            List<string> result = RecentProjectFileHelper.Record(existing, input, 10, StringComparer.Ordinal);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Record_Given_DuplicateWithDifferentCase_When_CaseInsensitive_Then_NewestSpellingWins()
        {
            List<string> existing = [BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Record(
                existing, BasePath(@"alpha.zpp"), 10, StringComparer.OrdinalIgnoreCase);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Record_Given_DuplicateWithDifferentCase_When_CaseSensitive_Then_BothKept()
        {
            List<string> existing = [BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Record(
                existing, BasePath(@"alpha.zpp"), 10, StringComparer.Ordinal);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Record_Given_MoreThanMaximum_Then_OldestDropped()
        {
            List<string> existing = [BasePath(@"beta.zpp"), BasePath(@"gamma.zpp"), BasePath(@"delta.zpp")];

            List<string> result = RecentProjectFileHelper.Record(existing, BasePath(@"alpha.zpp"), 3);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"beta.zpp"), BasePath(@"gamma.zpp")]);
        }

        [Fact]
        public void Record_Given_ZeroMaximum_Then_Empty()
        {
            List<string> result = RecentProjectFileHelper.Record(null, BasePath(@"alpha.zpp"), 0);

            result.ShouldBeEmpty();
        }

        [Fact]
        public void Record_Given_WhitespaceEntries_Then_Skipped()
        {
            List<string> existing = [string.Empty, @" ", BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Record(existing, BasePath(@"alpha.zpp"), 10);

            result.ShouldBe([BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")]);
        }

        #endregion

        #region Remove

        [Fact]
        public void Remove_Given_MatchingEntry_Then_Removed()
        {
            List<string> existing = [BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Remove(existing, BasePath(@"alpha.zpp"));

            result.ShouldBe([BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Remove_Given_RelativeDuplicateSpelling_Then_Removed()
        {
            List<string> existing = [BasePath(@"alpha.zpp"), BasePath(@"beta.zpp")];
            string input = Path.Combine(s_BaseDirectory, @"sub", @"..", @"alpha.zpp");

            List<string> result = RecentProjectFileHelper.Remove(existing, input, StringComparer.Ordinal);

            result.ShouldBe([BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Remove_Given_DifferentCase_When_CaseInsensitive_Then_Removed()
        {
            List<string> existing = [BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Remove(
                existing, BasePath(@"alpha.zpp"), StringComparer.OrdinalIgnoreCase);

            result.ShouldBe([BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Remove_Given_DifferentCase_When_CaseSensitive_Then_Kept()
        {
            List<string> existing = [BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")];

            List<string> result = RecentProjectFileHelper.Remove(
                existing, BasePath(@"alpha.zpp"), StringComparer.Ordinal);

            result.ShouldBe([BasePath(@"ALPHA.ZPP"), BasePath(@"beta.zpp")]);
        }

        [Fact]
        public void Remove_Given_NullList_Then_Empty()
        {
            List<string> result = RecentProjectFileHelper.Remove(null, BasePath(@"alpha.zpp"));

            result.ShouldBeEmpty();
        }

        #endregion
    }
}
