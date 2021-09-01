////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;
using Un4seen.Bass;
using Un4seen.BassAsio;
using WiimoteLib;

namespace Holofunk
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Console.WriteLine("test test TEST TEST TEST test test");

            using (Holofunk game = new Holofunk())
            {
                // set 10 msec as target frame time, e.g. 1/100 sec (down from 1/60 sec, the default)
                game.TargetElapsedTime = new TimeSpan(10 * 1000 * 10);
                game.Run();
            }
        }
    }
#endif
}

