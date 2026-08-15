using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Splat;
using System;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public class ViewLocator
        : IDataTemplate
    {
        public static Control BuildView(object? data)
        {
            if (data is not null)
            {
                string? assemblyQualifiedName = data.GetType().AssemblyQualifiedName;
                if (assemblyQualifiedName is null)
                {
                    return new TextBlock { Text = "Not Found: unable to resolve assembly-qualified name" };
                }
                var name = assemblyQualifiedName.Replace("ViewModel", "View");
                var type = Type.GetType(name);

                if (type != null)
                {
                    Control? resolved = Locator.Current.GetService(type) as Control
                        ?? Activator.CreateInstance(type) as Control;
                    return resolved ?? new TextBlock { Text = "Not Found: " + name };
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

        public Control Build(object? data)
        {
            return BuildView(data);
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase || data is ToolViewModelBase;
        }
    }
}
