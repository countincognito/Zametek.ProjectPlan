using CommandLine;
using Shouldly;
using Xunit;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for the check of zpp's and zpp serve's arguments before the parser
    /// reads them. It refuses what the parser would let pass - an option
    /// without its value, a value for a switch, more values than a list takes,
    /// an option given again, and a word for nothing - so each refusal is
    /// pinned, word for word. It must pass everything else, as it is, so each
    /// form it passes is shown to parse, and each it leaves to the parser to
    /// report is shown to be refused there - but for a short option's value
    /// after =, which it gives the parser without the =.
    /// </summary>
    public class ArgumentsHelperTests
    {
        private const string c_HelpCommand = @"zpp --help";
        private const string c_ServeHelpCommand = @"zpp serve --help";

        private static string[] Check(string[] args)
        {
            return ArgumentsHelper.Check<Options>(args, c_HelpCommand);
        }

        private static string[] CheckServe(string[] args)
        {
            return ArgumentsHelper.Check<ServeOptions>(args, c_ServeHelpCommand);
        }

        private static string Refusal(string[] args)
        {
            return Should.Throw<UsageException>(() => Check(args)).Message;
        }

        private static string ServeRefusal(string[] args)
        {
            return Should.Throw<UsageException>(() => CheckServe(args)).Message;
        }

        private static string NotAnOptionOrValue(string word, string helpCommand)
        {
            return string.Format(Resource.ProjectPlan.Messages.Message_ArgumentNotOptionOrValue, word, helpCommand);
        }

        // Every way of writing an option and its values that the parser reads.
        public static TheoryData<string[]> ArgumentsTheParserReads => new()
        {
            { [@"-i", @"a.zpp"] },
            { [@"--input", @"a.zpp"] },
            { [@"--input=a.zpp"] },
            { [@"-ia.zpp"] },
            { [@"-i", @"a.zpp", @"-vl"] },
            { [@"-i", @"a.zpp", @"-vo", @"out.zpp"] },
            { [@"-i", @"a.zpp", @"-voout.zpp"] },
            { [@"-i", @"a.zpp", @"--output=x=y.zpp"] },
            { [@"-i", @"a.zpp", @"-s", @"Iteration 2", @"-o", @"plan-iter2.zpp"] },
            { [@"-i", @"a.zpp", @"--compile-timeout", @"-1"] },
            { [@"-i", @"a.zpp", @"-o", @"-"] },
            { [@"-i", @"a.zpp", @"-o", string.Empty] },
            { [@"-i", @"a.zpp", @"--gantt-directory", @"out", @"--gantt-size", @"800:600"] },
            { [@"-i", @"a.zpp", @"--gantt-size", @"800", @"600"] },
            { [@"-i", @"a.zpp", @"--gantt-size=800:600"] },
            { [@"-i", @"a.zpp", @"--gantt-size=800", @"600"] },
            { [@"-i", @"a.zpp", @"--now", @"2026-10-03T09:00:00+01:00", @"--metrics-format", @"json", @"--base-theme", @"dark"] },
            { [@"-m", @"plan.xlsx", @"--server", @"unix:/run/zpp.sock", @"--api-key-file", @"key.txt"] },
        };

        public static TheoryData<string[]> ServeArgumentsTheParserReads => new()
        {
            { [] },
            { [@"--listen", @"http://localhost:9770", @"https://0.0.0.0:9771"] },
            { [@"--listen", @"http://localhost:9770", @"--listen", @"https://0.0.0.0:9771"] },
            { [@"--listen=http://localhost:9770", @"https://0.0.0.0:9771"] },
            { [@"--max-chart-size", @"600:400", @"-v"] },
            { [@"--unix-socket", @"/run/zpp.sock", @"--culture", @"en-GB", @"--max-jobs", @"2"] },
        };

        [Theory]
        [MemberData(nameof(ArgumentsTheParserReads))]
        public void Check_Given_ArgumentsTheParserReads_Then_LetsThemPassAsTheyAre(string[] args)
        {
            Check(args).ShouldBe(args);
            OptionsParsingTests.Parse(args).ShouldBeOfType<Parsed<Options>>();
        }

        [Theory]
        [MemberData(nameof(ServeArgumentsTheParserReads))]
        public void Check_Given_ServeArgumentsTheParserReads_Then_LetsThemPassAsTheyAre(string[] args)
        {
            CheckServe(args).ShouldBe(args);
            ServeOptionsParsingTests.Parse(args).ShouldBeOfType<Parsed<ServeOptions>>();
        }

        // What the parser refuses itself: help asked for, an option it does not know - after which the check reads no
        // further - too few values for a list, a value it cannot read, and no plan.
        public static TheoryData<string[]> ArgumentsTheParserRefuses => new()
        {
            { [@"--help"] },
            { [@"--help", @"extra"] },
            { [@"-i", @"a.zpp", @"--nonsense", @"value"] },
            { [@"-i", @"a.zpp", @"-Z", @"value"] },
            { [@"-i", @"a.zpp", @"--Output", @"out.zpp"] },
            { [@"-i", @"a.zpp", @"--gantt-size", @"800"] },
            { [@"-i", @"a.zpp", @"--compile-timeout", @"soon"] },
            { [] },
        };

        public static TheoryData<string[]> ServeArgumentsTheParserRefuses => new()
        {
            { [@"--help"] },
            { [@"--nonsense", @"value"] },
            { [@"--max-jobs", @"many"] },
            { [@"--max-chart-size", @"600"] },
        };

        [Theory]
        [MemberData(nameof(ArgumentsTheParserRefuses))]
        public void Check_Given_ArgumentsTheParserRefuses_Then_LeavesThemToIt(string[] args)
        {
            Check(args).ShouldBe(args);
            OptionsParsingTests.Parse(args).ShouldBeOfType<NotParsed<Options>>();
        }

        [Theory]
        [MemberData(nameof(ServeArgumentsTheParserRefuses))]
        public void Check_Given_ServeArgumentsTheParserRefuses_Then_LeavesThemToIt(string[] args)
        {
            CheckServe(args).ShouldBe(args);
            ServeOptionsParsingTests.Parse(args).ShouldBeOfType<NotParsed<ServeOptions>>();
        }

        // A short option's value after =, which the parser would read with the = at its front, as part of the value: it
        // is given the value without, as it is given --output=out.zpp's.
        public static TheoryData<string[], string[]> ShortOptionsWithValuesAfterAnEquals => new()
        {
            { [@"-i=a.zpp"], [@"-ia.zpp"] },
            { [@"-i", @"a.zpp", @"-o=out.zpp"], [@"-i", @"a.zpp", @"-oout.zpp"] },
            { [@"-i", @"a.zpp", @"-vo=out.zpp"], [@"-i", @"a.zpp", @"-voout.zpp"] },
            { [@"-i", @"a.zpp", @"-s=-draft"], [@"-i", @"a.zpp", @"-s-draft"] },
            { [@"-i", @"a.zpp", @"-o==x.zpp"], [@"-i", @"a.zpp", @"-o=x.zpp"] },
        };

        [Theory]
        [MemberData(nameof(ShortOptionsWithValuesAfterAnEquals))]
        public void Check_Given_AShortOptionsValueAfterAnEquals_Then_GivesItToTheParserWithout(string[] args, string[] arguments)
        {
            Check(args).ShouldBe(arguments);
        }

        [Fact]
        public void Check_Given_ShortOptionsValuesAfterAnEquals_Then_TheParserReadsTheValues()
        {
            Options options = OptionsParsingTests.Parse(Check([@"-i=a.zpp", @"-o=out.zpp", @"-s=-draft"]))
                .ShouldBeOfType<Parsed<Options>>().Value;

            options.InputFilename.ShouldBe(@"a.zpp");
            options.OutputFilename.ShouldBe(@"out.zpp");
            options.Scenario.ShouldBe(@"-draft");

            // The one = is taken, so a value can still start with one.
            OptionsParsingTests.Parse(Check([@"-i", @"a.zpp", @"-o==x.zpp"]))
                .ShouldBeOfType<Parsed<Options>>().Value.OutputFilename.ShouldBe(@"=x.zpp");
        }

        // An option that takes a value, given none: last, or before another option - which the parser would take as the
        // value, by its name - or with nothing after =.
        public static TheoryData<string[], string> OptionsWithoutTheirValues => new()
        {
            { [@"-i", @"a.zpp", @"-o"], @"--output" },
            { [@"-i", @"a.zpp", @"--server"], @"--server" },
            { [@"-i", @"a.zpp", @"--compile-timeout"], @"--compile-timeout" },
            { [@"-i", @"a.zpp", @"--now"], @"--now" },
            { [@"-i", @"a.zpp", @"-t"], @"--base-theme" },
            { [@"-i", @"a.zpp", @"-vo"], @"--output" },
            { [@"-i", @"a.zpp", @"-s", @"--metrics-format", @"json"], @"--scenario" },
            { [@"-i", @"--import", @"b.mpp"], @"--input" },
            { [@"-i", @"a.zpp", @"-o", @"-x.zpp"], @"--output" },
            { [@"-i", @"a.zpp", @"--output="], @"--output" },
            { [@"-i", @"a.zpp", @"-o="], @"--output" },
            { [@"-i", @"a.zpp", @"-vo=", @"out.zpp"], @"--output" },
            { [@"-i", @"a.zpp", @"--gantt-size"], @"--gantt-size" },
            { [@"-i", @"a.zpp", @"--gantt-size", @"-v"], @"--gantt-size" },
        };

        public static TheoryData<string[], string> ServeOptionsWithoutTheirValues => new()
        {
            { [@"--unix-socket"], @"--unix-socket" },
            { [@"--culture"], @"--culture" },
            { [@"--max-jobs", @"--listen", @"http://localhost:9779"], @"--max-jobs" },
            { [@"--listen"], @"--listen" },
        };

        [Theory]
        [MemberData(nameof(OptionsWithoutTheirValues))]
        public void Check_Given_AnOptionWithoutItsValue_Then_UsageExceptionNamingIt(string[] args, string option)
        {
            Refusal(args).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionNeedsValue, option));
        }

        [Theory]
        [MemberData(nameof(ServeOptionsWithoutTheirValues))]
        public void Check_Given_AServeOptionWithoutItsValue_Then_UsageExceptionNamingIt(string[] args, string option)
        {
            ServeRefusal(args).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionNeedsValue, option));
        }

        [Theory]
        [InlineData(@"--verbose=false", @"--verbose")]
        [InlineData(@"--verbose=", @"--verbose")]
        [InlineData(@"--list-scenarios=true", @"--list-scenarios")]
        [InlineData(@"--local=yes", @"--local")]
        [InlineData(@"-v=false", @"--verbose")]
        [InlineData(@"-vl=true", @"--list-scenarios")]
        public void Check_Given_ASwitchWithAValue_Then_UsageExceptionNamingIt(string arg, string option)
        {
            // The parser would set the switch, whatever the value said, and drop the value.
            Refusal([@"-i", @"a.zpp", arg]).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionTakesNoValue, option));
        }

        [Fact]
        public void Check_Given_AServeSwitchWithAValue_Then_UsageExceptionNamingIt()
        {
            ServeRefusal([@"--verbose=false"]).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionTakesNoValue, @"--verbose"));
        }

        public static TheoryData<string[]> SizesWithTooManyValues => new()
        {
            { [@"-i", @"a.zpp", @"--gantt-size", @"800:600:400"] },
            { [@"-i", @"a.zpp", @"--gantt-size", @"800", @"600", @"400"] },
            { [@"-i", @"a.zpp", @"--gantt-size=800:600", @"400"] },
            { [@"-i", @"a.zpp", @"--gantt-size", @"800:600", @"extra"] },
        };

        [Theory]
        [MemberData(nameof(SizesWithTooManyValues))]
        public void Check_Given_ASizeWithTooManyValues_Then_UsageExceptionNamingIt(string[] args)
        {
            // The parser would keep the first two, and drop the rest.
            Refusal(args).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionTakesAtMostValues, @"--gantt-size", 2));
        }

        [Fact]
        public void Check_Given_AServeSizeWithTooManyValues_Then_UsageExceptionNamingIt()
        {
            ServeRefusal([@"--max-chart-size", @"600:400:300"])
                .ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionTakesAtMostValues, @"--max-chart-size", 2));
        }

        // An option given again - by either of its names, and as a switch in a word of switches - which the parser
        // refuses for zpp.
        public static TheoryData<string[], string> OptionsGivenAgain => new()
        {
            { [@"-i", @"a.zpp", @"-o", @"x.zpp", @"-o", @"y.zpp"], @"--output" },
            { [@"-i", @"a.zpp", @"--input", @"b.zpp"], @"--input" },
            { [@"-i", @"a.zpp", @"-v", @"--verbose"], @"--verbose" },
            { [@"-i", @"a.zpp", @"-vv"], @"--verbose" },
            { [@"-i", @"a.zpp", @"--gantt-size", @"800:600", @"--gantt-size", @"700:500"], @"--gantt-size" },
        };

        // The same for zpp serve, whose parser takes an option more than once, for --listen's sake: of any other, it
        // keeps one value - the last, or a list's first - and ignores the rest.
        public static TheoryData<string[], string> ServeOptionsGivenAgain => new()
        {
            { [@"--max-chart-size", @"600:400", @"--max-chart-size", @"700:500"], @"--max-chart-size" },
            { [@"--culture", @"en-GB", @"--culture", @"fr-FR"], @"--culture" },
            { [@"--culture", @"en-GB", @"--culture"], @"--culture" },
            { [@"--max-jobs", @"2", @"--max-jobs=4"], @"--max-jobs" },
            { [@"-v", @"-v"], @"--verbose" },
        };

        [Theory]
        [MemberData(nameof(OptionsGivenAgain))]
        public void Check_Given_AnOptionGivenAgain_Then_UsageExceptionNamingIt(string[] args, string option)
        {
            Refusal(args).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionGivenMoreThanOnce, option));
        }

        [Theory]
        [MemberData(nameof(ServeOptionsGivenAgain))]
        public void Check_Given_AServeOptionGivenAgain_Then_UsageExceptionNamingIt(string[] args, string option)
        {
            ServeRefusal(args).ShouldBe(string.Format(Resource.ProjectPlan.Messages.Message_OptionGivenMoreThanOnce, option));
            ServeOptionsParsingTests.Parse(args).ShouldBeOfType<Parsed<ServeOptions>>();
        }

        // A word that is neither an option nor an option's value - before the options, after them, after an option's
        // one value or after a switch, -- alone, and the rest of a word of switches - which the parser would ignore.
        public static TheoryData<string[], string> WordsForNothing => new()
        {
            { [@"-i", @"a.zpp", @"extra"], @"extra" },
            { [@"extra", @"-i", @"a.zpp"], @"extra" },
            { [@"help"], @"help" },
            { [@"-i", @"a.zpp", @"-o", @"x.zpp", @"y.zpp"], @"y.zpp" },
            { [@"-i", @"a.zpp", @"-l", @"x"], @"x" },
            { [@"-i", @"a.zpp", string.Empty], string.Empty },
            { [@"-i", @"a.zpp", @"--"], @"--" },
            { [@"-i", @"a.zpp", @"-o", @"--", @"x.zpp"], @"--" },
            { [@"-i", @"a.zpp", @"-vX"], @"X" },
            { [@"-i", @"a.zpp", @"-v1"], @"1" },
        };

        public static TheoryData<string[], string> ServeWordsForNothing => new()
        {
            { [@"help"], @"help" },
            { [@"-v", @"extra"], @"extra" },
            { [@"--listen", @"http://localhost:9770", @"-v", @"http://localhost:9771"], @"http://localhost:9771" },
        };

        [Theory]
        [MemberData(nameof(WordsForNothing))]
        public void Check_Given_AWordForNothing_Then_UsageExceptionNamingIt(string[] args, string word)
        {
            Refusal(args).ShouldBe(NotAnOptionOrValue(word, c_HelpCommand));
        }

        [Theory]
        [MemberData(nameof(ServeWordsForNothing))]
        public void Check_Given_AServeWordForNothing_Then_UsageExceptionNamingIt(string[] args, string word)
        {
            ServeRefusal(args).ShouldBe(NotAnOptionOrValue(word, c_ServeHelpCommand));
        }

        [Fact]
        public void Check_Given_OptionsWithAValueOfItsOwn_Then_InvalidOperationException()
        {
            // A word for nothing could be such a value, so the check cannot be used with one.
            Should.Throw<InvalidOperationException>(() => ArgumentsHelper.Check<OptionsWithAValueOfItsOwn>([], c_HelpCommand));
        }

        [Fact]
        public void Check_Given_ACountOfAFlag_Then_TakesItForASwitchThatCanBeGivenAgain()
        {
            // As the parser does: -v -v counts two, and takes no value.
            Should.NotThrow(() => ArgumentsHelper.Check<OptionsWithACount>([@"-v", @"-v"], c_HelpCommand));
            Should.NotThrow(() => ArgumentsHelper.Check<OptionsWithACount>([@"-vv"], c_HelpCommand));
        }

        private sealed class OptionsWithAValueOfItsOwn
        {
            [Value(0)]
            public string? Word { get; set; }
        }

        private sealed class OptionsWithACount
        {
            [Option('v', @"verbose", FlagCounter = true)]
            public int Verbose { get; set; }
        }
    }
}
