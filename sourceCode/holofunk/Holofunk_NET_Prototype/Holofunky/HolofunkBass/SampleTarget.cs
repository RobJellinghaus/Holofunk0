using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>
    /// Destination of samples being recorded.
    /// </summary>
    /// <remarks>
    /// Holofunk's threading model is simple:  there is a game thread, which drives the game
    /// update and rendering loop, and which processes input; and there is an ASIO thread,
    /// which drives ASIO sound processing.
    /// 
    /// The interaction between these two threads is primarily around state transitions for
    /// starting and stopping recording.  We need a thread-safe means of synchronization for
    /// these transitions.
    /// 
    /// SampleTarget<typeparam name="T"/> represents a destination for sound data processed
    /// by ASIO.  Various subclasses of SampleTarget implement particular policies for sample
    /// data (e.g. recording into successive chunks with a Track, or recording into recycled
    /// chunks for latency compensation).  The game thread can update a single SampleTarget
    /// reference to thread-safely expose a change of state to the ASIO thread, lock-free and
    /// wait-free.
    /// </remarks>
    internal abstract class SampleTarget<T>
    {
        // Action that implements copying data from a byte buffer into a Sample<T>.
        protected readonly Action<IntPtr, Sample<T>> m_copyAction;

        // Is the current target stable?
        /// Arguably this should be volatile or something like it, to ensure prompt inter-thread
        /// visibility of writes to the underlying field...?
        protected bool m_isStable;

        internal SampleTarget(Action<IntPtr, Sample<T>> copyAction)
        {
            m_copyAction = copyAction;
        }

        /// <summary>
        /// Push data from the given buffer into this sample target.
        /// </summary>
        internal abstract void PushSampleData(IntPtr buffer, int lengthSamples);

        /// <summary>
        /// Is this sample target's contained data stable?
        /// </summary>
        /// <remarks>
        /// The sample target must set this to false while doing its processing, then set it to 
        /// true when done.  This allows the game thread to switch the sample target and then
        /// ensure the previous sample target's processing is complete.
        /// 
        /// Arguably this should be volatile or something like it, to ensure prompt inter-thread
        /// visibility of writes to the underlying field...?
        /// </remarks>
        internal bool IsStable { get { return m_isStable; } }
    }

    /// <summary>
    /// Sample target which records data into a Track.
    /// </summary>
    /// <remarks>
    /// This single Target can be redirected at successive Tracks, to avoid having to allocate a
    /// target per track.
    /// </remarks>
    internal class TrackSampleTarget<T> : SampleTarget<T>
    {
        readonly SamplePool<T> m_pool;
        Track<T> m_track;

        internal TrackSampleTarget(Action<IntPtr, Sample<T>> copyAction, SamplePool<T> pool)
            : base(copyAction)
        {
            m_pool = pool;
        }

        internal Track<T> Track
        {
            get { return m_track; }
            set { m_track = value; }
        }

        internal override void PushSampleData(IntPtr sourceBuffer, int lengthSamples)
        {
            m_isStable = false;

            Sample<T> sample = m_pool.AllocateNewSample(lengthSamples);

            m_copyAction(sourceBuffer, sample);

            m_track.Append(sample);

            m_isStable = true;
        }
    }

    /// <summary>
    /// Sample target which copies data into a double recycle buffer; first chunk1 is
    /// filled up, then chunk2, then chunk1 is copied over, etc.
    /// </summary>
    /// <remarks>
    /// The purpose here is to allow us to continually keep the last N milliseconds of
    /// audio, *even when we are not recording.*  This way, when we start recording,
    /// we can compensate for the latency of the whole input system by initializing the
    /// new track with the last N milliseconds of audio.
    /// </remarks>
    internal class RecycledSampleTarget<T> : SampleTarget<T>
    {
        // m_sample1 is the currently accumulated sample in the current chunk.
        Sample<T> m_sample1;

        // m_sample2 is the previously accumulated sample (if any) in the previous chunk.
        Sample<T> m_sample2;

        // The chunks are pulled from the sample pool passed on creation, and are
        // rotated as we recycle.
        Chunk<T> m_chunk1;
        Chunk<T> m_chunk2;

        internal RecycledSampleTarget(Action<IntPtr, Sample<T>> copyAction, SamplePool<T> pool)
            : base(copyAction)
        {
            m_chunk1 = pool.AllocateChunk();
            m_chunk2 = pool.AllocateChunk();
        }

        // Get both of the samples.
        // Sample1 will always have more recent data, but may not have much of it.
        // Sample2 will have less recent data, but should almost always be full (except within msec
        // of the start of the entire app).
        internal Sample<T> Sample1 { get { return m_sample1; } }
        internal Sample<T> Sample2 { get { return m_sample2; } }

        internal override void PushSampleData(IntPtr buffer, int lengthSamples)
        {
            m_isStable = false;

            Sample<T> nextSample;

            if (!m_sample1.IsInitialized) {
                nextSample = m_chunk1.GetSample(lengthSamples);
                m_sample1 = nextSample;
            }
            else if (lengthSamples > m_sample1.Chunk.RemainingSpace) {
                // not enough space left in m_sample1's Chunk.
                // first, copy m_sample1 to m_sample2, to preserve it
                m_sample2 = m_sample1;
                // now, switch m_sample1's Chunk
                Chunk<T> chunkToReset;
                if (m_sample1.Chunk == m_chunk1) {
                    // we're now going to use chunk 2; wipe it
                    chunkToReset = m_chunk2;
                }
                else {
                    Debug.Assert(m_sample1.Chunk == m_chunk2);
                    chunkToReset = m_chunk1;
                }
                Debug.Assert(m_sample2.Chunk != chunkToReset);

                // Recycle chunkToReset's storage
                chunkToReset.Reset();

                // And carve m_sample1 from chunkToReset
                nextSample = chunkToReset.GetSample(lengthSamples);
                m_sample1 = nextSample;
            }
            else {
                nextSample = m_sample1.Chunk.GetSample(lengthSamples);
                Debug.Assert(m_sample1.AdjacentTo(nextSample));
                m_sample1 = m_sample1.MergeWith(nextSample);
            }

            m_copyAction(buffer, nextSample);

            m_isStable = true;
        }
    }
}
