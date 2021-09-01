////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A large array that can be subdivided into Samples.</summary>
    public class Chunk<T> 
    {
        // storage
        readonly T[] m_storage;

        // id
        readonly int m_id;

        static int s_currentId; // ahh, mutable statics :-)

        // how many samples allocated?
        int m_allocatedLengthSamples;

        internal Chunk(int capacity)
        {
            m_storage = new T[capacity];
            m_id = s_currentId++;
        }

        internal int Capacity { get { return m_storage.Length; } }
        internal int RemainingSpace { get { return Capacity - m_allocatedLengthSamples; } }
        internal int Id { get { return m_id; } }

        // only Sample<T> should use this!
        internal T[] Storage { get { return m_storage; } }

        // Get a sample; the chunk determines where it starts, the makerFunc creates the actual sample instance.
        internal Sample<T> GetSample(int lengthSamples)
        {
            // when we run out, boom!
            HoloDebug.Assert(m_allocatedLengthSamples + lengthSamples <= Capacity);

            var ret = new Sample<T>(this, m_allocatedLengthSamples, lengthSamples);
            m_allocatedLengthSamples += lengthSamples;
            return ret;
        }

        // Reset the chunk; we want to reuse its storage.
        // This is only done by RecycledSampleTarget<T>.
        internal void Reset()
        {
            m_allocatedLengthSamples = 0;
        }
    }
}