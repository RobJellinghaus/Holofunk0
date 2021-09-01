////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    public class HoloDebug
    {
        /// <summary>Assertion dialogs can hose Holofunk; this trivial wrapper lets us breakpoint just before we dialog.</summary>
        /// <param name="value"></param>
        public static void Assert(bool value)
        {
            if (!value) {
                Debug.Assert(value);
            }
        }

        /// <summary>Assertion dialogs can hose Holofunk; this trivial wrapper lets us breakpoint just before we dialog.</summary>
        /// <param name="value"></param>
        public static void Assert(bool value, string message)
        {
            if (!value) {
                Debug.Assert(value, message);
            }
        }
    }
}
