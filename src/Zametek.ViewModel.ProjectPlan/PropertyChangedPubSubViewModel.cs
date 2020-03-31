using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public abstract class PropertyChangedPubSubViewModel
        : BindableBase, IPropertyChangedPubSubViewModel
    {
        #region Fields

        private readonly IEventAggregator m_EventService;
        private readonly HashSet<string> m_ReadablePropertyNames;
        private readonly ConditionalWeakTable<IPropertyChangedPubSubViewModel, Dictionary<string, HashSet<string>>> m_SourceSubscribedPropertyNames;

        #endregion

        #region Ctors

        protected PropertyChangedPubSubViewModel(IEventAggregator eventService)
        {
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            // Provides a unique ID for a given instance.
            InstanceId = Guid.NewGuid();
            // The list of readable properties on the object.
            m_ReadablePropertyNames =
                new HashSet<string>(
                    GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.CanRead)
                    .Select(x => x.Name));
            // Look up for specific instances to which this object is subscribed.
            m_SourceSubscribedPropertyNames = new ConditionalWeakTable<IPropertyChangedPubSubViewModel, Dictionary<string, HashSet<string>>>();
        }

        #endregion

        #region Public Methods

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string propertyName)
        {
            return SubscribePropertyChanged(source, propertyName, propertyName);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string propertyName,
            ThreadOption threadOption)
        {
            return SubscribePropertyChanged(source, propertyName, propertyName, threadOption);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string propertyName,
            bool keepSubscriberReferenceAlive)
        {
            return SubscribePropertyChanged(source, propertyName, propertyName, keepSubscriberReferenceAlive);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string sourcePropertyName,
            string targetPropertyName)
        {
            return SubscribePropertyChanged(source, sourcePropertyName, targetPropertyName, ThreadOption.PublisherThread);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string sourcePropertyName,
            string targetPropertyName,
            ThreadOption threadOption)
        {
            return SubscribePropertyChanged(source, sourcePropertyName, targetPropertyName, threadOption, false);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string sourcePropertyName,
            string targetPropertyName,
            bool keepSubscriberReferenceAlive)
        {
            return SubscribePropertyChanged(source, sourcePropertyName, targetPropertyName, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
        }

        public SubscriptionToken SubscribePropertyChanged(
            IPropertyChangedPubSubViewModel source,
            string sourcePropertyName,
            string targetPropertyName,
            ThreadOption threadOption,
            bool keepSubscriberReferenceAlive)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (string.IsNullOrWhiteSpace(sourcePropertyName))
            {
                throw new ArgumentNullException(nameof(sourcePropertyName));
            }
            if (!source.ContainsReadableProperty(sourcePropertyName))
            {
                throw new InvalidOperationException($"{sourcePropertyName} is not a public, readable instance property on {source.GetType().FullName} (instance ID: {source.InstanceId})");
            }
            if (string.IsNullOrWhiteSpace(targetPropertyName))
            {
                throw new ArgumentNullException(nameof(targetPropertyName));
            }
            if (!ContainsReadableProperty(targetPropertyName))
            {
                throw new InvalidOperationException($"{targetPropertyName} is not a public, readable instance property on {GetType().FullName} (instance ID: {InstanceId})");
            }

            Dictionary<string, HashSet<string>> sourceSubscribedProperties = m_SourceSubscribedPropertyNames.GetOrCreateValue(source);

            if (!sourceSubscribedProperties.TryGetValue(sourcePropertyName, out HashSet<string> subscribedPropertyTargets))
            {
                subscribedPropertyTargets = new HashSet<string>();
                sourceSubscribedProperties.Add(sourcePropertyName, subscribedPropertyTargets);
            }

            if (subscribedPropertyTargets.Contains(targetPropertyName))
            {
                throw new InvalidOperationException($"{GetType().FullName} (instance ID: {InstanceId}) {targetPropertyName} property is already subscribed to {source.GetType().FullName} (instance {source.InstanceId}) {sourcePropertyName} property");
            }

            // Need to create the delegates this way in order for the event aggregator to retain the weak reference.
            var action = (Action<PropertyChangedPubSubPayload>)GetType()
                .GetRuntimeMethods()
                .First(x => string.CompareOrdinal(x.Name, nameof(SubscriptionAction)) == 0)
                .CreateDelegate(typeof(Action<PropertyChangedPubSubPayload>), this);

            var filter = (Predicate<PropertyChangedPubSubPayload>)GetType()
                .GetRuntimeMethods()
                .First(x => string.CompareOrdinal(x.Name, nameof(SubscriptionFilter)) == 0)
                .CreateDelegate(typeof(Predicate<PropertyChangedPubSubPayload>), this);

            subscribedPropertyTargets.Add(targetPropertyName);

            return m_EventService.GetEvent<PubSubEvent<PropertyChangedPubSubPayload>>()
                .Subscribe(action, threadOption, keepSubscriberReferenceAlive, filter);
        }

        #endregion

        #region Protected Methods

        protected void SubscriptionAction(PropertyChangedPubSubPayload payload)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!payload.Source.TryGetTarget(out IPropertyChangedPubSubViewModel source))
            {
                return;
            }

            // Prevent an object reacting to its own notifications.
            if (source.InstanceId == InstanceId)
            {
                return;
            }

            Dictionary<string, HashSet<string>> sourceSubscribedProperties = m_SourceSubscribedPropertyNames.GetOrCreateValue(source);

            if (!sourceSubscribedProperties.TryGetValue(payload.PropertyName, out HashSet<string> subscribedPropertyTargets))
            {
                return;
            }

            foreach (string target in subscribedPropertyTargets)
            {
                RaisePropertyChanged(target);
            }
        }

        protected bool SubscriptionFilter(PropertyChangedPubSubPayload payload)
        {
            if (payload is null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (!payload.Source.TryGetTarget(out IPropertyChangedPubSubViewModel source))
            {
                return false;
            }

            // Prevent an object reacting to its own notifications.
            if (source.InstanceId == InstanceId)
            {
                return false;
            }

            Dictionary<string, HashSet<string>> sourceSubscribedProperties = m_SourceSubscribedPropertyNames.GetOrCreateValue(source);

            // Only proceed if object is subscribed to source property name.
            if (!sourceSubscribedProperties.TryGetValue(payload.PropertyName, out HashSet<string> subscribedPropertyTargets))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Overrides

        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            base.OnPropertyChanged(args);
            m_EventService.GetEvent<PubSubEvent<PropertyChangedPubSubPayload>>()
                .Publish(new PropertyChangedPubSubPayload(args.PropertyName, new WeakReference<IPropertyChangedPubSubViewModel>(this)));
        }

        #endregion

        #region IPropertyChangedPubSubViewModel Members

        public Guid InstanceId
        {
            get;
        }

        public bool ContainsReadableProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }
            return m_ReadablePropertyNames.Contains(propertyName);
        }

        #endregion

        #region Protected Types

        protected class PropertyChangedPubSubPayload
        {
            #region Ctors

            public PropertyChangedPubSubPayload(string propertyName, WeakReference<IPropertyChangedPubSubViewModel> source)
            {
                PropertyName = propertyName;
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            #endregion

            #region Properties

            public string PropertyName
            {
                get;
            }

            public WeakReference<IPropertyChangedPubSubViewModel> Source
            {
                get;
            }

            #endregion
        }

        #endregion
    }
}
