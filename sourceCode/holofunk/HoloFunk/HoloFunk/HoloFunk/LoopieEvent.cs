//////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>The varieties of events in our Loopie machine.</summary>
    /// <remarks>The comments following are the payload of that variety of event.</remarks>
    enum LoopieEventType
    {
        None,       // uninitialized value; to catch inadvertent defaults
        TriggerDown,// trigger pressed
        TriggerUp,  // trigger released
        ADown,      // A button pressed
        AUp,        // A button released
        MinusDown,  // "-" button down
        MinusUp,    // "-" button up
        PlusDown,   // "+" button down
        PlusUp,     // "+" button up
        HomeDown,   // home button down
        HomeUp,     // home button up
        LeftDown,   // left D-pad down
        LeftUp,     // left D-pad up
        RightDown,  // right D-pad down
        RightUp,    // right D-pad up
        UpDown,     // up D-pad down
        UpUp,       // up D-pad up
        DownDown,   // down D-pad down (bee down, doo bee D-pad down)
        DownUp,     // down D-pad up
        OneDown,    // 1 down
        OneUp,      // 1 up
        TwoDown,    // 2 down
        TwoUp,      // 2 up

        Beat,       // a timer event fired once per beat
    }

    /// <summary>An event in a Loopie machine.</summary>
    struct LoopieEvent
    {
        readonly LoopieEventType m_type;

        /// <summary>The type of event.</summary>
        internal LoopieEventType Type { get { return m_type; } }

        internal LoopieEvent(LoopieEventType type) { m_type = type; }

        internal static LoopieEvent TriggerDown { get { return new LoopieEvent(LoopieEventType.TriggerDown); } }
        internal static LoopieEvent TriggerUp { get { return new LoopieEvent(LoopieEventType.TriggerUp); } }
        internal static LoopieEvent ADown { get { return new LoopieEvent(LoopieEventType.ADown); } }
        internal static LoopieEvent AUp { get { return new LoopieEvent(LoopieEventType.AUp); } }
        internal static LoopieEvent MinusDown { get { return new LoopieEvent(LoopieEventType.MinusDown); } }
        internal static LoopieEvent MinusUp { get { return new LoopieEvent(LoopieEventType.MinusUp); } }
        internal static LoopieEvent PlusDown { get { return new LoopieEvent(LoopieEventType.PlusDown); } }
        internal static LoopieEvent PlusUp { get { return new LoopieEvent(LoopieEventType.PlusUp); } }
        internal static LoopieEvent HomeDown { get { return new LoopieEvent(LoopieEventType.HomeDown); } }
        internal static LoopieEvent HomeUp { get { return new LoopieEvent(LoopieEventType.HomeUp); } }
        internal static LoopieEvent LeftDown { get { return new LoopieEvent(LoopieEventType.LeftDown); } }
        internal static LoopieEvent LeftUp { get { return new LoopieEvent(LoopieEventType.LeftUp); } }
        internal static LoopieEvent RightDown { get { return new LoopieEvent(LoopieEventType.RightDown); } }
        internal static LoopieEvent RightUp { get { return new LoopieEvent(LoopieEventType.RightUp); } }
        internal static LoopieEvent UpDown { get { return new LoopieEvent(LoopieEventType.UpDown); } }
        internal static LoopieEvent UpUp { get { return new LoopieEvent(LoopieEventType.UpUp); } }
        internal static LoopieEvent DownDown { get { return new LoopieEvent(LoopieEventType.DownDown); } }
        internal static LoopieEvent DownUp { get { return new LoopieEvent(LoopieEventType.DownUp); } }
        internal static LoopieEvent OneDown { get { return new LoopieEvent(LoopieEventType.OneDown); } }
        internal static LoopieEvent OneUp { get { return new LoopieEvent(LoopieEventType.OneUp); } }
        internal static LoopieEvent TwoDown { get { return new LoopieEvent(LoopieEventType.TwoDown); } }
        internal static LoopieEvent TwoUp { get { return new LoopieEvent(LoopieEventType.TwoUp); } }
        internal static LoopieEvent Beat { get { return new LoopieEvent(LoopieEventType.Beat); } }
    }

    class LoopieEventComparer : IComparer<LoopieEvent>
    {
        internal static readonly LoopieEventComparer Instance = new LoopieEventComparer();

        public int Compare(LoopieEvent x, LoopieEvent y)
        {
            int delta = (int)x.Type - (int)y.Type;
            return delta;
        }
    }
}
