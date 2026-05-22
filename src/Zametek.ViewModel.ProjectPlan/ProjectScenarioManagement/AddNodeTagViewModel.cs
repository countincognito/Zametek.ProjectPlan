using ReactiveUI;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class AddNodeTagViewModel
        : DataErrorViewModelBase, IAddNodeTagViewModel
    {
        #region Fields

        private readonly HashSet<string> m_ExistingNames;

        #endregion

        #region Ctors

        public AddNodeTagViewModel(HashSet<string> existingNames)
            : base()
        {
            m_Tag = string.Empty;
            m_ExistingNames = existingNames;
        }

        #endregion

        #region Private Members

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
    }
}
