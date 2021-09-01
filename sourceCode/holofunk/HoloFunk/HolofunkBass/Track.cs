////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
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
    /// <summary>A series of Samples, with functionality for creating and feeding BASS streams.</summary>
    public abstract class Track<T> 
    {
        /// <summary>list of samples</summary>
        readonly List<Sample<T>> m_samples = new List<Sample<T>>();

        /// <summary>total length of all samples (not timepoints!) appended with Append</summary>
        long m_totalAppendedLengthSamples;

        /// <summary>HolofunkBass we were created on</summary>
        HolofunkBass m_holofunkBass;

        /// <summary>ID of the track (also gets used as user data in track callback)</summary>
        readonly int m_id;

        /// <summary>SYNCPROC to push more data into this stream</summary>
        readonly SYNCPROC m_syncProc;

        /// <summary>The BassStream we are using (and reserving).</summary>
        BassStream m_bassStream;

        /// <summary>handle of the synchronizer attached to m_trackHStream</summary>
        StreamHandle m_trackHSync;

        /// <summary>Master clock, for time spam.</summary>
        Clock m_clock;

        /// <summary>Time at last sync.</summary>
        Moment m_lastSyncTime;

        /// <summary>The beat time at which we started recording.
        /// This is needed later to get the proper phase.</summary>
        int m_initialBeat;

        /// <summary>How many timepoints should we skip when we first start playing? 
        /// This is simply so we can avoid running late by the amount that we
        /// missed our target timepoint count.</summary>
        int m_initialTimepointAdvance;

        /// <summary>Peak level meter.</summary>
        DSP_PeakLevelMeter m_plmTrack;
        int m_levelL;
        int m_levelR;

        /// <summary>Index of sample last pushed to stream</summary>
        int m_index;

        /// <summary>Parameters of the track's effects</summary>
        ParameterMap m_parameters;

        /// <summary>Is the track muted?</summary>
        bool m_isMuted;

        /// <summary>Create a Track.</summary>
        /// <param name="bass">The BASS helper object; for coordination on disposal.</param>
        /// <param name="id">Unique ID of this track.</param>
        /// <param name="clock">ASIO-driven clock.</param>
        /// <param name="now">ASIO time.</param>
        /// <param name="startingParameters">A starting set of parameters, which must have been already copied (unshared).</param>
        public Track(HolofunkBass bass, int id, Clock clock, Moment now, ParameterMap startingParameters)
        {
            m_holofunkBass = bass;
            m_id = id;
            m_syncProc = new SYNCPROC(SyncProc);
            m_clock = clock;

            m_initialBeat = now.CompleteBeats;

            m_parameters = AllEffects.CreateParameterMap();
            m_parameters.ShareAll(startingParameters);
        }

        /// <summary>The number of discrete Sample<typeparam name="T"/> instances in this Track.</summary>
        /// <remarks>This should really be named Sample<typeparamref name="T"/>Count -- this is not the 
        /// number of individual data points in this Track, but rather the number of disjoint
        /// Sample<typeparamref name="T"/> instances in this Track.</remarks>
        public int SampleCount { get { return m_samples.Count; } }

        /// <summary>The number of data points (e.g. total number of <typeparam name="T"/>'s) in this Track.</summary>
        /// <remarks>We maintain two fields for this, to avoid races between the ASIO thread (incrementing
        /// m_totalAppendedLengthSamples) and the UI thread (possibly updating m_initiallyEmptyLengthSamples).</remarks>
        public long TotalLengthSamples 
        { 
            get { return m_totalAppendedLengthSamples; } 
        }

        /// <summary>On what beat (since the beginning of time) did we start being recorded?</summary>
        public int InitialBeat
        {
            get { return m_initialBeat; }
        }

        /// <summary>Total number of recorded Timepoints in this Track.</summary>
        public long TotalLengthTimepoints
        {
            get { return TotalLengthSamples / HolofunkBassAsio.InputChannelCount; }
        }

        /// <summary>Get the length of this Track as a Moment, enabling all kinds of useful conversions.</summary>
        public Moment LengthAsMoment
        {
            get { return m_clock.Time(TotalLengthTimepoints); }
        }

        /// <summary>Get one of the Sample<typeparamref name="T"/>'s in this track, by index.</summary>
        public Sample<T> this[int i] { get { return m_samples[i]; } }

        /// <summary>The Parameters of this track.</summary>
        public ParameterMap Parameters { get { return m_parameters; } }

        /// <summary>Copy all of the given sample's data to the stream.</summary>
        /// <remarks>The lengthSamples argument permits us to adjust (shorten) a sample's length while
        /// pushing it.  We use this for the clock drift adjustment, to keep the playback of
        /// this Track perfectly in sync with the input ASIOPROC.</remarks>
        /// <param name="sample">The sample to copy data from</param>
        /// <param name="lengthSamples">The number of samples to push.</param>
        /// <param name="last">Is this the last sample in the stream?</param>
        protected abstract void PushSampleToStream(Sample<T> sample, int lengthSamples);

        /// <summary>Append a sample that has been populated with new data for the track.</summary>
        public void Append(Sample<T> sample)
        {
            if (m_samples.Count == 0) {
                m_samples.Add(sample);

                Spam.Audio.WriteLine("Track #" + m_id + ": setting initial sample " + sample.AsString());
            }
            else if (m_samples[m_samples.Count - 1].AdjacentTo(sample)) {
                // coalesce adjacent samples
                m_samples[m_samples.Count - 1] = m_samples[m_samples.Count - 1].MergeWith(sample);
                // Spam.Audio.WriteLine("Track #" + m_id + ": merged into tail sample " + m_samples[m_samples.Count - 1].AsString());
            }
            else {
                Spam.Audio.WriteLine("Track #" + m_id + ": completed tail sample " + m_samples[m_samples.Count - 1].AsString());
                Spam.Audio.WriteLine("Track #" + m_id + ": appending next sample " + sample.AsString());

                m_samples.Add(sample);
            }

            m_totalAppendedLengthSamples += sample.Length;
        }

        /// <summary>ASIO sync callback.</summary>
        /// <remarks>[AsioThread]</remarks>
        void SyncProc(int handle, int channel, int data, IntPtr user)
        {
            // push the next sample to the stream
            if (data == 0) { // means "stalled"
                Moment now = m_clock.Now;

                Spam.Audio.WriteLine("Track #" + m_id + " SyncProc: now: " + m_clock.Now + "; ");

                PushNextSampleToStream();
            }
        }

        void PushNextSampleToStream()
        {
            HoloDebug.Assert(m_index >= 0);
            HoloDebug.Assert(m_index < m_samples.Count);

            Spam.Audio.WriteLine("pushing sample #" + m_index);

            PushSampleToStream(
                m_samples[m_index],
                m_samples[m_index].Length); //  - (m_loopLagLengthTimepoints * HolofunkBassAsio.InputChannelCount)); // let's see if this is still critical...

            m_index++;
            if (m_index >= m_samples.Count) {
                m_index = 0;
            }
        }

        /// <summary>If we want to skip some samples at the beginning, set this.</summary>
        public int InitialTimepointAdvance
        {
            set { m_initialTimepointAdvance = value; }
        }

        /// <summary>Start playing this track.</summary>
        /// <remarks>[MainThread] but we have seen no troubles from the cross-thread operations here --
        /// the key point is that m_syncProc is ready to go as soon as the ChannelSetSync happens.</remarks>
        public void StartPlaying(BassStreamPool pool, Moment now)
        {
            Spam.Audio.WriteLine("Starting playing; total track length is now " + m_totalAppendedLengthSamples);

            m_bassStream = pool.Reserve();

            m_bassStream.Effects.Apply(Parameters, now);

            m_holofunkBass.AddStreamToMixer(StreamHandle);

            m_trackHSync = (StreamHandle)BassMix.BASS_Mixer_ChannelSetSync(
                (int)StreamHandle,
                BASSSync.BASS_SYNC_MIXTIME | BASSSync.BASS_SYNC_END, 
                0, // ignored
                m_syncProc,
                new IntPtr(0));

            // connect peak level meter to input push stream
            m_plmTrack = new DSP_PeakLevelMeter((int)StreamHandle, 0);
            m_plmTrack.Notification += new EventHandler(Plm_Track_Notification);

            // we want to set the clock LATER in this case
            m_lastSyncTime = m_clock.Now.EarlierByTimepoints(-m_initialTimepointAdvance);

            // and push the first sample
            Spam.Audio.WriteLine("Track #" + m_id + ": starting; ");

            PushNextSampleToStream();
        }

        /// <summary>Effects use this property to invoke the appropriate channel attribute setting, etc.</summary>
        internal StreamHandle StreamHandle { get { return m_bassStream.PushStream; } }

        /// <summary>Set whether the track is muted or not.</summary>
        /// <param name="vol"></param>
        public void SetMuted(bool isMuted)
        {
            m_isMuted = isMuted;

            // TODO: make this handle time properly in the muting; Moment.Start is pure hack here
            Bass.BASS_ChannelSetAttribute(
                (int)StreamHandle, 
                BASSAttribute.BASS_ATTRIB_VOL, 
                isMuted ? 0f : m_parameters[VolumeEffect.Volume].GetInterpolatedValue(Moment.Start));
        }

        void Plm_Track_Notification(object sender, EventArgs e)
        {
            if (m_plmTrack != null) {
                m_levelL = m_plmTrack.LevelL;
                m_levelR = m_plmTrack.LevelR;
            }
        }

        public float LevelRatio 
        { 
            get { return m_holofunkBass.CalculateLevelRatio(m_levelL, m_levelR); } 
        }
        
        /// <summary>Trim the given number of samples from the start.</summary>
        /// <remarks>This really is samples, not timepoints.</remarks>
        public void TrimFromStart(int lengthSamples)
        {
            Trim(lengthSamples, false);
        }

        /// <summary>Trim the given number of samples from the end.</summary>
        /// <remarks>This really is samples, not timepoints.</remarks>
        public void TrimFromEnd(int lengthSamples)
        {
            Trim(lengthSamples, true);
        }

        void Trim(int lengthSamples, bool fromEnd)
        {
            // <= is perfectly possible here
            HoloDebug.Assert(lengthSamples <= TotalLengthSamples);

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

            // trim last sample -- this really must be < here
            HoloDebug.Assert(lengthSamples < m_samples[index].Length);
            m_samples[index] = new Sample<T>(
                m_samples[index].Chunk, 
                fromEnd ? m_samples[index].Index : m_samples[index].Index + lengthSamples, 
                m_samples[index].Length - lengthSamples);
        }

        /// <summary>Update all effects applied to this track.</summary>
        /// <remarks>[MainThread]
        /// 
        /// This should arguably be on the ASIO thread, but it doesn't seem likely that we can afford
        /// the ASIO speed hit.</remarks>
        public void UpdateEffects(Moment now)
        {
            m_bassStream.Effects.Apply(Parameters, now);
        }

        public void ResetEffects(Moment now)
        {
            Parameters.ResetToDefault();
            UpdateEffects(now);
        }

        public void Dispose(Moment now)
        {
            Parameters.ResetToDefault();
            m_bassStream.Effects.Apply(Parameters, now);

            m_holofunkBass.RemoveStreamFromMixer(StreamHandle);

            m_holofunkBass.Free(m_bassStream);
        }
    }

    public class FloatTrack : Track<float>
    {
        public FloatTrack(HolofunkBass bass, int id, Clock clock, Moment now, ParameterMap startingParameters) 
            : base(bass, id, clock, now, startingParameters)
        {
        }

        protected unsafe override void PushSampleToStream(Sample<float> sample, int lengthSamples)
        {
            float[] samples = sample.Chunk.Storage;

            // reset stream position so no longer ended.
            // this is as per http://www.un4seen.com/forum/?topic=12965.msg90332#msg90332
            Bass.BASS_ChannelSetPosition((int)StreamHandle, 0, BASSMode.BASS_POS_BYTES);

            // per http://www.un4seen.com/forum/?topic=12912.msg89978#msg89978
            fixed (float* p = &samples[sample.Index]) {
                byte* b = (byte*)p;

                // we ignore the return value from StreamPutData since it queues anything in excess,
                // so we don't need to track any underflows
                Bass.BASS_StreamPutData(
                    (int)StreamHandle,
                    new IntPtr(p),
                    (lengthSamples * sizeof(float)) | (int)BASSStreamProc.BASS_STREAMPROC_END);
            }
        }
    }
}
