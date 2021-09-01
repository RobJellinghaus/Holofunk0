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
    /// A hierarchical state machine description.
    /// </summary>
    /// <remarks>
    /// This reacts to TEvents by transitioning between states.
    /// 
    /// The StateMachine contains the transitions between States, rather than keeping them in the 
    /// States themselves.  This supports more centralized control of transitioning.
    /// 
    /// The TActionState parameter corresponds to the mutable state that is available to the actions of
    /// the machine.  Think state monad.
    /// 
    /// StateMachine is an immutable class providing functional operations for returning one state from
    /// another, given a particular transition.  To actually run a state machine, use a
    /// StateMachineInstance.
    /// 
    /// Subclasses of StateMachine are intended to construct the states in a static constructor for a
    /// singleton instance.  Hence the fact that all mutators are protected.
    /// </remarks>
    public abstract class StateMachine<TEvent, TActionState> 
    {
        // The states of this machine.
        // readonly List<State<TEvent, TActionState>> m_states = new List<State<TEvent, TActionState>>();

        // The single initial state of this machine.
        readonly State<TEvent, TActionState> m_initialState;

        // A state -> event -> state transition map.
        readonly Dictionary<State<TEvent, TActionState>, List<Transition<TEvent, TActionState>>> m_transitions
            = new Dictionary<State<TEvent,TActionState>,List<Transition<TEvent,TActionState>>>();

        // Func to use when comparing transition events.
        readonly IComparer<TEvent> m_eventMatcher;

        protected StateMachine(State<TEvent, TActionState> initialState, IComparer<TEvent> eventMatcher)
        {
            Debug.Assert(initialState != null);

            //m_states.Add(initialState);
            m_initialState = initialState;
            m_eventMatcher = eventMatcher;
        }

        /*
        protected void AddState(State<TEvent, TActionState> state)
        {
            Debug.Assert(!m_states.Contains(state));
            m_states.Add(state);
        }
         */

        protected void AddTransition(
            State<TEvent, TActionState> source,
            Transition<TEvent, TActionState> transition)
        {
            //Debug.Assert(m_states.Contains(source));
            //Debug.Assert(m_states.Contains(transition.Destination));

            List<Transition<TEvent, TActionState>> list;
            if (m_transitions.ContainsKey(source)) {
                list = m_transitions[source];
            }
            else {
                list = new List<Transition<TEvent, TActionState>>();
                m_transitions[source] = list;
            }
            list.Add(transition);
        }

        /// <summary>
        /// Get the initial state.
        /// </summary>
        public State<TEvent, TActionState> InitialState { get { return m_initialState; } }

        /// <summary>
        /// If a transition exists from the source state on the given event, return that transition's
        /// destination (or null if no such transition).
        /// </summary>
        public State<TEvent, TActionState> TransitionFrom(
            State<TEvent, TActionState> source,
            TEvent evt)
        {
            //Debug.Assert(m_states.Contains(source));

            // just walk down transitions in order...
            // arguably we should use IEqualityComparer here, and it might even let us optimize
            // our transition dispatch?!
            // For now, we mildly hack by ONLY testing the comparer result against 0, freeing us
            // from both having to generate a hashcode and having to get a total ordering right.
            // ... and we would like foreach, but it forces allocation, and we are trying to
            // avoid that as a rule, so we don't have terrible cleanup issues later.
            if (m_transitions.ContainsKey(source)) {
                List<Transition<TEvent, TActionState>> transitions = m_transitions[source];
                for (int i = 0; i < transitions.Count; i++) {
                    if (m_eventMatcher.Compare(evt, transitions[i].Event) == 0) {
                        // this one be it.  up to user to avoid overlapping subscriptions
                        return transitions[i].Destination;
                    }
                }
            }

            return null;
        }
    }
}
