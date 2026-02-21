using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class NodeNameViewModel
        : ViewModelBase, INodeNameViewModel, INotifyDataErrorInfo
    {
        #region Fields

        private readonly HashSet<string> m_ExistingNames;
        private readonly Func<string, HashSet<string>, string> m_SuggestNodeNameFunc;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public NodeNameViewModel(
            string suggestedName,
            HashSet<string> existingNames,
            Func<string, HashSet<string>, string> suggestNodeNameFunc)
        {
            m_Name = suggestedName;
            m_ExistingNames = existingNames;
            m_SuggestNodeNameFunc = suggestNodeNameFunc;
            m_ErrorsByPropertyName = [];
            m_Name = m_SuggestNodeNameFunc(m_Name, m_ExistingNames);
        }

        #endregion

        #region Private Members

        private void SetError(string propertyName, string error)
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

        private void ClearErrors(string? propertyName)
        {
            if (!string.IsNullOrWhiteSpace(propertyName)
                && m_ErrorsByPropertyName.TryGetValue(propertyName, out List<string>? errorList))
            {
                errorList.Clear();
            }
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        private void ClearErrors()
        {
            IList<string> propertyNames = [.. m_ErrorsByPropertyName.Keys];
            m_ErrorsByPropertyName.Clear();

            foreach (string propertyName in propertyNames)
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            this.RaisePropertyChanged(nameof(HasErrors));
        }

        private void ValidateName(string name)
        {
            ClearErrors(nameof(Name));

            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    SetError(nameof(Name), Resource.ProjectPlan.Messages.Message_NameCannotBeEmpty);
                }
            }
            {
                if (m_ExistingNames.Contains(name))
                {
                    SetError(nameof(Name), Resource.ProjectPlan.Messages.Message_NameAlreadyExists);
                }
            }
        }

        #endregion

        #region IAddNodeViewModel Members

        private string m_Name;
        public string Name
        {
            get => m_Name;
            set
            {
                m_Name = value;
                ValidateName(m_Name);
                this.RaisePropertyChanged();
            }
        }

        public void RunValidation()
        {
            ValidateName(Name);
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
