using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class NodeNameViewModel
        : DataErrorViewModelBase, INodeNameViewModel
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
            : base()
        {
            m_Name = suggestedName;
            m_ExistingNames = existingNames;
            m_SuggestNodeNameFunc = suggestNodeNameFunc;
            m_ErrorsByPropertyName = [];
            m_Name = m_SuggestNodeNameFunc(m_Name, m_ExistingNames);
        }

        #endregion

        #region Private Members

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
    }
}
