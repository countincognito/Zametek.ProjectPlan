using Dock.Model.Mvvm.Controls;
using ReactiveUI;
using System.ComponentModel;
using System.Reactive;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ToolViewModelBase
        : Tool, IReactiveObject
    {
        private readonly Lazy<Unit> _propertyChangingEventsSubscribed;
        private readonly Lazy<Unit> _propertyChangedEventsSubscribed;

        public ToolViewModelBase()
        {
            _propertyChangingEventsSubscribed = new Lazy<Unit>(
                                                               () =>
                                                               {
                                                                   this.SubscribePropertyChangingEvents();
                                                                   return Unit.Default;
                                                               },
                                                               LazyThreadSafetyMode.PublicationOnly);
            _propertyChangedEventsSubscribed = new Lazy<Unit>(
                                                              () =>
                                                              {
                                                                  this.SubscribePropertyChangedEvents();
                                                                  return Unit.Default;
                                                              },
                                                              LazyThreadSafetyMode.PublicationOnly);
        }

        public event PropertyChangingEventHandler? PropertyChanging
        {
            add
            {
                _ = _propertyChangingEventsSubscribed.Value;
                PropertyChangingHandler += value;
            }
            remove => PropertyChangingHandler -= value;
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _ = _propertyChangedEventsSubscribed.Value;
                PropertyChangedHandler += value;
            }
            remove => PropertyChangedHandler -= value;
        }

        private event PropertyChangingEventHandler? PropertyChangingHandler;

        private event PropertyChangedEventHandler? PropertyChangedHandler;

        void IReactiveObject.RaisePropertyChanging(PropertyChangingEventArgs args) =>
            PropertyChangingHandler?.Invoke(this, args);

        void IReactiveObject.RaisePropertyChanged(PropertyChangedEventArgs args) =>
            PropertyChangedHandler?.Invoke(this, args);
    }
}
