using Prism.Mvvm;
using System;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedResourceViewModel
        : BindableBase, IManagedResourceViewModel
    {
        #region Fields

        private readonly ResourceModel m_Resource;

        #endregion

        #region Ctors

        public ManagedResourceViewModel(ResourceModel resource)
        {
            m_Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        }

        #endregion

        #region Properties

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

        public ColorFormatModel ColorFormat
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

        #region IManagedResourceViewModel Members

        public int Id => m_Resource.Id;

        public ResourceModel Resource => m_Resource;

        #endregion
    }
}
