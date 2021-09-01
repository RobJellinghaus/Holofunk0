////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.BassAsio;
using WiimoteLib;

namespace Holofunk
{
    static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        static void Main(string[] args)
        {
            using (Holofunk game = new Holofunk())
            {
                var secondary = new HolofunkRenderer(game);
                game.GameSystems.Add(secondary);

                game.Run();
            }
        }
    }
}

