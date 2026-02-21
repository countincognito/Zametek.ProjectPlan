using ReactiveUI;
using System.Collections;
using System.ComponentModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AddNodeTagViewModel
        : ViewModelBase, IAddNodeTagViewModel, INotifyDataErrorInfo
    {
        #region Fields

        private readonly HashSet<string> m_ExistingNames;

        private static readonly string[] s_NoErrors = [];
        private readonly Dictionary<string, List<string>> m_ErrorsByPropertyName;

        #endregion

        #region Ctors

        public AddNodeTagViewModel(HashSet<string> existingNames)
        {
            m_Tag = string.Empty;
            m_ExistingNames = existingNames;
            m_ErrorsByPropertyName = [];
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

        private void ValidateTag(string tag)
        {
            ClearErrors(nameof(Tag));

            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    SetError(nameof(Tag), Resource.ProjectPlan.Messages.Message_TagCannotBeEmpty);
                }
            }
            {
                if (m_ExistingNames.Contains(tag))
                {
                    SetError(nameof(Tag), Resource.ProjectPlan.Messages.Message_TagAlreadyExists);
                }
            }
        }

        #endregion

        #region IAddNodeTagViewModel Members

        private string m_Tag;
        public string Tag
        {
            get => m_Tag;
            set
            {
                m_Tag = value;
                ValidateTag(m_Tag);
                this.RaisePropertyChanged();
            }
        }

        public void RunValidation()
        {
            ValidateTag(Tag);
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
