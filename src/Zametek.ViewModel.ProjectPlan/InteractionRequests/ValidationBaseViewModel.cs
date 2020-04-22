using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Controls;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ValidationBaseViewModel
        : BasicConfirmationViewModel, IDataErrorInfo
    {
        #region Fields

        private readonly IDictionary<string, string> m_Errors;
        private readonly IDictionary<PropertyInfo, IList<ValidationRule>> m_PropertiesToValidate;
        private readonly Type m_ThisType;
        private string m_FocusedProperty;

        #endregion

        #region Ctors

        public ValidationBaseViewModel()
        {
            m_ThisType = GetType();
            m_Errors = new Dictionary<string, string>();
            m_PropertiesToValidate = new Dictionary<PropertyInfo, IList<ValidationRule>>();
        }

        #endregion

        #region Properties

        public Type ThisType => m_ThisType;

        public bool IsValid => !m_Errors.Any();

        public string FocusedProperty
        {
            get
            {
                return m_FocusedProperty;
            }
            set
            {
                m_FocusedProperty = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Protected Methods

        protected void AddPropertyToValidate<T>(
            Expression<Func<T>> propertyExpression,
            ValidationRule validation)
        {
            string propertyName = PropertySupport.ExtractPropertyName(propertyExpression);
            PropertyInfo property = ThisType.GetProperty(propertyName);
            if (property != null)
            {
                IList<ValidationRule> validations;
                if (!m_PropertiesToValidate.TryGetValue(property, out validations))
                {
                    validations = new List<ValidationRule>(new[] { validation });
                    m_PropertiesToValidate.Add(property, validations);
                }
                if (!validations.Contains(validation))
                {
                    validations.Add(validation);
                }
            }
        }

        protected void ClearPropertyToValidate<T>(Expression<Func<T>> propertyExpression)
        {
            string propertyName = PropertySupport.ExtractPropertyName(propertyExpression);
            PropertyInfo property = ThisType.GetProperty(propertyName);
            if (property != null)
            {
                m_PropertiesToValidate.Remove(property);
            }
        }

        protected void CheckForErrors()
        {
            m_Errors.Clear();
            foreach (KeyValuePair<PropertyInfo, IList<ValidationRule>> item in m_PropertiesToValidate)
            {
                foreach (ValidationRule validationRule in item.Value)
                {
                    ValidationResult result = validationRule.Validate(item.Key.GetValue(this), CultureInfo.CurrentCulture);
                    if (!result.IsValid)
                    {
                        m_Errors.Add(item.Key.Name, result.ErrorContent.ToString());
                        break;
                    }
                }
            }
        }

        protected void Validate(bool changeFocus)
        {
            CheckForErrors();
            if (changeFocus && m_Errors.Count > 0)
            {
                FocusedProperty = m_Errors.First().Key;
            }
            foreach (PropertyInfo key in m_PropertiesToValidate.Keys)
            {
                RaisePropertyChanged(key.Name);
            }
            //m_ValidationActive = true;
        }

        #endregion

        #region Overrides

        public override void Confirm()
        {
            Validate(true);
            if (IsValid)
            {
                base.Confirm();
            }
        }

        #endregion

        #region IDataErrorInfo Members

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                if (m_Errors.ContainsKey(columnName))
                {
                    return m_Errors[columnName];
                }
                return string.Empty;
            }
        }

        #endregion
    }
}
