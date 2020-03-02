using Prism.Mvvm;
using System;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ManagedResourceViewModel
        : BindableBase
    {
        #region Fields

        private readonly Common.Project.v0_1_0.ResourceDto m_Resource;

        #endregion

        #region Ctors

        public ManagedResourceViewModel(Common.Project.v0_1_0.ResourceDto resource)
        {
            m_Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        }

        #endregion

        #region Properties

        public Common.Project.v0_1_0.ResourceDto ResourceDto => m_Resource;

        public int Id => m_Resource.Id;

        public string Name
        {
            get
            {
                return m_Resource.Name;
            }
            set
            {
                m_Resource.Name = value;
                RaisePropertyChanged();
            }
        }

        public bool IsExplicitTarget
        {
            get
            {
                return m_Resource.IsExplicitTarget;
            }
            set
            {
                m_Resource.IsExplicitTarget = value;
                RaisePropertyChanged();
            }
        }

        public InterActivityAllocationType InterActivityAllocationType
        {
            get
            {
                return m_Resource.InterActivityAllocationType;
            }
            set
            {
                m_Resource.InterActivityAllocationType = value;
                RaisePropertyChanged();
            }
        }

        public double UnitCost
        {
            get
            {
                return m_Resource.UnitCost;
            }
            set
            {
                m_Resource.UnitCost = value;
                RaisePropertyChanged();
            }
        }

        public int DisplayOrder
        {
            get
            {
                return m_Resource.DisplayOrder;
            }
            set
            {
                m_Resource.DisplayOrder = value;
                RaisePropertyChanged();
            }
        }

        public Common.Project.v0_1_0.ColorFormatDto ColorFormat
        {
            get
            {
                return m_Resource.ColorFormat;
            }
            set
            {
                m_Resource.ColorFormat = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
