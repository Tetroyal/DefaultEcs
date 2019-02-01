﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DefaultEcs.Technical;
using DefaultEcs.Technical.Message;

namespace DefaultEcs
{
    /// <summary>
    /// Represent an helper object to create an <see cref="EntitySet"/> to retrieve specific subset of <see cref="Entity"/>.
    /// </summary>
    public sealed class EntitySetBuilder
    {
        #region Fields

        private static readonly MethodInfo _handleWithOneOf;

        private readonly World _world;
        private readonly List<Func<EntitySet, World, IDisposable>> _subscriptions;

        private ComponentEnum _withFilter;
        private ComponentEnum _withoutFilter;
        private List<ComponentEnum> _withOneOfFilters;

        #endregion

        #region Initialisation

        static EntitySetBuilder()
        {
            _handleWithOneOf = typeof(EntitySetBuilder).GetTypeInfo().GetDeclaredMethod(nameof(HandleWithOneOf));
        }

        internal EntitySetBuilder(World world)
        {
            _world = world;
            _subscriptions = new List<Func<EntitySet, World, IDisposable>>
            {
                (s, w) => w.Subscribe<EntityDisposedMessage>(s.Remove)
            };
        }

        #endregion

        #region Methods

        private ComponentFlag HandleWithOneOf<T>()
        {
            _subscriptions.Add((s, w) => w.Subscribe<ComponentAddedMessage<T>>(s.CheckedAdd));
            _subscriptions.Add((s, w) => w.Subscribe<ComponentRemovedMessage<T>>(s.CheckedRemove));

            return ComponentManager<T>.Flag;
        }

        /// <summary>
        /// Makes a rule to observe <see cref="Entity"/> with a component of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of component.</typeparam>
        /// <returns>The current <see cref="EntitySetBuilder"/>.</returns>
        public EntitySetBuilder With<T>()
        {
            _withFilter[ComponentManager<T>.Flag] = true;
            _subscriptions.Add((s, w) => w.Subscribe<ComponentAddedMessage<T>>(s.CheckedAdd));
            _subscriptions.Add((s, w) => w.Subscribe<ComponentRemovedMessage<T>>(s.Remove));

            return this;
        }

        /// <summary>
        /// Makes a rule to ignore <see cref="Entity"/> with a component of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of component.</typeparam>
        /// <returns>The current <see cref="EntitySetBuilder"/>.</returns>
        public EntitySetBuilder Without<T>()
        {
            _withoutFilter[ComponentManager<T>.Flag] = true;
            _subscriptions.Add((s, w) => w.Subscribe<ComponentAddedMessage<T>>(s.Remove));
            _subscriptions.Add((s, w) => w.Subscribe<ComponentRemovedMessage<T>>(s.CheckedAdd));

            return this;
        }

        public EntitySetBuilder WithAnyOf(params Type[] componentTypes)
        {
            if (componentTypes?.Length > 0)
            {
                if (_withOneOfFilters == null)
                {
                    _withOneOfFilters = new List<ComponentEnum>();
                }

                ComponentEnum filter = new ComponentEnum();
                foreach (Type componentType in componentTypes)
                {
                    filter[(ComponentFlag)_handleWithOneOf.MakeGenericMethod(componentType).Invoke(this, null)] = true;
                }

                _withOneOfFilters.Add(filter);
            }

            return this;
        }

        /// <summary>
        /// Returns an <see cref="EntitySet"/> with the specified rules.
        /// </summary>
        /// <returns>The <see cref="EntitySet"/>.</returns>
        public EntitySet Build()
        {
            List<Func<EntitySet, World, IDisposable>> subscriptions = _subscriptions.ToList();

            if (subscriptions.Count == 1 || (_withFilter.IsNull && _withOneOfFilters == null))
            {
                subscriptions.Add((s, w) => w.Subscribe<EntityCreatedMessage>(s.Add));
            }

            return new EntitySet(_world, _withFilter, _withoutFilter, _withOneOfFilters, subscriptions);
        }

        #endregion
    }
}
