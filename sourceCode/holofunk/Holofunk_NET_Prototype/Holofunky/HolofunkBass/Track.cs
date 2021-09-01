////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>
    /// A series of Samples, with functionality for creating and feeding BASS streams.
    /// </summary>
    /// <remarks>
    /// The overriding goal of Holofunk is to *stay in sync*.  The problem, though, is that BASS seems
    /// not to support absolutely perfect looping; a 48,000-timepoint track will not clock accurately
    /// against 48,000 samples going through the BASS input ASIOPROC.  This leads to clock drift between
    /// the master Clock (driven by counting input ASIOPROC samples), and the looping Tracks (driven by
    /// SYNCPROCS that fire after the exact-multiple-of-48,000-samples data is fully played).
    /// 
    /// So we need some kind of clock correction.  This is also implemented in the Track, which uses
    /// internal hysteresis to see how far out of step it is getting with the underlying Clock, and
    /// which artificially truncates itself if necessary.  (So far we have only seen excess lag in the
    /// track playback; we've never yet seen a track play *faster* than the input ASIOPROC chews samples.)
    /// </remarks>
    public abstract class Track<T> : IDisposable
    {
        // list of samples
        readonly List<Sample<T>> m_samples = new List<Sample<T>>();

        // total length of all samples (not timepoints!) appended with Append
        int m_totalAppendedLengthSamples;
        
        // length of initially empty sample, once updated (if any)
        int m_initiallyEmptyLengthSamples;

        // BASS HSTREAM to the stream of this track
        protected int m_trackHStream;

        // HolofunkBass we were created on
        HolofunkBass m_holofunkBass;

        // ID of the track (also gets used as user data in track callback)
        readonly int m_id;

        // SYNCPROC to push more data into this stream
        readonly SYNCPROC m_syncProc;

        // handle of the synchronizer attached to m_trackHStream
        int m_trackHSync;

        // Master clock, for time spam.
        Clock m_clock;

        // Time at last sync.
        Moment m_lastSyncTime;

        // Rolling average of duration of each sync.
        // Rolling average of 20 syncs seems not unreasonable....
        FloatAverager m_averageSyncLengthTimepoints = new FloatAverager(3);

        // Peak level meter.
        DSP_PeakLevelMeter m_plmTrack;
        int m_levelL;
        int m_levelR;

        // The number of timepoints by which we last adjusted.
        // We try to keep this in hysteresis such that the average sync duration always equals our
        // actual timepoint length.
        int m_loopLagLengthTimepoints;

        /// <summary>
        /// Create a Track.
        /// </summary>
        /// <param name="bass">The BASS helper object; for coordination on disposal.</param>
        /// <param name="id">Unique ID of this track.</param>
        /// <param name="clock">ASIO-driven clock.</param>
        /// <param name="initialEmptyChunk">If true, add an initial empty sample, to be filled in post-construction.</param>
        public Track(HolofunkBass bass, int id, Clock clock, bool initialEmptySample = false)
        {
            m_holofunkBass = bass;
            m_id = id;
            m_syncProc = new SYNCPROC(SyncProc);
            m_clock = clock;

            if (initialEmptySample) {
                m_samples.Add(default(Sample<T>));
            }
        }

        // Update an initially empty sample (at the start of the Track).
        // This is necessary because the data we need to update it (the retroactively recorded last N milliseconds of
        // sound) is not stable until after we have switched ASIO to recording into this new track.  So we
        // create the track (with an empty slot at start), switch ASIO to record into it, then extract the last
        // N msec of data from the previous ASIO target and poke it into the track via this method.
        internal void UpdateEmptySample(Sample<T> nonEmptySample)
        {
            Spam.Write("Updating initially empty sample: ");
            nonEmptySample.Spam();
            Spam.WriteLine();

            Debug.Assert(!m_samples[0].IsInitialized);
            Debug.Assert(nonEmptySample.IsInitialized);
            m_samples[0] = nonEmptySample;
            m_initiallyEmptyLengthSamples = nonEmptySample.Length;
        }

        /// <summary>
        /// Append a sample that has been populated with new data for the track.
        /// </summary>
        public void Append(Sample<T> sample)
        {
            if (m_samples.Count == 0) {
                m_samples.Add(sample);

                Spam.Write("Track #");
                Spam.Write(m_id);
                Spam.Write(": appending initial sample");
                sample.Spam();
                Spam.WriteLine();
            }
            else if (m_samples[m_samples.Count - 1].AdjacentTo(sample)) {
                // coalesce adjacent samples
                m_samples[m_samples.Count - 1] = m_samples[m_samples.Count - 1].MergeWith(sample);
            }
            else {
                Spam.Write("Track #");
                Spam.Write(m_id);
                Spam.Write(": completed sample ");
                m_samples[m_samples.Count - 1].Spam();
                Spam.WriteLine();

                Spam.Write("Track #");
                Spam.Write(m_id);
                Spam.Write(": appending initial sample ");
                sample.Spam();
                Spam.WriteLine();

                m_samples.Add(sample);
            }

            m_totalAppendedLengthSamples += sample.Length;
        }

        /// <summary>
        /// The number of discrete Sample<typeparam name="T"/> instances in this Track.
        /// </summary>
        /// <remarks>
        /// This should really be named Sample<typeparamref name="T"/>Count -- this is not the 
        /// number of individual data points in this Track, but rather the number of disjoint
        /// Sample<typeparamref name="T"/> instances in this Track.
        /// </remarks>
        public int SampleCount { get { return m_samples.Count; } }

        /// <summary>
        /// The number of data points (e.g. total number of <typeparam name="T"/>'s) in this Track.
        /// </summary>
        /// <remarks>
        /// We maintain two fields for this, to avoid races between the ASIO thread (incrementing
        /// m_totalAppendedLengthSamples) and the UI thread (possibly updating m_initiallyEmptyLengthSamples).
        /// </remarks>
        public int TotalLengthSamples 
        { 
            get { return m_initiallyEmptyLengthSamples + m_totalAppendedLengthSamples; } 
        }

        /// <summary>
        /// Total number of recorded Timepoints in this Track.
        /// </summary>
        public int TotalLengthTimepoints
        {
            get { return TotalLengthSamples / HolofunkBass.InputChannelCount; }
        }

        /// <summary>
        /// Get one of the Sample<typeparamref name="T"/>'s in this track, by index.
        /// </summary>
        public Sample<T> this[int i] { get { return m_samples[i]; } }

        /// <summary>
        /// Copy all of the given sample's data to the stream.
        /// </summary>
        /// <remarks>
        /// The lengthSamples argument permits us to adjust (shorten) a sample's length while
        /// pushing it.  We use this for the clock drift adjustment, to keep the playback of
        /// this Track perfectly in sync with the input ASIOPROC.
        /// </remarks>
        /// <param name="sample">The sample to copy data from</param>
        /// <param name="lengthSamples">The number of samples to push.</param>
        /// <param name="last">Is this the last sample in the stream?</param>
        protected abstract void PushSampleToStream(Sample<T> sample, int lengthSamples);

        /// <summary>
        /// Push the next sample to the stream.
        /// </summary>
        /// <remarks>
        /// We push one entire sample at a time, to let BASS's own buffering perform more effectively.
        /// </remarks>
        /// <returns>true if they could all be copied; false if they ran out</returns>
        public void PushTrackToStream()
        {
            for (int i = 0; i < m_samples.Count; i++) {
                if (i == m_samples.Count - 1) {
                    // last sample, clock-adjust it
                    PushSampleToStream(
                        m_samples[i], 
                        m_samples[i].Length - (m_loopLagLengthTimepoints * HolofunkBass.InputChannelCount));
                }
                else {
                    PushSampleToStream(m_samples[i], m_samples[i].Length);
                }
            }
        }

        public void SyncProc(int handle, int channel, int data, IntPtr user)
        {
            // push the next sample to the stream
            if (data == 0) { // means "stalled"
                Moment now = m_clock.Now;
                int elapsedTimepoints = now.TimepointCount - m_lastSyncTime.TimepointCount;
                m_lastSyncTime = now;
                m_averageSyncLengthTimepoints.Update(elapsedTimepoints);

                float averageSyncLengthTimepoints = m_averageSyncLengthTimepoints.Average;
                float timepointDrift = TotalLengthTimepoints - averageSyncLengthTimepoints;

                // adjust in the direction of the drift, unless we're absolutely even
                m_loopLagLengthTimepoints -= (int)(timepointDrift / 2);

                Spam.Write("Track #");
                Spam.Write(m_id);
                Spam.Write(" SyncProc invoked at time ");
                Spam.Write(m_clock.Now);
                Spam.Write("; average sync length (timepoints): ");
                Spam.Write(averageSyncLengthTimepoints);
                Spam.Write("; timepoint drift: ");
                Spam.Write(timepointDrift);
                Spam.Write("; loop lag (timepoints): ");
                Spam.Write(m_loopLagLengthTimepoints);
                Spam.WriteLine();

                PushTrackToStream();
            }
        }

        public void StartPlaying()
        {
            Spam.Write("Starting playing; total track length is now ");
            Spam.Write(m_totalAppendedLengthSamples);

            m_trackHStream = Bass.BASS_StreamCreatePush(
                HolofunkBass.TimepointFrequencyHz, 
                HolofunkBass.InputChannelCount, 
                BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_DECODE, 
                new IntPtr(m_id));

            m_holofunkBass.AddStreamToMixer(m_trackHStream, new IntPtr(m_id), m_syncProc);

            m_trackHSync = BassMix.BASS_Mixer_ChannelSetSync(
                m_trackHStream,
                BASSSync.BASS_SYNC_MIXTIME | BASSSync.BASS_SYNC_STALL, 
                0, // ignored
                m_syncProc,
                new IntPtr(0));

            // connect peak level meter to input push stream
            m_plmTrack = new DSP_PeakLevelMeter(m_trackHStream, 0);
            m_plmTrack.Notification += new EventHandler(Plm_Track_Notification);

            m_lastSyncTime = m_clock.Now;

            // and boot us up with the whole track's data
            PushTrackToStream();
        }

        /// <summary>
        /// Set the volume to a fraction of its maximum value.
        /// </summary>
        /// <param name="vol"></param>
        public void SetVolume(double vol)
        {
            Debug.Assert(vol >= 0);
            Debug.Assert(vol <= 1);

            Bass.BASS_ChannelSetAttribute(m_trackHStream, BASSAttribute.BASS_ATTRIB_VOL, (float)(HolofunkBass.TopMixVolume * vol));
        }

        void Plm_Track_Notification(object sender, EventArgs e)
        {
            if (m_plmTrack != null) {
                m_levelL = m_plmTrack.LevelL;
                m_levelR = m_plmTrack.LevelR;
            }
        }

        public int InputLevelL { get { return m_levelL; } }
        public int InputLevelR { get { return m_levelR; } }

        /// <summary>
        /// Trim the given number of samples from the start.
        /// </summary>
        /// <remarks>
        /// This really is samples, not timepoints.
        /// </remarks>
        public void TrimFromStart(int lengthSamples)
        {
            Trim(lengthSamples, false);
        }

        /// <summary>
        /// Trim the given number of samples from the end.
        /// </summary>
        /// <remarks>
        /// This really is samples, not timepoints.
        /// </remarks>
        public void TrimFromEnd(int lengthSamples)
        {
            Trim(lengthSamples, true);
        }

        void Trim(int lengthSamples, bool fromEnd)
        {
            Debug.Assert(lengthSamples < TotalLengthSamples);

            m_totalAppendedLengthSamples -= lengthSamples;

            int index = fromEnd ? m_samples.Count - 1 : 0;

            while (m_samples[index].Length <= lengthSamples) {
                lengthSamples -= m_samples[index].Length;
                m_samples.RemoveAt(index);

                // have to update index because m_samples.Count changed
                index = fromEnd ? m_samples.Count - 1 : 0;
            }

            if (lengthSamples == 0) {
                return;
            }

            // trim last sample
            Debug.Assert(lengthSamples < m_samples[index].Length);
            m_samples[index] = new Sample<T>(
                m_samples[index].Chunk, 
                fromEnd ? m_samples[index].Index : m_samples[index].Index + lengthSamples, 
                m_samples[index].Length - lengthSamples);
        }

        #region IDisposable Members

        public void Dispose()
        {
            m_holofunkBass.RemoveStreamFromMixer(m_trackHStream);
        }

        #endregion
    }

    public class FloatTrack : Track<float>
    {
        public FloatTrack(HolofunkBass bass, int id, Clock clock, bool initialEmptySample = false) : base(bass, id, clock, initialEmptySample)
        {
        }

        protected unsafe override void PushSampleToStream(Sample<float> sample, int lengthSamples)
        {
            float[] samples = sample.Chunk.Storage;

            // per http://www.un4seen.com/forum/?topic=12912.msg89978#msg89978
            fixed (float* p = &samples[sample.Index]) {
                byte* b = (byte*)p;

                // we ignore the return value from StreamPutData since it queues anything in excess,
                // so we don't need to track any underflows
                Bass.BASS_StreamPutData(
                    m_trackHStream,
                    new IntPtr(p),
                    lengthSamples * sizeof(float));
            }
        }
    }
}
