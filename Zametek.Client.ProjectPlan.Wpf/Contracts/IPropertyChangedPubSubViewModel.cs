using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IPropertyChangedPubSubViewModel
    {
        Guid InstanceId
        {
            get;
        }

        bool ContainsReadableProperty(string propertyName);
    }
}
