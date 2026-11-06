using Avalonia;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zametek.Utility;

namespace Zametek.View.ProjectPlan
{
    public abstract class CustomSortComparer<T>
        : AvaloniaObject, IComparer<T>, IComparer where T : class
    {
        public abstract string SortMemberPath
        {
            get;
            set;
        }

        public static object? GetNestedPropertyValue(
            object source,
            string propertyName)
        {
            string[] parts = propertyName.Split('.');

            if (parts.Length > 1)
            {
                PropertyInfo? baseTypeProp = source.GetType().GetProperty(parts.First());
                if (baseTypeProp is null)
                {
                    return null;
                }

                object? nestedSource = baseTypeProp.GetValue(source);
                if (nestedSource is null)
                {
                    return null;
                }

                return GetNestedPropertyValue(nestedSource, parts.Skip(1).First());
            }

            return source.GetType().GetProperty(propertyName)?.GetValue(source);
        }

        public int Compare(T? x, T? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            object? X = GetNestedPropertyValue(x, SortMemberPath);
            object? Y = GetNestedPropertyValue(y, SortMemberPath);

            int returnValue = 0;

            if (X is not null)
            {
                if (Y is not null
                    && X.GetType().Equals(Y.GetType()))
                {
                    X.TypeSwitchOn()
                        .Case<IComparable>(_ => returnValue = ((IComparable)X).CompareTo((IComparable)Y));
                }
                else
                {
                    returnValue = 1;
                }
            }
            else
            {
                if (Y is not null)
                {
                    returnValue = -1;
                }
            }

            return returnValue;
        }

        public int Compare(object? x, object? y) => Compare(x as T, y as T);
    }
}
