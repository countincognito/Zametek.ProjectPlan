using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The live plan: the activities, the compiler that holds them as its graph, and the views of them that the UI binds
    /// to. It makes, removes, updates and clears the activities, pushes the inputs of the core into them, copies them for a
    /// compilation or a simulation, and gives the results of a compilation back to them.
    /// </summary>
    /// <remarks>
    /// It has no lock of its own, but the leaf lock that it shares with every activity (<see cref="m_ActivityDataLock"/>).
    /// The core calls the members that change the list of the activities, and the members that copy it, with the lock of
    /// the core held (ARCHITECTURE section 7 rules 6 and 11).
    /// </remarks>
    internal sealed class PlanGraph
        : IDisposable
    {
        #region Fields

        /// <summary>
        /// The leaf lock shared by every activity view model, guarding the activity state
        /// a compilation reads and writes. Held only around the snapshot and publish steps
        /// here, and around the individual writes inside the activities themselves.
        /// </summary>
        /// <remarks>
        /// Strictly inside the lock of the core in the lock order, and the only lock the
        /// activities take, so it cannot form a cycle. Never hold it while raising a
        /// change notification or calling out to anything else - see ARCHITECTURE
        /// section 7 rules 6 and 11.
        /// </remarks>
        private readonly Lock m_ActivityDataLock;

        // The compiler of the live graph. The activities are its nodes, and it keeps the structure of the graph for
        // everything between the compilations. A compilation never uses it: it works on a compiler of its own.
        private readonly VertexGraphCompiler m_VertexGraphCompiler;

        private readonly ProjectPlanMapper m_Mapper;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly SubscriptionLifetime m_SubscriptionLifetime;

        private readonly SourceList<IManagedActivityViewModel> m_Activities;
        public IReadOnlyList<IManagedActivityViewModel> RawActivities => m_Activities.Items;

        private readonly ObservableCollectionExtended<IManagedActivityViewModel> m_BoundActivities;
        private readonly ReadOnlyObservableCollection<IManagedActivityViewModel> m_ReadOnlyActivities;
        public ReadOnlyObservableCollection<IManagedActivityViewModel> Activities => m_ReadOnlyActivities;

        private readonly ObservableCollectionExtended<IManagedActivityViewModel> m_OrderableActivities;
        public ObservableCollection<IManagedActivityViewModel> OrderableActivities => m_OrderableActivities;

        // The binds of the views, made by StartBinds and not by the constructor: see IStartSubscriptions.
        private IDisposable? m_ReadOnlyActivitiesSub;
        private IDisposable? m_OrderableActivitiesSub;

        #endregion

        #region Ctors

        public PlanGraph(
            ProjectPlanMapper mapper,
            IDateTimeCalculator dateTimeCalculator,
            SubscriptionLifetime subscriptionLifetime)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(subscriptionLifetime);
            m_Mapper = mapper;
            m_DateTimeCalculator = dateTimeCalculator;
            m_SubscriptionLifetime = subscriptionLifetime;
            m_ActivityDataLock = new();
            m_VertexGraphCompiler = new VertexGraphCompiler();
            m_Activities = new();
            m_OrderableActivities = [];

            // The read-only view of the source list is made here, empty, so that it can be read and observed from
            // the start. The bind that fills it delivers on the UI thread and is made by StartBinds.
            m_BoundActivities = [];
            m_ReadOnlyActivities = new ReadOnlyObservableCollection<IManagedActivityViewModel>(m_BoundActivities);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The start time of the project, as the live compiler works it out from the activities that it holds.
        /// </summary>
        public int StartTime => m_VertexGraphCompiler.StartTime;

        /// <summary>
        /// The finish time of the project, as the live compiler works it out from the activities that it holds.
        /// </summary>
        public int FinishTime => m_VertexGraphCompiler.FinishTime;

        #endregion

        #region Methods

        /// <summary>
        /// The next id that is free in the live graph. Call it with the lock of the core held.
        /// </summary>
        public int GetNextActivityId()
        {
            return m_VertexGraphCompiler.GetNextActivityId();
        }

        /// <summary>
        /// Makes the activities of the models, in the order of their display orders and then of their ids, and adds the
        /// ones that the live compiler accepts. The others are disposed. Call it with the lock of the core held.
        /// </summary>
        /// <param name="owner">The core, which each activity reads its settings from.</param>
        /// <param name="projectStart">The start of the project that each activity is made with.</param>
        /// <param name="dependentActivityModels">The models of the activities to add.</param>
        public void AddActivities(
            ICoreViewModel owner,
            DateTimeOffset projectStart,
            IEnumerable<DependentActivityModel> dependentActivityModels)
        {
            m_Activities.Edit(activities =>
            {
                IOrderedEnumerable<DependentActivityModel> orderedDependentActivityModels = dependentActivityModels
                    .OrderBy(x => x.Activity.DisplayOrder)
                    .ThenBy(x => x.Activity.Id);

                foreach (DependentActivityModel dependentActivity in orderedDependentActivityModels)
                {
                    var activity = new ManagedActivityViewModel(
                        owner,
                        m_Mapper.ToDependentActivity(dependentActivity),
                        m_DateTimeCalculator,
                        m_VertexGraphCompiler,
                        m_ActivityDataLock,
                        projectStart,
                        dependentActivity.Activity.Trackers,
                        dependentActivity.Activity.MinimumEarliestStartDateTime,
                        dependentActivity.Activity.MaximumLatestFinishDateTime);

                    // Started as it is made when the core has been started; a host that has not started it
                    // (the headless engine) gets activities that do nothing by themselves.
                    if (m_SubscriptionLifetime.IsStarted)
                    {
                        activity.StartSubscriptions();
                    }

                    if (m_VertexGraphCompiler.AddActivity(activity))
                    {
                        activities.Add(activity);
                    }
                    else
                    {
                        activity.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// Removes the activities of the ids from the live compiler and from the list, and disposes them. Call it with the
        /// lock of the core held.
        /// </summary>
        public void RemoveActivities(IEnumerable<int> dependentActivityIds)
        {
            m_Activities.Edit(activities =>
            {
                IEnumerable<IManagedActivityViewModel> dependentActivities = [.. RawActivities.Where(x => dependentActivityIds.Contains(x.Id))];

                foreach (IManagedActivityViewModel dependentActivity in dependentActivities)
                {
                    if (m_VertexGraphCompiler.RemoveActivity(dependentActivity.Id))
                    {
                        activities.Remove(dependentActivity);
                        dependentActivity.Dispose();
                    }
                }
            });
        }

        /// <summary>
        /// Applies the updates to the activities that they name, each of them in one edit, muted. Call it with the lock
        /// of the core held.
        /// </summary>
        public void UpdateActivities(IEnumerable<UpdateDependentActivityModel> updateModels)
        {
            m_Activities.Edit(activities =>
            {
                Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                foreach (UpdateDependentActivityModel updateModel in updateModels)
                {
                    if (activityLookup.TryGetValue(updateModel.Id, out IManagedActivityViewModel? activity))
                    {
                        if (activity is IEditableObject editable)
                        {
                            activity.IsEditMuted = true;
                            editable.BeginEdit();

                            if (updateModel.IsNameEdited)
                            {
                                activity.Name = updateModel.Name;
                            }
                            if (updateModel.IsNotesEdited)
                            {
                                activity.Notes = updateModel.Notes;
                            }
                            if (updateModel.IsTargetWorkStreamsEdited)
                            {
                                activity.WorkStreamSelector.SetSelectedTargetWorkStreams([.. updateModel.TargetWorkStreams]);
                            }
                            if (updateModel.IsTargetResourcesEdited)
                            {
                                activity.ResourceSelector.SetSelectedTargetResources([.. updateModel.TargetResources]);
                            }
                            if (updateModel.IsTargetResourceOperatorEdited)
                            {
                                activity.TargetResourceOperator = updateModel.TargetResourceOperator;
                            }
                            if (updateModel.IsHasNoCostEdited)
                            {
                                activity.HasNoCost = updateModel.HasNoCost;
                            }
                            if (updateModel.IsHasNoBillingEdited)
                            {
                                activity.HasNoBilling = updateModel.HasNoBilling;
                            }
                            if (updateModel.IsHasNoEffortEdited)
                            {
                                activity.HasNoEffort = updateModel.HasNoEffort;
                            }
                            if (updateModel.IsHasNoRiskEdited)
                            {
                                activity.HasNoRisk = updateModel.HasNoRisk;
                            }
                            if (updateModel.IsOverrideColorEdited)
                            {
                                activity.OverrideColor = updateModel.OverrideColor;
                            }
                            if (updateModel.IsColorFormatEdited)
                            {
                                activity.ColorFormat = updateModel.ColorFormat;
                            }

                            editable.EndEdit();
                            activity.IsEditMuted = false;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Disposes every activity, empties the list and resets the live compiler. Call it with the lock of the core held.
        /// </summary>
        public void ClearActivities()
        {
            m_Activities.Edit(activities =>
            {
                foreach (IManagedActivityViewModel activity in RawActivities)
                {
                    activity.Dispose();
                }
                activities.Clear();

                m_VertexGraphCompiler.Reset();
            });
        }

        /// <summary>
        /// Sets the dependencies of an activity in the live graph. Call it with the lock of the core held.
        /// </summary>
        public void SetActivityDependencies(int activityId, HashSet<int> dependencies, HashSet<int> planningDependencies)
        {
            m_VertexGraphCompiler.SetActivityDependencies(activityId, dependencies, planningDependencies);
        }

        /// <summary>
        /// Gives the activities and the live compiler the dependencies that the reduction of the copies of the activities
        /// left. Call it with the lock of the core held.
        /// </summary>
        /// <remarks>
        /// The reduction took away a dependency because the others imply it, and the others are those it saw. It is
        /// applied only if the plan is as it was copied: every activity is the one that was copied, and holds the
        /// dependencies that it held then. An edit made meanwhile could make a dependency that the reduction took away
        /// needed again. When the plan is not as it was copied nothing is changed.
        /// <para>
        /// The check and the changes are made under <see cref="m_ActivityDataLock"/> together, so that no edit comes
        /// between them. The changes announce nothing: the compilation that follows publishes its results, and with them
        /// the strings of the dependencies.
        /// </para>
        /// </remarks>
        /// <param name="reduction">What the reduction of the copies gave.</param>
        /// <param name="sources">The live activity that each copy was made from.</param>
        /// <returns>Whether the dependencies were applied.</returns>
        public bool TryApplyReduction(TransitiveReduction reduction, IReadOnlyDictionary<int, IManagedActivityViewModel> sources)
        {
            ArgumentNullException.ThrowIfNull(reduction);
            ArgumentNullException.ThrowIfNull(sources);

            lock (m_ActivityDataLock)
            {
                Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                if (activityLookup.Count != sources.Count)
                {
                    return false;
                }

                foreach ((int activityId, IManagedActivityViewModel source) in sources)
                {
                    if (!activityLookup.TryGetValue(activityId, out IManagedActivityViewModel? activity)
                        || !ReferenceEquals(activity, source)
                        || !reduction.Before.TryGetValue(activityId, out ActivityDependencies? before)
                        || !ActivityDependencies.Of(activity).IsSameAs(before))
                    {
                        return false;
                    }
                }

                foreach ((int activityId, ActivityDependencies after) in reduction.After)
                {
                    if (!after.IsSameAs(reduction.Before[activityId]))
                    {
                        m_VertexGraphCompiler.SetActivityDependencies(activityId, [.. after.Dependencies], [.. after.PlanningDependencies]);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Numbers the activities in the order that the UI shows them. Call it with the lock of the core held.
        /// </summary>
        public void SetDisplayOrders()
        {
            // Mark the display order as it was left.
            for (int i = 0; i < OrderableActivities.Count; i++)
            {
                OrderableActivities[i].DisplayOrder = i;
            }
        }

        /// <summary>
        /// Pushes the start of the project into every activity, so that it measures the times that it constrains from it.
        /// </summary>
        public void SetProjectStart(DateTimeOffset projectStart)
        {
            foreach (IManagedActivityViewModel activity in RawActivities)
            {
                activity.SetProjectStart(projectStart);
            }
        }

        /// <summary>
        /// Pushes the resource settings into every activity, so that it reconciles its target resources with them.
        /// </summary>
        public void SetResourceSettings(ResourceSettingsModel resourceSettings)
        {
            foreach (IManagedActivityViewModel activity in RawActivities)
            {
                activity.SetResourceSettings(resourceSettings);
            }
        }

        /// <summary>
        /// Pushes the work stream settings into every activity, so that it reconciles its target work streams with them.
        /// </summary>
        public void SetWorkStreamSettings(WorkStreamSettingsModel workStreamSettings)
        {
            foreach (IManagedActivityViewModel activity in RawActivities)
            {
                activity.SetWorkStreamSettings(workStreamSettings);
            }
        }

        /// <summary>
        /// Counts again the times that every activity is constrained to, along the working calendar of the moment. It takes
        /// no lock of the core, and it is safe to call without one.
        /// </summary>
        public void UpdateEarliestStartAndLatestFinishDateTimes()
        {
            foreach (IManagedActivityViewModel activity in RawActivities)
            {
                activity.UpdateEarliestStartAndLatestFinishDateTimes();
            }
        }

        /// <summary>
        /// Calls <paramref name="visit"/> for each live activity, with its id, in the order that the live graph holds
        /// them and in one pass while <see cref="m_ActivityDataLock"/> is held. The remarks of CopyActivities say why.
        /// </summary>
        private void VisitLiveActivities(Action<int, IManagedActivityViewModel> visit)
        {
            lock (m_ActivityDataLock)
            {
                Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

                foreach (int activityId in m_VertexGraphCompiler.ActivityIds)
                {
                    if (activityLookup.TryGetValue(activityId, out IManagedActivityViewModel? activity))
                    {
                        visit(activityId, activity);
                    }
                }
            }
        }

        /// <summary>
        /// Copies every activity, together with the live activity that it came from.
        /// </summary>
        /// <remarks>
        /// The copies are taken in one pass while <see cref="m_ActivityDataLock"/> is held,
        /// so no activity can be edited part way through being copied, and the copy of the
        /// plan as a whole is of a single moment rather than of several.
        /// <para>
        /// The activities are copied in the order the live graph holds them, because the
        /// scheduling priority list resolves a tie between two activities in favour of
        /// whichever it meets first; copying them in any other order would schedule ties
        /// differently from a compilation of the live graph.
        /// </para>
        /// Call it with the lock of the core held.
        /// </remarks>
        public (List<IDependentActivity> Copies, Dictionary<int, IManagedActivityViewModel> Sources) CopyActivities()
        {
            List<IDependentActivity> activityCopies = [];
            Dictionary<int, IManagedActivityViewModel> sources = [];

            VisitLiveActivities((activityId, activity) =>
            {
                activityCopies.Add((IDependentActivity)activity.CloneObject());
                sources[activityId] = activity;
            });

            return (activityCopies, sources);
        }

        /// <summary>
        /// Applies a compilation's results to the live activities - the output half of the
        /// sequence. Each activity absorbs its own results, so what a compilation produces
        /// is applied in one place, next to the values it writes.
        /// </summary>
        /// <remarks>
        /// Deliberately not under <see cref="m_ActivityDataLock"/>. Applying results
        /// announces them - that is how they reach the view - and nothing may be announced
        /// while that lock is held; each activity takes it for its own writes and announces
        /// after releasing it. So results arrive one activity at a time, exactly as they did
        /// when the compiler wrote into the live activities as it calculated.
        /// <para>
        /// This also keeps the live compiler's own derived values right: it reports the
        /// project start and finish times by reading them from the activities it holds,
        /// which are these ones.
        /// </para>
        /// <para>
        /// A result goes only to the live activity that its copy was made from. An activity that was
        /// removed while the compilation ran, and one that has taken its id since, get nothing.
        /// </para>
        /// </remarks>
        /// <param name="graphCompilation">The compilation to apply.</param>
        /// <param name="sources">The live activity that each copy was made from, or null to go by id.</param>
        public void PublishCompilation(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            IReadOnlyDictionary<int, IManagedActivityViewModel>? sources)
        {
            Dictionary<int, IManagedActivityViewModel> activityLookup = RawActivities.ToDictionary(x => x.Id);

            foreach (IDependentActivity compiledActivity in graphCompilation.DependentActivities)
            {
                if (activityLookup.TryGetValue(compiledActivity.Id, out IManagedActivityViewModel? activity)
                    && (sources is null
                        || (sources.TryGetValue(compiledActivity.Id, out IManagedActivityViewModel? source) && ReferenceEquals(source, activity))))
                {
                    activity.SetCompiledValues(compiledActivity);
                }
            }
        }

        /// <summary>
        /// Copies every activity for a simulation, together with its three estimates and its name.
        /// </summary>
        /// <remarks>
        /// Call it with the lock of the core held.
        /// </remarks>
        public (List<IDependentActivity<int, int, int>> Activities, Dictionary<int, DurationEstimate> Estimates, Dictionary<int, string> Names) CopySimulationActivities()
        {
            List<IDependentActivity<int, int, int>> activities = [];
            Dictionary<int, DurationEstimate> estimates = [];
            Dictionary<int, string> names = [];

            // The copies are taken as a compilation takes them (CopyActivities). A simulation takes minutes, and
            // it must never see the live activities again after this.
            VisitLiveActivities((activityId, activity) =>
            {
                activities.Add((IDependentActivity<int, int, int>)activity.CloneObject());
                estimates[activityId] = EstimatesOf(activity);
                names[activityId] = activity.Name ?? string.Empty;
            });

            return (activities, estimates, names);
        }

        // The three estimates that an activity has. An activity outside the rule is one that a compile refuses (its
        // values are over the limit), and a simulation is not started for a plan that a compile refuses.
        private static DurationEstimate EstimatesOf(IManagedActivityViewModel activity)
        {
            (int optimistic, int mostLikely, int pessimistic) = EstimateHelper.Effective(
                activity.MostLikelyDuration,
                activity.OwnOptimisticDuration,
                activity.OwnPessimisticDuration);

            try
            {
                return new DurationEstimate(optimistic, mostLikely, pessimistic);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DurationEstimate.Fixed(Math.Clamp(activity.Duration, 0, GraphLimits.MaximumTimeValue));
            }
        }

        /// <summary>
        /// Starts the binds of the views of the activities. They deliver on the UI thread.
        /// </summary>
        public void StartBinds()
        {
            m_ReadOnlyActivitiesSub = m_Activities.Connect()
               .ObserveOn(RxSchedulers.MainThreadScheduler)
               .Bind(m_BoundActivities)
               .Subscribe();

            m_OrderableActivitiesSub = m_Activities.Connect()
               .ObserveOn(RxSchedulers.MainThreadScheduler) // Ensure UI thread safety
               .Bind(m_OrderableActivities)          // Bind to the mutable collection
               .DisposeMany()                        // Clean up resources
               .Subscribe();
        }

        /// <summary>
        /// Starts every activity that exists. The activities that are made after the start start as they are made.
        /// </summary>
        public void StartActivities()
        {
            foreach (IManagedActivityViewModel activity in RawActivities)
            {
                activity.StartSubscriptions();
            }
        }

        /// <summary>
        /// Ends the binds of the views of the activities.
        /// </summary>
        public void KillBinds()
        {
            m_ReadOnlyActivitiesSub?.Dispose();
            m_OrderableActivitiesSub?.Dispose();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            m_Activities.Dispose();
        }

        #endregion
    }
}
