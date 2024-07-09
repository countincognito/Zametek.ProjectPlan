using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Splat;
using System;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class ViewLocator
        : IDataTemplate
    {
        public Control Build(object? data)
        {
            if (data is not null)
            {
                var name = data.GetType().AssemblyQualifiedName!.Replace("ViewModel", "View");
                var type = Type.GetType(name);

                if (type != null)
                {
                    return Locator.Current.GetService(type) as Control ?? (Control)Activator.CreateInstance(type)!;
                }
                else
                {
                    return new TextBlock { Text = "Not Found: " + name };
                }
            }
            else
            {
                return new TextBlock { Text = "Data object is null" };
            }
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase || data is ToolViewModelBase;
        }
    }
}
