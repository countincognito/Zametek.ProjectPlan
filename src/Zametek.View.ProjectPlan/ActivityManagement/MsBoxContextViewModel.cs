using MsBox.Avalonia.ViewModels;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class MsBoxContextViewModel<T>
        : MsBoxStandardViewModel
    {
        public MsBoxContextViewModel(MessageBoxContextParams<T> @params)
            : base(@params)
        {
            Context = @params.Context;

            if (Context != null)
            {
                IsContextVisible = true;
            }
            IsMarkdownVisible = @params.Markdown;
            if (!string.IsNullOrWhiteSpace(@params.ContentMessage))
            {
                IsContentMessageVisible = true;
            }
        }

        public bool IsMarkdownVisible { get; init; } = false;

        public bool IsContentMessageVisible { get; init; } = false;

        public T Context { get; init; }

        public bool IsContextVisible { get; init; } = false;
    }

    public class MsBoxContextViewModel
       : MsBoxContextViewModel<ViewModelBase>
    {
        public MsBoxContextViewModel(MessageBoxContextParams<ViewModelBase> @params)
            : base(@params)
        {
        }
    }
}
