////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

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
    public class Option<T>
    {
        readonly bool m_hasValue;
        readonly T m_value;

        public static Option<T> None
        {
            get { return new Option<T>(); }
        }

        Option()
        {
            m_hasValue = false;
        }

        Option(T value)
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
            get { Debug.Assert(m_hasValue); return m_value; }
        }

        public bool HasValue
        {
            get { return m_hasValue; }
        }
    }
}
