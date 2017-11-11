using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public abstract class PropertyChangedPubSubViewModel
        : BindableBase
    {
        #region Fields

        private readonly IEventAggregator m_EventService;
        private readonly Guid m_InstanceId;
        private readonly HashSet<string> m_ReadablePropertyNames;
        private readonly HashSet<string> m_SubscribedPropertyNames;

        #endregion

        #region Ctors

        protected PropertyChangedPubSubViewModel(IEventAggregator eventService)
        {
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            m_InstanceId = Guid.NewGuid();
            m_ReadablePropertyNames =
                new HashSet<string>(
                    GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.CanRead)
                    .Select(x => x.Name));
            m_SubscribedPropertyNames = new HashSet<string>();
        }

        #endregion

        #region Public Methods

        public SubscriptionToken SubscribePropertyChanged(string propertyName)
        {
            return SubscribePropertyChanged(propertyName, ThreadOption.PublisherThread);
        }

        public SubscriptionToken SubscribePropertyChanged(string propertyName, ThreadOption threadOption)
        {
            return SubscribePropertyChanged(propertyName, threadOption, false);
        }

        public SubscriptionToken SubscribePropertyChanged(string propertyName, bool keepSubscriberReferenceAlive)
        {
            return SubscribePropertyChanged(propertyName, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
        }

        public SubscriptionToken SubscribePropertyChanged(string propertyName, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentNullException(nameof(propertyName));
            }
            if (!m_ReadablePropertyNames.Contains(propertyName))
            {
                throw new InvalidOperationException($"{propertyName} is not a public, readable instance property on {GetType().FullName}, instance {m_InstanceId}");
            }
            if (m_SubscribedPropertyNames.Contains(propertyName))
            {
                throw new InvalidOperationException($"{propertyName} is already subscribed on {GetType().FullName}, instance {m_InstanceId}");
            }

            // Need to create the delegates this way in order for the event aggregator to retain the weak reference.
            var action = (Action<PropertyChangedPubSubPayload>)GetType()
                .GetRuntimeMethods()
                ?.FirstOrDefault(x => string.CompareOrdinal(x.Name, nameof(SubscriptionAction)) == 0)
                ?.CreateDelegate(typeof(Action<PropertyChangedPubSubPayload>), this);

            var filter = (Predicate<PropertyChangedPubSubPayload>)GetType()
                .GetRuntimeMethods()
                ?.FirstOrDefault(x => string.CompareOrdinal(x.Name, nameof(SubscriptionFilter)) == 0)
                ?.CreateDelegate(typeof(Predicate<PropertyChangedPubSubPayload>), this);

            m_SubscribedPropertyNames.Add(propertyName);

            return m_EventService.GetEvent<PubSubEvent<PropertyChangedPubSubPayload>>()
                .Subscribe(action, threadOption, keepSubscriberReferenceAlive, filter);
        }

        #endregion

        #region Protected Methods

        protected void SubscriptionAction(PropertyChangedPubSubPayload payload)
        {
            //base.OnPropertyChanged(new PropertyChangedEventArgs(payload.PropertyName));
            RaisePropertyChanged(payload.PropertyName);
        }

        protected bool SubscriptionFilter(PropertyChangedPubSubPayload payload)
        {
            return payload.InstanceId != m_InstanceId && m_SubscribedPropertyNames.Contains(payload.PropertyName);
        }

        #endregion

        #region Overrides

        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);
            m_EventService.GetEvent<PubSubEvent<PropertyChangedPubSubPayload>>()
                .Publish(new PropertyChangedPubSubPayload(args.PropertyName, m_InstanceId));
        }

        #endregion

        #region Protected Types

        protected class PropertyChangedPubSubPayload
        {
            #region Ctors

            public PropertyChangedPubSubPayload(string propertyName, Guid instanceId)
            {
                PropertyName = propertyName;
                InstanceId = instanceId;
            }

            #endregion

            #region Properties

            public string PropertyName
            {
                get;
            }

            public Guid InstanceId
            {
                get;
            }

            #endregion
        }

        #endregion
    }
}
