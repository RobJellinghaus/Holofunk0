////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

#if DEBUG
// uncomment this to spam to console; comment it to not spam at all
#define CONSOLE_SPAM
#endif

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>
    /// Wrappers around Console.Write, to allow centralized enabling/disabling of debug output.
    /// </summary>
    /// <remarks>
    /// There are deliberately no format strings, as these cost bitter, bitter garbage.
    /// </remarks>
    public static class Spam
    {
        public static void Write(string s)
        {
#if CONSOLE_SPAM
            Console.Write(s);
#endif
        }

        public static void Write(int i)
        {
#if CONSOLE_SPAM
            Console.Write(i);
#endif
        }

        public static void Write(float f)
        {
#if CONSOLE_SPAM
            Console.Write(f);
#endif
        }

        public static void Write(double d)
        {
#if CONSOLE_SPAM
            Console.Write(d);
#endif
        }

        public static void Write(Moment moment)
        {
#if CONSOLE_SPAM
            Console.Write("[");
            Console.Write(moment.TimepointCount);
            Console.Write(" timepoints, ");
            Console.Write(moment.Seconds);
            Console.Write(" secs, ");
            Console.Write(moment.Beats);
            Console.Write(" beats]");
#endif
        }

        public static void WriteLine()
        {
#if CONSOLE_SPAM
            Console.WriteLine();
#endif
        }

    }
}
