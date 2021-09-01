////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>Source of Samples; manages backing buffers.</summary>
    /// <remarks>This does no space reclamation whatsoever, yet.  Since 256MB of floats is in practice enough
    /// for almost half an hour of recording, this is fine for the current bare-bones prototype
    /// phase of Holofunk.</remarks>
    public class SamplePool<T>
    {
        readonly List<Chunk<T>> m_chunks = new List<Chunk<T>>();

        // The current chunk we are allocating new samples from.
        // Chunk zero is, appropriately, all zeroes; this lets
        // us hand out zero-filled samples cheaply.
        int m_currentChunk = 1;

        // 128K elements per chunk; just over two seconds @ 48,000 samples per second;
        // for float samples, will consume 512KB of memory
        const int ChunkCapacity = (1024 * 128);

        // 1,000 * 512K = 512MB
        const int PoolCapacity = 1000; 

        internal SamplePool()
        {
            for (int i = 0; i < PoolCapacity; i++) {
                m_chunks.Add(new Chunk<T>(ChunkCapacity));
            }
        }

        /// <summary>Number from 0 to 1, representing the amount of available recording space that has been used</summary>
        public float FractionOccupied { get { return ((float)m_currentChunk) / PoolCapacity; } }

        /// <summary>Get a single chunk.</summary>
        /// <remarks>This closes out the current chunk, returns the next one, and sets the one after that as the
        /// next available chunk.</remarks>
        /// <returns></returns>
        internal Chunk<T> AllocateChunk()
        {
            lock (this) {
                Chunk<T> ret = m_chunks[m_currentChunk++];
                m_currentChunk++;
                return ret;
            }
        }

        /// <summary>Return a Sample<typeparamref name="T"/> of the given length, suitable for copying data into.</summary>
        /// <remarks>If the current chunk is full, this will first advance to the next chunk.</remarks>
        public Sample<T> AllocateNewSample(int length)
        {
            lock (this) {
                // If ASIO gives us a sample we can't fit, blow up right away.  This would have
                // drastic implications for our memory management.
                HoloDebug.Assert(m_chunks[m_currentChunk].Capacity >= length);

                // Does current chunk have enough capacity?
                if (m_chunks[m_currentChunk].RemainingSpace >= length) {
                    return m_chunks[m_currentChunk].GetSample(length);
                }

                m_currentChunk++;

                if (m_currentChunk >= m_chunks.Count) {
                    HoloDebug.Assert(false, "Boom, out of memory!");
                }

                return m_chunks[m_currentChunk].GetSample(length);
            }
        }

        public Sample<T> GetZeroSample(int length)
        {
            lock (this) {
                HoloDebug.Assert(m_chunks[0].Capacity >= length);

                return new Sample<T>(m_chunks[0], 0, length);
            }
        }

        public void EndChunk()
        {
            lock (this) {
                m_currentChunk++;
            }
        }
    }
}
