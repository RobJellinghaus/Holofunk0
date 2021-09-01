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
    /// <summary>Internal text logging, for maximal speed while still allowing viewing under debugger.</summary>
    public static class Spam
    {
        // can look at this in the debugger; Console output hoses us completely (debugger too slow)
        static readonly List<string> s_output = new List<string>();

        static void WriteLine(string s)
        {
            const int chunkSize = 100;
            const int numChunks = 10;

            lock (s_output) {
                if (s_output.Count() > (chunkSize * numChunks)) {
                    s_output.RemoveRange(0, chunkSize);
                }

                s_output.Add(s);
            }
        }

        static void WriteLine()
        {
            WriteLine("");
        }

        // These inner classes let us comment in or out whole categories of spam by making local
        // edits here.
        public static class Graphics
        {
            [Conditional("DEBUG")]
            public static void WriteLine(string s)
            {
                // Spam.WriteLine(s);
            }

            [Conditional("DEBUG")]
            public static void WriteLine()
            {
                // Spam.WriteLine();
            }
        }

        // These inner classes let us comment in or out whole categories of spam by making local
        // edits here.
        public static class Audio
        {
            [Conditional("DEBUG")]
            public static void WriteLine(string s)
            {
                // Spam.WriteLine(s);
            }

            [Conditional("DEBUG")]
            public static void WriteLine()
            {
                // Spam.WriteLine();
            }
        }

        public static class Model
        {
            [Conditional("DEBUG")]
            public static void WriteLine(string s)
            {
                Spam.WriteLine(s);
            }

            [Conditional("DEBUG")]
            public static void WriteLine()
            {
                Spam.WriteLine();
            }
        }
    }
}
