////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Holofunk.Tests
{
    /// <summary>Simple object to collect and compare messages.</summary>
    public class Logger
    {
        readonly List<string> m_messages = new List<string>();
        int m_lastExpectedIndex;

        public Logger() { }

        public void Log(string message)
        {
            m_messages.Add(message);
        }

        public void Check(params string[] messages)
        {
            foreach (string expected in messages) {
                string actual = m_messages[m_lastExpectedIndex++];

                for (int i = 0; i < expected.Length; i++) {
                    HoloDebug.Assert(expected[i] == actual[i]);
                }

                int comparison = string.Compare(expected, actual);
                HoloDebug.Assert(comparison == 0);
            }
        }

        public void CheckDone()
        {
            HoloDebug.Assert(m_messages.Count == m_lastExpectedIndex);
        }

        public void CheckOnly(params string[] messages)
        {
            Check(messages);
            CheckDone();
        }
    }
}
