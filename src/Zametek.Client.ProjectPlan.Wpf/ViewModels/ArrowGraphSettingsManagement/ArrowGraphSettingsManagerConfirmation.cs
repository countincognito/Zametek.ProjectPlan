using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ArrowGraphSettingsManagerConfirmation
        : Confirmation
    {
        #region Fields

        private IList<Common.Project.v0_1_0.EdgeTypeFormatDto> m_EdgeTypeFormats;

        #endregion

        #region Ctors

        public ArrowGraphSettingsManagerConfirmation(Common.Project.v0_1_0.ArrowGraphSettingsDto arrowGraphSettings)
        {
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            m_EdgeTypeFormats = new List<Common.Project.v0_1_0.EdgeTypeFormatDto>();
            ActivitySeverities = new ObservableCollection<ManagedActivitySeverityViewModel>();
            SetManagedActivitySeverities(arrowGraphSettings.ActivitySeverities);
            SetEdgeTypeFormats(arrowGraphSettings.EdgeTypeFormats);
        }

        #endregion

        #region Properties

        public ObservableCollection<ManagedActivitySeverityViewModel> ActivitySeverities
        {
            get;
        }

        public IEnumerable<Common.Project.v0_1_0.ActivitySeverityDto> ActivitySeverityDtos
        {
            get
            {
                return ActivitySeverities.Select(x => x.ActivitySeverityDto);
            }
        }

        public IEnumerable<Common.Project.v0_1_0.EdgeTypeFormatDto> EdgeTypeFormatDtos
        {
            get
            {
                return m_EdgeTypeFormats;
            }
        }

        public Common.Project.v0_1_0.ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get
            {
                return new Common.Project.v0_1_0.ArrowGraphSettingsDto
                {
                    ActivitySeverities = ActivitySeverityDtos.ToList(),
                    EdgeTypeFormats = EdgeTypeFormatDtos.ToList()
                };
            }
        }

        #endregion

        #region Private Methods

        private void SetManagedActivitySeverities(IEnumerable<Common.Project.v0_1_0.ActivitySeverityDto> activitySeverities)
        {
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            ActivitySeverities.Clear();
            ActivitySeverities.AddRange(activitySeverities.Select(x => new ManagedActivitySeverityViewModel(x)));
        }

        private void SetEdgeTypeFormats(IEnumerable<Common.Project.v0_1_0.EdgeTypeFormatDto> edgeTypeFormats)
        {
            if (edgeTypeFormats == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormats));
            }
            m_EdgeTypeFormats.Clear();
            foreach (Common.Project.v0_1_0.EdgeTypeFormatDto edgeTypeFormatDto in edgeTypeFormats)
            {
                m_EdgeTypeFormats.Add(edgeTypeFormatDto);
            }
        }

        #endregion
    }
}
