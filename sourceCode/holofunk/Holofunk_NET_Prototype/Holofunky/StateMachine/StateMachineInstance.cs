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
    public static class ListExtensions
    {
        public static void AddFrom<T>(this List<T> thiz, List<T> other, int start, int count)
        {
            Debug.Assert(thiz != null && other != null);
            Debug.Assert(start >= 0 && count >= 0);
            Debug.Assert(start + count <= other.Count);

            for (int i = start; i < start + count; i++) {
                thiz.Add(other[i]);
            }
        }
    }

    /// <summary>
    /// A running instantiation of a particular StateMachine.
    /// </summary>
    public class StateMachineInstance<TEvent, TActionState> : IObserver<TEvent>
    {
        readonly StateMachine<TEvent, TActionState> m_machine;
        State<TEvent, TActionState> m_machineState;
        TActionState m_actionState;

        // Reused stacks for finding common parents.
        readonly List<State<TEvent, TActionState>> m_startList = new List<State<TEvent, TActionState>>();
        readonly List<State<TEvent, TActionState>> m_endList = new List<State<TEvent, TActionState>>();
        readonly List<State<TEvent, TActionState>> m_pathDownList = new List<State<TEvent, TActionState>>();

        public StateMachineInstance(StateMachine<TEvent, TActionState> machine, TActionState initialState)
        {
            m_machine = machine;
            m_machineState = machine.InitialState.Root;
            m_actionState = initialState;

            MoveTo(machine.InitialState);
        }

        public TActionState ActionState
        {
            get { return m_actionState; }
        }

        // We are in state start.  We need to get to state end.
        // Do so by performing all the exit actions necessary to get to the common parent of start and end,
        // and then 
        void MoveTo(State<TEvent, TActionState> end)
        {
            // Get the common parent of start and end.
            // This will be null if they have no common parent.
            State<TEvent, TActionState> commonParent = GetCommonParent(m_machineState, end, m_pathDownList);

            ExitUpTo(m_machineState, commonParent);
            EnterDownTo(m_pathDownList);

            m_machineState = end;
        }

        void ExitUpTo(State<TEvent, TActionState> state, State<TEvent, TActionState> commonParent)
        {
            while (state != commonParent) {
                state.Exit(m_actionState);
                state = state.Parent;
            }
        }

        void EnterDownTo(List<State<TEvent, TActionState>> pathToEnd)
        {
            for (int i = 0; i < pathToEnd.Count; i++) {
                pathToEnd[i].Enter(m_actionState);
            }
        }

        State<TEvent, TActionState> GetCommonParent(
            State<TEvent, TActionState> start,
            State<TEvent, TActionState> end,
            List<State<TEvent, TActionState>> pathDownToEnd)
        {
            // we don't handle this case!
            Debug.Assert(start != end);

            if (start == null || end == null) {
                return null;
            }

            // make a list of all states to root.
            // (actually, the lists wind up being ordered from root to the leaf state.)
            ListToRoot(start, m_startList);
            ListToRoot(end, m_endList);

            // now the common parent is the end of the longest common prefix.
            pathDownToEnd.Clear();
            for (int i = 0; i < Math.Min(m_startList.Count, m_endList.Count); i++) {
                if (m_startList[i] != m_endList[i]) {
                    if (i == 0) {
                        pathDownToEnd.AddFrom(m_endList, 0, m_endList.Count);
                        return null;
                    }
                    else {
                        pathDownToEnd.AddFrom(m_endList, i - 1, m_endList.Count - i + 1);
                        return m_startList[i - 1];
                    }
                }
            }

            // If we got to here, then one list is a prefix of the other.

            if (m_startList.Count > m_endList.Count) {
                // The start list is longer, so end contains (hierarchically speaking) start.
                // So there IS no pathDownToEnd, and the end of endList is the common parent.
                return m_endList[m_endList.Count - 1];
            }
            else {
                // m_endList is longer.
                pathDownToEnd.AddFrom(m_endList, m_startList.Count - 1, m_endList.Count - m_startList.Count + 1);
                return m_startList[m_startList.Count - 1];
            }
        }

        // Clear list and replace it with the ancestor chain of state, with the root at index 0.
        void ListToRoot(
            State<TEvent, TActionState> state,
            List<State<TEvent, TActionState>> list)
        {
            list.Clear();

            while (state != null) {
                list.Add(state);
                state = state.Parent;
            }

            list.Reverse();
        }

        public void OnCompleted()
        {
            // we don't do nothin' (yet)
        }

        public void OnError(Exception exception)
        {
            Debug.WriteLine(exception.ToString());
            Debug.WriteLine(exception.StackTrace);
            Debug.Assert(false);
        }

        public void OnNext(TEvent value)
        {
            // Find transition if any.
            var destination = m_machine.TransitionFrom(m_machineState, value);
            if (destination != null) {
                MoveTo(destination);
            }
        }
    }
}
