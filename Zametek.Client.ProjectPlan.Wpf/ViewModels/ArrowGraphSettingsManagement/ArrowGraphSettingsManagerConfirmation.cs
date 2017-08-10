using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zametek.Common.Project;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ArrowGraphSettingsManagerConfirmation
        : Confirmation
    {
        #region Fields

        private IList<EdgeTypeFormatDto> m_EdgeTypeFormats;

        #endregion

        #region Ctors

        public ArrowGraphSettingsManagerConfirmation(ArrowGraphSettingsDto arrowGraphSettings)
        {
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            m_EdgeTypeFormats = new List<EdgeTypeFormatDto>();
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

        public IEnumerable<ActivitySeverityDto> ActivitySeverityDtos
        {
            get
            {
                return ActivitySeverities.Select(x => x.ActivitySeverityDto);
            }
        }

        public IEnumerable<EdgeTypeFormatDto> EdgeTypeFormatDtos
        {
            get
            {
                return m_EdgeTypeFormats;
            }
        }

        public ArrowGraphSettingsDto ArrowGraphSettingsDto
        {
            get
            {
                return new ArrowGraphSettingsDto
                {
                    ActivitySeverities = ActivitySeverityDtos.ToList(),
                    EdgeTypeFormats = EdgeTypeFormatDtos.ToList()
                };
            }
        }

        #endregion

        #region Private Methods

        private void SetManagedActivitySeverities(IEnumerable<ActivitySeverityDto> activitySeverities)
        {
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            ActivitySeverities.Clear();
            ActivitySeverities.AddRange(activitySeverities.Select(x => new ManagedActivitySeverityViewModel(x)));
        }

        private void SetEdgeTypeFormats(IEnumerable<EdgeTypeFormatDto> edgeTypeFormats)
        {
            if (edgeTypeFormats == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormats));
            }
            m_EdgeTypeFormats.Clear();
            foreach (EdgeTypeFormatDto edgeTypeFormatDto in edgeTypeFormats)
            {
                m_EdgeTypeFormats.Add(edgeTypeFormatDto);
            }
        }

        #endregion
    }
}
