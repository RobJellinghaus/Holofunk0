////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A contiguous array of samples in memory.</summary>
    /// <remarks>This is a slice out of a larger Chunk.
    /// 
    /// The naming here is somewhat inherently confusing: in audio parlance, a "sample" can be
    /// either an individual single numeric measurement of an audio signal ("the sample rate is
    /// 48,000Hz, so there are 48,000 samples per second"), or a series of such samples ("there
    /// are ten samples layered in this song").  This struct represents the latter meaning.
    /// We talk about "sample length" when we are referring to the number of individual 
    /// measurement samples in a single Sample.
    /// 
    /// We use the term "Sample count" to mean "the number of Sample<typeparam name="T"/> instances",
    /// and the term "sample length" to mean "the total number of measurement samples, e.g. data points".</remarks>
    public struct Sample<T>
    {
        // starting index in chunk
        readonly int m_index;

        // length, i.e. number of samples
        readonly int m_length;

        // backing chunk
        readonly Chunk<T> m_chunk;

        internal Sample(Chunk<T> chunk, int index, int length)
        {
            HoloDebug.Assert(chunk != null);
            HoloDebug.Assert(index >= 0);
            HoloDebug.Assert(length >= 0);
            HoloDebug.Assert(index + length <= chunk.Capacity);

            m_chunk = chunk;
            m_index = index;
            m_length = length;
        }

        /// <summary>Has this Sample<typeparam name="T"/> actually been constructed (if false, it's default)?</summary>
        internal bool IsInitialized { get { return m_chunk != null; } }

        /// <summary>The Chunk containing the actual data of this Sample.</summary>
        internal Chunk<T> Chunk { get { return m_chunk; } }

        /// <summary>The starting index of the data bracketed by this Sample.</summary>
        internal int Index { get { return m_index; } }

        /// <summary>The number of individual T's in the sequence bracketed by this Sample.</summary>
        internal int Length { get { return m_length; } }

        /// <summary>Are these samples adjacent in their underlying storage?</summary>
        public bool AdjacentTo(Sample<T> next)
        {
            return m_chunk == next.Chunk && m_index + m_length == next.Index;
        }

        /// <summary>Merge two adjacent samples into a single sample.</summary>
        public Sample<T> MergeWith(Sample<T> next)
        {
            HoloDebug.Assert(AdjacentTo(next));
            return new Sample<T>(m_chunk, m_index, m_length + next.Length);
        }

    }

    public static class SampleExtensions
    {
        public static string AsString<T>(this Sample<T> sample)
        {
            return "[chunk #" + sample.Chunk.Id
                + ", start pos " + sample.Index
                + ", length " + sample.Length + "]";
        }

        /// <summary>Copy the contents of the given buffer into this sample; use this sample's length.</summary>
        public static void CopyFrom(this Sample<float> floatSample, IntPtr buffer)
        {
            Marshal.Copy(buffer, floatSample.Chunk.Storage, floatSample.Index, floatSample.Length);
        }

        /// <summary>Copy all data from otherSample to thisSample.</summary>
        public static void CopyFrom(this Sample<float> thisSample, Sample<float> otherSample)
        {
            HoloDebug.Assert(thisSample.Length == otherSample.Length);
            Array.Copy(otherSample.Chunk.Storage, otherSample.Index, thisSample.Chunk.Storage, thisSample.Index, otherSample.Length);
        }
    }
}
