using System;

namespace Zametek.Contract.ProjectPlan
{
    public interface IPropertyChangedPubSubViewModel
    {
        Guid InstanceId { get; }

        bool ContainsReadableProperty(string propertyName);
    }
}
