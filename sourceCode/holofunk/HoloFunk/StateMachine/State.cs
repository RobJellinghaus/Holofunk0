////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.StateMachines
{
    public abstract class State<TEvent>
    {
        readonly string m_label;
        readonly State<TEvent> m_parent;

        protected State(
            string label,
            State<TEvent> parent)
        {
            m_label = label;
            m_parent = parent;
        }

        public State<TEvent> Parent { get { return m_parent; } }

        protected String Label { get { return m_label; } }

        public State<TEvent> Root
        {
            get
            {
                // recursion would be fine, but this is just as easy and probably faster
                State<TEvent> current = this;
                while (current.Parent != null) {
                    current = current.Parent;
                }
                return current;
            }
        }

        /// <summary>
        /// Enter this state, passing in the model from the parent, and obtaining the new model.
        /// </summary>
        public abstract Model Enter(TEvent evt, Model parentState);

        /// <summary>
        /// Exit this state, passing in the current model, and obtaining the model for the parent state.
        /// </summary>
        public abstract Model Exit(TEvent evt, Model parentState);
    }

    /// <summary>A state in a StateMachine which has the same action state as its parent.</summary>
    /// <remarks>Contains entry and exit actions that reference the action state.
    /// 
    /// The transitions are kept at the StateMachine level.
    /// 
    /// Note that the State has no idea that transitions even exist, nor that the
    /// StateMachine itself exists!  This lets States be constructed independently
    /// (modulo parent states being created before children).  Then the 
    /// StateMachine can be created with the full set of available states.</remarks>
    public class State<TEvent, TModel, TParentModel> : State<TEvent>
        where TModel : Model
        where TParentModel : Model
    {
        readonly List<Action<TEvent, TModel>> m_entryActions;
        readonly List<Action<TEvent, TModel>> m_exitActions;

        readonly Func<TParentModel, TModel> m_entryConversionFunc;
        readonly Func<TModel, TParentModel> m_exitConversionFunc;

        public State(
            string label,
            State<TEvent> parent,
            Action<TEvent, TModel> entryAction,
            Action<TEvent, TModel> exitAction,
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : this(label, parent, new[] { entryAction }, new[] { exitAction }, entryConversionFunc, exitConversionFunc)
        {
        }

        public State(
            string label,
            State<TEvent> parent,
            Action<TEvent, TModel> entryAction,   
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : this(label, parent, new[] { entryAction }, new Action<TEvent, TModel>[0], entryConversionFunc, exitConversionFunc)
        {
        }

        public State(
            string label,
            State<TEvent> parent,
            Action<TEvent, TModel>[] entryActions, 
            Action<TEvent, TModel>[] exitActions,
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : base(label, parent)
        {
            m_entryActions = new List<Action<TEvent, TModel>>(entryActions);
            m_exitActions = new List<Action<TEvent, TModel>>(exitActions);

            m_entryConversionFunc = entryConversionFunc;
            m_exitConversionFunc = exitConversionFunc;
        }

        public override Model Enter(TEvent evt, Model parentState)
        {
            Spam.Model.WriteLine("State.Enter: state " + Label + ", event type: " + evt.GetType() + ", parentState.GetType(): " + parentState.GetType());
            TModel thisState;
            if (m_entryConversionFunc != null) {
                thisState = m_entryConversionFunc((TParentModel)parentState);
            }
            else {
                // had better be the same!
                thisState = (TModel)parentState;
            }
            for (int i = 0; i < m_entryActions.Count; i++) {
                m_entryActions[i](evt, thisState);
            }
            return thisState;
        }

        public override Model Exit(TEvent evt, Model state)
        {
            Spam.Model.WriteLine("State.Exit: state " + Label + ", event type: " + evt.GetType() + ", state.GetType(): " + state.GetType());
            TModel thisState = (TModel)state;
            for (int i = 0; i < m_exitActions.Count; i++) {
                m_exitActions[i](evt, thisState);
            }
            TParentModel parentState;
            if (m_exitConversionFunc != null) {
                parentState = m_exitConversionFunc(thisState);
            }
            else {
                // Terrible, but meets the expectation: these must be dynamically the same type.
                parentState = (TParentModel)(object)thisState;
            }
            return parentState;
        }
    }
}
