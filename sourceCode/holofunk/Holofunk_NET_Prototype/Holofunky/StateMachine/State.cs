////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.StateMachines
{
    /// <summary>
    /// A state in a StateMachine.
    /// </summary>
    /// <remarks>
    /// Contains entry and exit actions, and an optional parent reference.
    /// 
    /// The transitions are kept at the StateMachine level.
    /// 
    /// Note that the State has no idea that transitions even exist, nor that the
    /// StateMachine itself exists!  This lets States be constructed independently
    /// (modulo parent states being created before children).  Then the 
    /// StateMachine can be created with the full set of available states.
    /// </remarks>
    public class State<TEvent, TActionState>
    {
        readonly State<TEvent, TActionState> m_parent;

        readonly List<Action<TActionState>> m_entryActions;
        readonly List<Action<TActionState>> m_exitActions;

        public State(
            State<TEvent, TActionState> parent,
            Action<TActionState> entryAction,
            Action<TActionState> exitAction)
            : this(parent, new[] { entryAction }, new[] { exitAction })
        {
        }

        public State(
            State<TEvent, TActionState> parent,
            Action<TActionState> entryAction)
            : this(parent, new[] { entryAction }, new Action<TActionState>[0])
        {
        }

        public State(
            State<TEvent, TActionState> parent,
            Action<TActionState>[] entryActions, 
            Action<TActionState>[] exitActions)
        {
            m_parent = parent;
            m_entryActions = new List<Action<TActionState>>(entryActions);
            m_exitActions = new List<Action<TActionState>>(exitActions);
        }

        public void Enter(TActionState state)
        {
            for (int i = 0; i < m_entryActions.Count; i++) {
                m_entryActions[i](state);
            }
        }
        public void Exit(TActionState state)
        {
            for (int i = 0; i < m_exitActions.Count; i++) {
                m_exitActions[i](state);
            }
        }

        public State<TEvent, TActionState> Parent { get { return m_parent; } }

        public State<TEvent, TActionState> Root 
        { 
            get 
            {
                // recursion would be fine, but this is just as easy and probably faster
                State<TEvent, TActionState> current = this;
                while (current.Parent != null) {
                    current = current.Parent;
                }
                return current;
            } 
        }
    }
}
