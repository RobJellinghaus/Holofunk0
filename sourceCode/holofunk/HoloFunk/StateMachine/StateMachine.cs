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
    /// <summary>A hierarchical state machine description.</summary>
    /// <remarks>This reacts to TEvents by transitioning between states.
    /// 
    /// The StateMachine contains the transitions between States, rather than keeping them in the 
    /// States themselves.  This supports more centralized control of transitioning.
    /// 
    /// StateMachine is an immutable class providing functional operations for returning one state from
    /// another, given a particular transition.  To actually run a state machine, use a
    /// StateMachineInstance.
    /// 
    /// Subclasses of StateMachine are intended to construct the states in a static constructor for a
    /// singleton instance.  Hence the fact that all mutators are protected.</remarks>
    public abstract class StateMachine<TEvent> 
    {
        // The single initial state of this machine.
        readonly State<TEvent> m_initialState;

        // A state -> event -> state transition map.
        readonly Dictionary<State<TEvent>, List<Transition<TEvent>>> m_transitions
            = new Dictionary<State<TEvent>, List<Transition<TEvent>>>();

        // Func to use when comparing transition events.
        readonly IComparer<TEvent> m_eventMatcher;

        protected StateMachine(State<TEvent> initialState, IComparer<TEvent> eventMatcher)
        {
            HoloDebug.Assert(initialState != null);

            m_initialState = initialState;
            m_eventMatcher = eventMatcher;
        }

        protected void AddTransition(
            State<TEvent> source,
            Transition<TEvent> transition)
        {
            //HoloDebug.Assert(m_states.Contains(source));
            //HoloDebug.Assert(m_states.Contains(transition.Destination));

            List<Transition<TEvent>> list;
            if (m_transitions.ContainsKey(source)) {
                list = m_transitions[source];
            }
            else {
                list = new List<Transition<TEvent>>();
                m_transitions[source] = list;
            }

            foreach (Transition<TEvent> t in list) {
                // if this assertion fires, it is because you tried to add a duplicate transition on the same event from the same state
                HoloDebug.Assert(m_eventMatcher.Compare(t.Event, transition.Event) != 0);
            }

            list.Add(transition);
        }

        /// <summary>Get the initial state.</summary>
        public State<TEvent> InitialState { get { return m_initialState; } }

        /// <summary>If a transition exists from the source state on the given event, return that transition's
        /// destination (or null if no such transition).</summary>
        public State<TEvent> TransitionFrom(
            State<TEvent> source,
            TEvent evt)
        {
            //HoloDebug.Assert(m_states.Contains(source));

            // just walk down transitions in order...
            // arguably we should use IEqualityComparer here, and it might even let us optimize
            // our transition dispatch?!
            // For now, we mildly hack by ONLY testing the comparer result against 0, freeing us
            // from both having to generate a hashcode and having to get a total ordering right.
            // ... and we would like foreach, but it forces allocation, and we are trying to
            // avoid that as a rule, so we don't have terrible cleanup issues later.
            if (m_transitions.ContainsKey(source)) {
                List<Transition<TEvent>> transitions = m_transitions[source];
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
