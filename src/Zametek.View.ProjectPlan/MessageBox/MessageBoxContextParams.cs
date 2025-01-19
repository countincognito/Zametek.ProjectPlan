using MsBox.Avalonia.Dto;

namespace Zametek.View.ProjectPlan
{
    public class MessageBoxContextParams :
        MessageBoxStandardParams
    {
        public MessageBoxContextParams(object context)
        {
            Context = context;
        }

        public object Context { get; init; }
    }
}
