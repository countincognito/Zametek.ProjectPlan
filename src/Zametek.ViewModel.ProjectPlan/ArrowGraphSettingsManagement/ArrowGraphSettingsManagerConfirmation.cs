using Prism.Interactivity.InteractionRequest;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphSettingsManagerConfirmation
        : Confirmation
    {
        #region Fields

        private IList<EdgeTypeFormatModel> m_EdgeTypeFormats;

        #endregion

        #region Ctors

        public ArrowGraphSettingsManagerConfirmation(ArrowGraphSettingsModel arrowGraphSettings)
        {
            if (arrowGraphSettings == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettings));
            }
            m_EdgeTypeFormats = new List<EdgeTypeFormatModel>();
            ActivitySeverities = new ObservableCollection<IManagedActivitySeverityViewModel>();
            SetManagedActivitySeverities(arrowGraphSettings.ActivitySeverities);
            SetEdgeTypeFormats(arrowGraphSettings.EdgeTypeFormats);
        }

        #endregion

        #region Properties

        public ObservableCollection<IManagedActivitySeverityViewModel> ActivitySeverities
        {
            get;
        }

        public IEnumerable<EdgeTypeFormatModel> EdgeTypeFormats
        {
            get
            {
                return m_EdgeTypeFormats;
            }
        }

        public ArrowGraphSettingsModel ArrowGraphSettings
        {
            get
            {
                return new ArrowGraphSettingsModel
                {
                    ActivitySeverities = ActivitySeverities.Select(x => x.ActivitySeverity).ToList(),
                    EdgeTypeFormats = EdgeTypeFormats.ToList()
                };
            }
        }

        #endregion

        #region Private Methods

        private void SetManagedActivitySeverities(IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            ActivitySeverities.Clear();
            ActivitySeverities.AddRange(activitySeverities.Select(x => new ManagedActivitySeverityViewModel(x)));
        }

        private void SetEdgeTypeFormats(IEnumerable<EdgeTypeFormatModel> edgeTypeFormats)
        {
            if (edgeTypeFormats == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormats));
            }
            m_EdgeTypeFormats.Clear();
            foreach (EdgeTypeFormatModel edgeTypeFormat in edgeTypeFormats)
            {
                m_EdgeTypeFormats.Add(edgeTypeFormat);
            }
        }

        #endregion
    }
}
