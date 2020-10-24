using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Zametek.Common.ProjectPlan
{
    public static class ResourceScenarioBuilder
    {
        public static IReadOnlyList<ResourceSettingsModel> Build(ResourceSettingsModel settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.Resources.All(x => x.IsExplicitTarget))
            {
                throw new ArgumentException("At least one resource must not be an Explicit Target.");
            }

            var scenarios = new List<ResourceSettingsModel>();

            var resourceIdsToToggle = settings.Resources
                .Where(x => !x.IsExplicitTarget)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => x.Id)
                .ToList();

            for (var i = 0; i < resourceIdsToToggle.Count; i++)
            {
                var explicitIds = resourceIdsToToggle.GetRange(i + 1, resourceIdsToToggle.Count - i - 1);
                var implicitIds = resourceIdsToToggle.GetRange(0, i);

                var clones = settings.Resources.Select(Clone).ToList();

                clones
                    .ForEach(x =>
                    {
                        if (explicitIds.Contains(x.Id))
                        {
                            x.IsExplicitTarget = true;
                        }
                        else if (implicitIds.Contains(x.Id))
                        {
                            x.IsExplicitTarget = false;
                        }
                    });

                var scenario = new ResourceSettingsModel
                {
                    DefaultUnitCost = settings.DefaultUnitCost,
                    Resources = clones
                };

                scenarios.Add(scenario);
            }

            return scenarios;
        }

        private static ResourceModel Clone(ResourceModel resource)
        {
            var formatter = new BinaryFormatter();
            using var ms = new MemoryStream();
            formatter.Serialize(ms, resource);
            ms.Position = 0;
            return (ResourceModel)formatter.Deserialize(ms);
        }
    }
}
