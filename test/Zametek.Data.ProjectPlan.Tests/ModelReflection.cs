using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// Reflection over the persisted models, so that the version mapping tests do not
    /// have to be rewritten every time a model gains a property.
    ///
    /// A hand-built fixture only ever tests the properties whoever wrote it remembered,
    /// and a version mapping fails by omission - a property that is not carried is not
    /// an error anywhere, it is simply a default on the other side. So these tests fill
    /// every property with a value that is not its default, map it, and compare
    /// everything back, which is the only way an omission can announce itself.
    /// </summary>
    internal static class ModelReflection
    {
        private const int c_CollectionSize = 2;
        private const int c_MaxDepth = 12;

        private static readonly DateTimeOffset s_Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private static bool IsModel(Type type) =>
            type.IsClass && !type.IsAbstract && type.Namespace is not null && type.Namespace.StartsWith(@"Zametek", StringComparison.Ordinal);

        private static bool IsCollection(Type type) =>
            type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

        private static IEnumerable<PropertyInfo> ReadableProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.CanRead && x.GetIndexParameters().Length == 0);

        #region Filling

        /// <summary>
        /// Builds an instance of the given model with every property set to something
        /// distinctive: a property left at its default would survive a mapping that
        /// dropped it, and prove nothing.
        /// </summary>
        public static object Fill(Type type) => Fill(type, new Counter(), 0);

        public static T Fill<T>() where T : class => (T)Fill(typeof(T));

        private sealed class Counter
        {
            private int m_Value;

            public int Next() => ++m_Value;
        }

        private static object Fill(Type type, Counter counter, int depth)
        {
            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable is not null)
            {
                return Fill(nullable, counter, depth);
            }

            if (type.IsEnum)
            {
                // The last declared value, so that a mapping which quietly leaves an
                // enum at its default is caught rather than matching by accident.
                Array values = Enum.GetValues(type);
                return values.GetValue(values.Length - 1)!;
            }

            int n = counter.Next();

            if (type == typeof(string))
            {
                return $@"value {n}";
            }
            if (type == typeof(bool))
            {
                return true;
            }
            if (type == typeof(int))
            {
                return n;
            }
            if (type == typeof(long))
            {
                return (long)n;
            }
            if (type == typeof(short))
            {
                return (short)n;
            }
            if (type == typeof(byte))
            {
                return (byte)(n % 256);
            }
            if (type == typeof(double))
            {
                return n + 0.5;
            }
            if (type == typeof(decimal))
            {
                return n + 0.5m;
            }
            if (type == typeof(Guid))
            {
                var bytes = new byte[16];
                bytes[0] = (byte)(n % 256);
                bytes[1] = (byte)(n / 256);
                return new Guid(bytes);
            }
            if (type == typeof(DateTimeOffset))
            {
                return s_Epoch.AddDays(n);
            }
            if (type == typeof(DateTime))
            {
                return s_Epoch.AddDays(n).DateTime;
            }
            if (type == typeof(TimeSpan))
            {
                return TimeSpan.FromMinutes(n);
            }

            if (IsCollection(type))
            {
                return FillCollection(type, counter, depth);
            }

            if (IsModel(type))
            {
                if (depth >= c_MaxDepth)
                {
                    throw new InvalidOperationException($@"model nesting exceeded {c_MaxDepth} at {type.FullName}");
                }

                object instance = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($@"{type.FullName} has no parameterless constructor");

                foreach (PropertyInfo property in ReadableProperties(type).Where(x => x.SetMethod is not null))
                {
                    property.SetValue(instance, Fill(property.PropertyType, counter, depth + 1));
                }

                return instance;
            }

            throw new InvalidOperationException($@"no fill rule for {type.FullName}");
        }

        private static object FillCollection(Type type, Counter counter, int depth)
        {
            Type elementType =
                type.IsGenericType ? type.GetGenericArguments()[0]
                : type.IsArray ? type.GetElementType()!
                : throw new InvalidOperationException($@"cannot determine the element type of {type.FullName}");

            Type listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType)!;

            for (int i = 0; i < c_CollectionSize; i++)
            {
                list.Add(Fill(elementType, counter, depth + 1));
            }

            if (type.IsArray)
            {
                Array array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return type.IsAssignableFrom(listType)
                ? list
                : throw new InvalidOperationException($@"cannot fill collection type {type.FullName}");
        }

        #endregion

        #region Comparing

        /// <summary>
        /// The paths at which two instances of the same model type differ, so a failure
        /// names the property rather than dumping two object graphs.
        ///
        /// A path looks like <c>$.DependentActivities[].Activity.ColorFormat</c>. Paths
        /// given as expected are left out of the result: a version that has no room for
        /// a property is not a fault, but which properties those are is worth stating
        /// rather than leaving to a blanket comparison.
        /// </summary>
        public static IReadOnlyList<string> Differences(
            object? left,
            object? right,
            params string[] expectedDifferences)
        {
            var expected = new HashSet<string>(expectedDifferences, StringComparer.Ordinal);
            return
            [
                .. Differences(left, right, @"$", 0)
                    .Where(x => !expected.Contains(WithoutIndices(x[..x.IndexOf(':', StringComparison.Ordinal)])))
            ];
        }

        /// <summary>
        /// Collapses the indices in a path, so that one expectation covers every element
        /// of a collection rather than needing one per index.
        /// </summary>
        private static string WithoutIndices(string path)
        {
            var builder = new System.Text.StringBuilder(path.Length);
            bool inIndex = false;

            foreach (char character in path)
            {
                if (character == '[')
                {
                    inIndex = true;
                    builder.Append('[');
                }
                else if (character == ']')
                {
                    inIndex = false;
                    builder.Append(']');
                }
                else if (!inIndex)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<string> Differences(object? left, object? right, string path, int depth)
        {
            if (left is null || right is null)
            {
                if (!(left is null && right is null))
                {
                    yield return $@"{path}: {left ?? @"null"} vs {right ?? @"null"}";
                }
                yield break;
            }

            Type type = left.GetType();

            if (depth >= c_MaxDepth || type.IsPrimitive || type.IsEnum || type == typeof(string)
                || type == typeof(Guid) || type == typeof(DateTimeOffset) || type == typeof(DateTime)
                || type == typeof(decimal) || type == typeof(TimeSpan))
            {
                if (!Equals(left, right))
                {
                    yield return $@"{path}: {left} vs {right}";
                }
                yield break;
            }

            if (IsCollection(type))
            {
                List<object?> leftItems = [.. ((IEnumerable)left).Cast<object?>()];
                List<object?> rightItems = [.. ((IEnumerable)right).Cast<object?>()];

                if (leftItems.Count != rightItems.Count)
                {
                    yield return $@"{path}: {leftItems.Count} items vs {rightItems.Count}";
                    yield break;
                }

                for (int i = 0; i < leftItems.Count; i++)
                {
                    foreach (string difference in Differences(leftItems[i], rightItems[i], $@"{path}[{i}]", depth + 1))
                    {
                        yield return difference;
                    }
                }
                yield break;
            }

            if (IsModel(type))
            {
                foreach (PropertyInfo property in ReadableProperties(type))
                {
                    foreach (string difference in Differences(
                        property.GetValue(left),
                        property.GetValue(right),
                        $@"{path}.{property.Name}",
                        depth + 1))
                    {
                        yield return difference;
                    }
                }
                yield break;
            }

            if (!Equals(left, right))
            {
                yield return $@"{path}: {left} vs {right}";
            }
        }

        #endregion

        #region Shared collections

        /// <summary>
        /// The paths at which a mapped result still holds the very list it was given,
        /// rather than one of its own. Matched by property name, since the two sides are
        /// different versions of the same model.
        /// </summary>
        public static IReadOnlyList<string> SharedCollections(object? source, object? target) =>
            [.. SharedCollections(source, target, @"$", 0)];

        private static IEnumerable<string> SharedCollections(object? source, object? target, string path, int depth)
        {
            if (source is null || target is null || depth >= c_MaxDepth)
            {
                yield break;
            }

            Type sourceType = source.GetType();
            Type targetType = target.GetType();

            if (IsCollection(sourceType) && IsCollection(targetType))
            {
                if (ReferenceEquals(source, target))
                {
                    yield return path;
                    yield break;
                }

                List<object?> sourceItems = [.. ((IEnumerable)source).Cast<object?>()];
                List<object?> targetItems = [.. ((IEnumerable)target).Cast<object?>()];

                for (int i = 0; i < Math.Min(sourceItems.Count, targetItems.Count); i++)
                {
                    foreach (string shared in SharedCollections(sourceItems[i], targetItems[i], $@"{path}[{i}]", depth + 1))
                    {
                        yield return shared;
                    }
                }
                yield break;
            }

            if (!IsModel(sourceType) || !IsModel(targetType))
            {
                yield break;
            }

            foreach (PropertyInfo targetProperty in ReadableProperties(targetType))
            {
                PropertyInfo? sourceProperty = sourceType.GetProperty(
                    targetProperty.Name,
                    BindingFlags.Public | BindingFlags.Instance);

                if (sourceProperty is null || sourceProperty.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                foreach (string shared in SharedCollections(
                    sourceProperty.GetValue(source),
                    targetProperty.GetValue(target),
                    $@"{path}.{targetProperty.Name}",
                    depth + 1))
                {
                    yield return shared;
                }
            }
        }

        #endregion
    }
}
