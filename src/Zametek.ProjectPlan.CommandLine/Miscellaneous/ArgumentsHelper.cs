using CommandLine;
using System.Collections;
using System.Reflection;

namespace Zametek.ProjectPlan.CommandLine
{
    // Checks the arguments of zpp and of zpp serve, before the parser reads them, for what the parser lets pass without
    // a word: an option that takes a value but is not given one, which it drops - or gives, as its value, the name of
    // the option after it - and a value given to a switch, values past the most a list takes, and a word that is
    // neither an option nor an option's value, all of which it ignores. Each is refused with a UsageException.
    //
    // The arguments are read as the parser (CommandLineParser 2.9.1) reads them: --name value, --name=value, -n value,
    // -nvalue, and switches together, as -vl; a word that starts with - and a digit, as a negative number does, and -
    // alone are values; and -- alone, which the parser drops, is a word for nothing. The parser is left to report what
    // it reports itself: an option it does not know - after which the check reads no further, since the parser refuses
    // the arguments anyway - --help, too few values for a list, and a value it cannot read.
    internal static class ArgumentsHelper
    {
        private const string c_LongPrefix = @"--";
        private const char c_ShortPrefix = '-';
        private const char c_ValueSeparator = '=';

        // Refuses what the parser would let pass in args, the arguments of the command whose options are TOptions;
        // helpCommand is the command that lists those options.
        public static void Check<TOptions>(
            IReadOnlyList<string> args,
            string helpCommand)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(helpCommand);

            Dictionary<string, PropertyInfo> options = OptionsByName<TOptions>();

            // The option the next value is for, if there is one, and how many values it has been given.
            PropertyInfo? waiting = null;
            int values = 0;

            foreach (string arg in args)
            {
                if (IsValue(arg))
                {
                    if (waiting is null)
                    {
                        throw NotAnOptionOrValue(arg, helpCommand);
                    }

                    values = AddValues<TOptions>(waiting, values, arg);

                    // A list takes the values up to the next option; anything else, the one.
                    if (!IsList(waiting))
                    {
                        waiting = null;
                    }

                    continue;
                }

                if (arg == c_LongPrefix)
                {
                    throw NotAnOptionOrValue(arg, helpCommand);
                }

                // An option comes next, so the one before it has all the values it is given.
                if (waiting is not null
                    && values == 0)
                {
                    throw NeedsValue<TOptions>(waiting);
                }

                waiting = null;
                values = 0;

                if (arg.StartsWith(c_LongPrefix, StringComparison.Ordinal))
                {
                    // --name, or --name=value.
                    string text = arg[c_LongPrefix.Length..];
                    int separator = text.IndexOf(c_ValueSeparator, StringComparison.Ordinal);
                    string name = separator < 0 ? text : text[..separator];

                    if (!options.TryGetValue(name, out PropertyInfo? option))
                    {
                        return;
                    }

                    if (separator < 0)
                    {
                        waiting = TakesValue(option) ? option : null;
                        continue;
                    }

                    string value = text[(separator + 1)..];

                    if (!TakesValue(option))
                    {
                        throw new UsageException(string.Format(
                            Resource.ProjectPlan.Messages.Message_OptionTakesNoValue,
                            Program.OptionLongName<TOptions>(option.Name)));
                    }

                    if (value.Length == 0)
                    {
                        throw NeedsValue<TOptions>(option);
                    }

                    values = AddValues<TOptions>(option, 0, value);
                    waiting = IsList(option) ? option : null;
                    continue;
                }

                // -n, or switches together, as -vl, perhaps ending with an option that takes a value: the rest of the
                // word, if there is any, is its value, and otherwise the next word is.
                for (int index = 1; index < arg.Length; index++)
                {
                    if (!options.TryGetValue(arg[index..(index + 1)], out PropertyInfo? option))
                    {
                        // The parser reports an option it does not know when it comes first; after a switch, it takes the
                        // rest of the word for a value, which no option has.
                        if (index == 1)
                        {
                            return;
                        }

                        throw NotAnOptionOrValue(arg[index..], helpCommand);
                    }

                    if (TakesValue(option))
                    {
                        string value = arg[(index + 1)..];

                        if (value.Length == 0)
                        {
                            waiting = option;
                        }
                        else
                        {
                            values = AddValues<TOptions>(option, 0, value);
                            waiting = IsList(option) ? option : null;
                        }

                        break;
                    }
                }
            }

