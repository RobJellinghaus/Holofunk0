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
    public struct Option<T>
    {
        readonly bool m_hasValue;
        readonly T m_value;

        public static Option<T> None
        {
            get { return default(Option<T>); }
        }

        public Option(T value)
        {
            m_hasValue = true;
            m_value = value;
        }

        public static implicit operator Option<T>(T value)
        {
            return new Option<T>(value);
        }

        public T Value
        {
            get { HoloDebug.Assert(m_hasValue); return m_value; }
        }

        public bool HasValue
        {
            get { return m_hasValue; }
        }
    }
}
