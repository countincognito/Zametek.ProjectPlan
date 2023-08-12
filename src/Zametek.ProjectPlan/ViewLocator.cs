using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;
using Splat;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class ViewLocator
        : IDataTemplate
    {
        public Control Build(object data)
        {
            var name = data.GetType().AssemblyQualifiedName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Locator.Current.GetService(type) ?? (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                return new TextBlock { Text = "Not Found: " + name };
            }
        }

        public bool Match(object data)
        {
            return data is ViewModelBase || data is ToolViewModelBase;
        }
    }
}
