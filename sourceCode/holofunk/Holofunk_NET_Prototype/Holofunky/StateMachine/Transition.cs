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
    /// A transition in a StateMachine.
    /// </summary>
    /// <remarks>
    /// Is labeled with an event, and contains a destination state.
    /// </remarks>
    public class Transition<TEvent, TActionState>
    {
        readonly TEvent m_event;
        readonly State<TEvent, TActionState> m_destination;

        public Transition(
            TEvent evt,
            State<TEvent, TActionState> destination)
    {
            m_event = evt;
            m_destination = destination;
        }

        public TEvent Event { get { return m_event; } }
        public State<TEvent, TActionState> Destination { get { return m_destination; } }
    }
}
