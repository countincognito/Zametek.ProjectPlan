using MsBox.Avalonia.Dto;

namespace Zametek.View.ProjectPlan
{
    public class MessageBoxContextParams<T> :
        MessageBoxStandardParams
    {
        public MessageBoxContextParams(T context)
        {
            Context = context;
        }

        public T Context { get; init; }
    }
}