            if (waiting is not null
                && values == 0)
            {
                throw NeedsValue<TOptions>(waiting);
            }
        }

        // The options of TOptions, by each of their names: the parser looks a name up among the long and the short names
        // alike, whether it is given as --name or as -n.
        private static Dictionary<string, PropertyInfo> OptionsByName<TOptions>()
        {
            var options = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

            foreach (PropertyInfo property in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // A word that is no option's value is refused, which is only right while no value stands on its own.
                if (property.GetCustomAttribute<ValueAttribute>() is not null)
                {
                    throw new InvalidOperationException($@"{property.Name} on {typeof(TOptions).Name} is a value of its own, which the arguments check does not allow for");
                }

                if (property.GetCustomAttribute<OptionAttribute>() is OptionAttribute attribute)
                {
                    if (!string.IsNullOrEmpty(attribute.LongName))
                    {
                        options.Add(attribute.LongName, property);
                    }

                    if (!string.IsNullOrEmpty(attribute.ShortName))
                    {
                        options.Add(attribute.ShortName, property);
                    }
                }
            }

            return options;
        }

        // Whether the parser reads arg as a value: a word that does not start with -, - alone, or a word that starts with
        // - and a digit, as a negative number does.
        private static bool IsValue(string arg)
        {
            return arg.Length < 2
                || arg[0] != c_ShortPrefix
                || char.IsDigit(arg[1]);
        }

        // Whether the option takes a value: every option does, as the parser has it, but a switch - a bool, or a count of
        // how many times it is given.
        private static bool TakesValue(PropertyInfo option)
        {
            return option.PropertyType != typeof(bool)
                && !option.GetCustomAttribute<OptionAttribute>()!.FlagCounter;
        }

        // Whether the option, which takes a value, takes a list of them, as the parser has it: anything that holds
        // several, but a string.
        private static bool IsList(PropertyInfo option)
        {
            return option.PropertyType != typeof(string)
                && typeof(IEnumerable).IsAssignableFrom(option.PropertyType);
        }

        // How many values the option has with value as well as the values it has: a list's first value after its name is
        // split where the list says, as the parser splits it, and more than the most the list takes are refused.
        private static int AddValues<TOptions>(
            PropertyInfo option,
            int values,
            string value)
        {
            if (!IsList(option))
            {
                return values + 1;
            }

            OptionAttribute attribute = option.GetCustomAttribute<OptionAttribute>()!;
            int added = values == 0 && attribute.Separator != default
                ? value.Split(attribute.Separator).Length
                : 1;

            return attribute.Max >= 0
                && values + added > attribute.Max
                ? throw new UsageException(string.Format(
                    Resource.ProjectPlan.Messages.Message_OptionTakesAtMostValues,
                    Program.OptionLongName<TOptions>(option.Name),
                    attribute.Max))
                : values + added;
        }

        private static UsageException NeedsValue<TOptions>(PropertyInfo option)
        {
            return new UsageException(string.Format(
                Resource.ProjectPlan.Messages.Message_OptionNeedsValue,
                Program.OptionLongName<TOptions>(option.Name)));
        }

        private static UsageException NotAnOptionOrValue(
            string word,
            string helpCommand)
        {
            return new UsageException(string.Format(
                Resource.ProjectPlan.Messages.Message_ArgumentNotOptionOrValue,
                word,
                helpCommand));
        }
    }
}
