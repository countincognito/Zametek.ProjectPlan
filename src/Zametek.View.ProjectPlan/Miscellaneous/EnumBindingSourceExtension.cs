﻿using Avalonia.Markup.Xaml;
using System;

// https://github.com/brianlagunas/BindingEnumsInWpf

namespace Zametek.View.ProjectPlan
{
    public class EnumBindingSourceExtension
        : MarkupExtension
    {
        #region Ctors

        public EnumBindingSourceExtension()
        {
        }

        public EnumBindingSourceExtension(Type enumType)
        {
            EnumType = enumType;
        }

        #endregion

        #region Properties

        private Type? m_EnumType;
        public Type? EnumType
        {
            get
            {
                return m_EnumType;
            }
            set
            {
                if (value is not null)
                {
                    if (m_EnumType != value)
                    {
                        Type? enumType = Nullable.GetUnderlyingType(value) ?? value;
                        if (!enumType.IsEnum)
                        {
                            throw new ArgumentException("Type must be for an Enum");
                        }
                    }
                    m_EnumType = value;
                }
            }
        }

        #endregion

        #region Overrides

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (m_EnumType is null)
            {
                throw new InvalidOperationException("The EnumType must be specified");
            }
            Type? actualEnumType = Nullable.GetUnderlyingType(m_EnumType) ?? m_EnumType;
            Array enumValues = Enum.GetValues(actualEnumType);

            if (m_EnumType == actualEnumType)
            {
                return enumValues;
            }
            Array tempArray = Array.CreateInstance(actualEnumType, enumValues.Length + 1);
            enumValues.CopyTo(tempArray, 1);
            return tempArray;
        }

        #endregion
    }
}
