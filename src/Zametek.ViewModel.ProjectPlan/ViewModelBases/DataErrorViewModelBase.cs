using ReactiveUI;
using System.Collections;
using System.ComponentModel;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DataErrorViewModelBase
        : ViewModelBase, INotifyDataErrorInfo
    {
        #region Fields

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public DataErrorViewModelBase()
        {
            m_ErrorsByPropertyName = [];
        }

        #endregion

        #region Protected Members

        protected void SetError(string propertyName, string error)
        {
            if (m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                if (!errorList.Contains(error))
                {
                    errorList.Add(error);
                }
            }
            else
            {
                m_ErrorsByPropertyName.Add(propertyName, [error]);
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            this.RaisePropertyChanged(nameof(HasErrors));
        }

        protected void ClearErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                errorList.Clear();
                m_ErrorsByPropertyName.Remove(propertyName);
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        protected void ClearErrors()
        {
            IList<string> propertyNames = [.. m_ErrorsByPropertyName.Keys];
            m_ErrorsByPropertyName.Clear();

            foreach (string propertyName in propertyNames)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            this.RaisePropertyChanged(nameof(HasErrors));
        }

        #endregion

        #region INotifyDataErrorInfo Members

        public bool HasErrors => m_ErrorsByPropertyName.Count != 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                return errorList;
            }
            return s_NoErrors;
        }

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        #endregion

    }
}
