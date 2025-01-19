using MsBox.Avalonia.ViewModels;

namespace Zametek.View.ProjectPlan
{
    public class MsBoxContextViewModel
        : MsBoxStandardViewModel
    {
        public MsBoxContextViewModel(MessageBoxContextParams @params)
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

        public object? Context { get; init; }

        public bool IsContextVisible { get; init; } = false;
    }
}
