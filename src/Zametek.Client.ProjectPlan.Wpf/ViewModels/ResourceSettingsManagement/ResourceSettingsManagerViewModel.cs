using Prism.Commands;
using Prism.Interactivity.InteractionRequest;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ResourceSettingsManagerViewModel
        : BasicConfirmationViewModel, IResourceSettingsManagerViewModel
    {
        #region Ctors

        public ResourceSettingsManagerViewModel()
            : base()
        {
            SelectedResources = new ObservableCollection<ManagedResourceViewModel>();
            OnClose = ClearSelectedResources;
            InitializeCommands();
        }

        #endregion

        #region Properties

        public ObservableCollection<ManagedResourceViewModel> SelectedResources
        {
            get;
        }

        #endregion

        #region Commands

        public DelegateCommandBase InternalSetSelectedManagedResourcesCommand
        {
            get;
            private set;
        }

        private DelegateCommandBase InternalAddManagedResourceCommand
        {
            get;
            set;
        }

        private DelegateCommandBase InternalRemoveManagedResourceCommand
        {
            get;
            set;
        }

        private void SetSelectedManagedResources(SelectionChangedEventArgs args)
        {
            if (args?.AddedItems != null)
            {
                SelectedResources.AddRange(args?.AddedItems.OfType<ManagedResourceViewModel>());
            }
            if (args?.RemovedItems != null)
            {
                foreach (var managedResourceViewModel in args?.RemovedItems.OfType<ManagedResourceViewModel>())
                {
                    SelectedResources.Remove(managedResourceViewModel);
                }
            }
            RaisePropertyChanged(nameof(SelectedResource));
            RaiseCanExecuteChangedAllCommands();
        }

        private void AddManagedResource()
        {
            DoAddManagedResource();
        }

        private bool CanAddManagedResource()
        {
            return true;
        }

        private void RemoveManagedResource()
        {
            DoRemoveManagedResource();
        }

        private bool CanRemoveManagedResource()
        {
            return SelectedResources.Any();
        }

        #endregion

        #region Public Methods

        public void DoAddManagedResource()
        {
            int resourceId = GetNextResourceId();
            Resources.Add(
                new ManagedResourceViewModel(
                    new Common.Project.v0_1_0.ResourceDto
                    {
                        Id = resourceId,
                        IsExplicitTarget = true,
                        ColorFormat = new Common.Project.v0_1_0.ColorFormatDto(),
                        UnitCost = DefaultUnitCost
                    }));
            RaisePropertyChanged(nameof(Resources));
            RaisePropertyChanged(nameof(SelectedResources));
            RaiseCanExecuteChangedAllCommands();
        }

        public void DoRemoveManagedResource()
        {
            IEnumerable<ManagedResourceViewModel> managedResources = SelectedResources.ToList();
            if (!managedResources.Any())
            {
                return;
            }
            foreach (ManagedResourceViewModel managedResource in managedResources)
            {
                Resources.Remove(managedResource);
            }
            SelectedResources.Clear();
            RaisePropertyChanged(nameof(Resources));
            RaisePropertyChanged(nameof(SelectedResources));
            RaiseCanExecuteChangedAllCommands();
        }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            SetSelectedManagedResourcesCommand =
                InternalSetSelectedManagedResourcesCommand =
                    new DelegateCommand<SelectionChangedEventArgs>(SetSelectedManagedResources);
            AddManagedResourceCommand =
                InternalAddManagedResourceCommand =
                    new DelegateCommand(AddManagedResource, CanAddManagedResource);
            RemoveManagedResourceCommand =
                InternalRemoveManagedResourceCommand =
                    new DelegateCommand(RemoveManagedResource, CanRemoveManagedResource);
        }

        private void RaiseCanExecuteChangedAllCommands()
        {
            InternalSetSelectedManagedResourcesCommand.RaiseCanExecuteChanged();
            InternalAddManagedResourceCommand.RaiseCanExecuteChanged();
            InternalRemoveManagedResourceCommand.RaiseCanExecuteChanged();
        }

        private void ClearSelectedResources()
        {
            SelectedResources.Clear();
        }

        private int GetNextResourceId()
        {
            return Resources.Select(x => x.Id).DefaultIfEmpty().Max() + 1;
        }

        #endregion

        #region Overrides

        public override INotification Notification
        {
            get
            {
                return base.Notification;
            }
            set
            {
                base.Notification = value;
                RaisePropertyChanged(nameof(Resources));
                RaisePropertyChanged(nameof(DefaultUnitCost));
                RaisePropertyChanged(nameof(DisableResources));
                RaisePropertyChanged(nameof(ActivateResources));
            }
        }

        #endregion

        #region IResourcesManagerViewModel Members

        public double DefaultUnitCost
        {
            get
            {
                var notification = (ResourceSettingsManagerConfirmation)Notification;
                if (notification != null)
                {
                    return notification.DefaultUnitCost;
                }
                return 1.0;
            }
            set
            {
                var notification = (ResourceSettingsManagerConfirmation)Notification;
                if (notification != null)
                {
                    notification.DefaultUnitCost = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool DisableResources
        {
            get
            {
                var notification = (ResourceSettingsManagerConfirmation)Notification;
                if (notification != null)
                {
                    return notification.AreDisabled;
                }
                return false;
            }
            set
            {
                var notification = (ResourceSettingsManagerConfirmation)Notification;
                if (notification != null)
                {
                    notification.AreDisabled = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ActivateResources));
                }
            }
        }

        public bool ActivateResources
        {
            get
            {
                return !DisableResources;
            }
        }

        public ObservableCollection<ManagedResourceViewModel> Resources
        {
            get
            {
                return ((ResourceSettingsManagerConfirmation)Notification).Resources;
            }
        }

        public ManagedResourceViewModel SelectedResource
        {
            get
            {
                if (SelectedResources.Count == 1)
                {
                    return SelectedResources.FirstOrDefault();
                }
                return null;
            }
        }

        public ICommand SetSelectedManagedResourcesCommand
        {
            get;
            private set;
        }

        public ICommand AddManagedResourceCommand
        {
            get;
            private set;
        }

        public ICommand RemoveManagedResourceCommand
        {
            get;
            private set;
        }

        #endregion
    }
}