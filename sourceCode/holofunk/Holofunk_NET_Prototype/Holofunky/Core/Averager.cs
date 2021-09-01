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
    /// <summary>
    /// Rolling buffer which can average a number of T's.
    /// </summary>
    /// <remarks>
    /// Parameterized with methods to handle summing / dividing the T's in question.
    /// </remarks>
    public abstract class Averager<T>
    {
        // the buffer of T's
        readonly T[] m_storage;

        // have we filled the current storage?
        bool m_storageFull;

        // what's the next index to be overwritten with the next datum?
        int m_index;

        // the total
        T m_total;

        public Averager(int capacity)
        {
            m_storage = new T[capacity];
        }

        /// <summary>
        /// Has this Averager got no data?
        /// </summary>
        public bool IsEmpty { get { return m_index == 0 && !m_storageFull; } }

        /// <summary>
        /// Update this Averager with another data point.
        /// </summary>
        public void Update(T nextT)
        {
            if (m_index == m_storage.Length) {
                // might as well unconditionally set it, branching is more expensive
                m_storageFull = true;
                m_index = 0;
            }

            if (m_storageFull) {
                m_total = Subtract(m_total, m_storage[m_index]);
            }
            m_total = Add(m_total, nextT);
            m_storage[m_index] = nextT;
            m_index++;
        }

        /// <summary>
        /// Get the average; invalid if Average.IsEmpty.
        /// </summary>
        public T Average 
        { 
            get 
            {
                Debug.Assert(!IsEmpty);
                return Divide(m_total, m_storageFull ? m_storage.Length : m_index); 
            } 
        }

        protected abstract T Subtract(T total, T nextT);
        protected abstract T Add(T total, T nextT);
        protected abstract T Divide(T total, int count);
    }

    public class FloatAverager : Averager<float>
    {
        public FloatAverager(int capacity)
            : base(capacity)
        {
        }

        protected override float Add(float total, float nextT)
        {
            return total + nextT;
        }

        protected override float Subtract(float total, float nextT)
        {
            return total - nextT;
        }

        protected override float Divide(float total, int count)
        {
            return total / count;
        }
    }

    public class Vector2Averager : Averager<Vector2>
    {
        public Vector2Averager(int capacity)
            : base(capacity)
        {
        }

        protected override Vector2 Add(Vector2 total, Vector2 nextT)
        {
            return total + nextT;
        }

        protected override Vector2 Subtract(Vector2 total, Vector2 nextT)
        {
            return total - nextT;
        }

        protected override Vector2 Divide(Vector2 total, int count)
        {
            return new Vector2(total.X / count, total.Y / count);
        }
    }
}
