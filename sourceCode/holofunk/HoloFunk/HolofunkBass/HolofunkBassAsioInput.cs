////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>This class manages state related to a single input channel.</summary>
    public class HolofunkBassAsioInput
    {
        /// <summary>Which ASIO channel does this input object track?</summary>
        readonly int m_asioChannel;

        /// <summary>Our parent ASIO object.</summary>
        readonly HolofunkBassAsio m_bassAsio;

        /// <summary>SampleTarget that ASIO is currently recording to</summary>
        SampleTarget<float> m_sampleTarget;

        /// <summary>TrackSampleTarget, reused for each newly recorded track</summary>
        TrackSampleTarget<float> m_trackSampleTarget;

        /// <summary>RecycledSampleTarget, reused between recording tracks</summary>
        RecycledSampleTarget<float> m_recycledSampleTarget;

        /// <summary>Track sample count that we want to stop at;
        /// set by StopRecordingAtNextBeat; if -1, we are not waiting for a track to finish</summary>
        int m_currentRecordingSampleCountLimit = -1;

        /// <summary>The number of beats long we consider the currently recorded track to be.</summary>
        int m_currentRecordingBeatCount;

        /// <summary>Measuring peak levels</summary>
        DSP_PeakLevelMeter m_plmRec;
        int m_levelL;
        int m_levelR;

        /// <summary>ASIOPROC to feed ASIO input channel data to input push stream</summary>
        ASIOPROC m_inputToInputPushStreamAsioProc;

        // Push stream for input data
        StreamHandle m_inputPushStream;

        // set of effects we are applying to the input push stream
        EffectSet m_inputPushEffects;

        /// <summary>Hook for processing incoming audio from ASIO; this copies it into m_currentRecordingTrack.</summary>
        /// <remarks>Only accessed by [AsioThread]</remarks>
        DSPPROC m_inputDspProc;

        /// <summary>
        /// How many timepoints earlier do we start a track?  Originates in the MagicNumbers class.
        /// </summary>
        int m_earlierDurationInTimepoints;

        internal HolofunkBassAsioInput(HolofunkBassAsio bassAsio, int asioChannel, int earlierDurationInTimepoints)
        {
            m_bassAsio = bassAsio;
            m_asioChannel = asioChannel;
            m_earlierDurationInTimepoints = earlierDurationInTimepoints;

            m_recycledSampleTarget = new RecycledSampleTarget<float>(
                (buffer, destSample) => destSample.CopyFrom(buffer),
                m_bassAsio.SamplePool);

            m_trackSampleTarget = new TrackSampleTarget<float>(
                (buffer, destSample) => destSample.CopyFrom(buffer),
                (srcSample, destSample) => destSample.CopyFrom(srcSample),
                m_bassAsio.SamplePool);

            m_sampleTarget = m_recycledSampleTarget;

            m_inputToInputPushStreamAsioProc = new ASIOPROC(InputToInputPushStreamAsioProc);

            // create input push stream
            m_inputPushStream = (StreamHandle)Bass.BASS_StreamCreatePush(
                Clock.TimepointRateHz,
                HolofunkBassAsio.InputChannelCount,
                BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT,
                new IntPtr(m_asioChannel));

            // connect to ASIO input channel
            CheckError(BassAsio.BASS_ASIO_ChannelEnable(
                HolofunkBassAsio.IsInputChannel,
                m_asioChannel,
                m_inputToInputPushStreamAsioProc,
                new IntPtr(m_asioChannel)));

            // join right channel if we have more than one input channel
            // (this is not generalized for >stereo)
            if (HolofunkBassAsio.InputChannelCount == 2) {
                CheckError(BassAsio.BASS_ASIO_ChannelJoin(HolofunkBassAsio.IsInputChannel, 1, m_asioChannel));
            }

            // set format and rate of input channel
            CheckError(BassAsio.BASS_ASIO_ChannelSetFormat(HolofunkBassAsio.IsInputChannel, m_asioChannel, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT));
            CheckError(BassAsio.BASS_ASIO_ChannelSetRate(HolofunkBassAsio.IsInputChannel, m_asioChannel, Clock.TimepointRateHz));

            // add input push stream to mixer
            CheckError(BassMix.BASS_Mixer_StreamAddChannel(
                (int)m_bassAsio.MixerHStream,
                (int)m_inputPushStream,
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN));

            // set up the input effects (aka microphone effects)
            m_inputPushEffects = AllEffects.CreateLoopEffectSet(m_inputPushStream, m_bassAsio.BaseForm);

            // connect peak level meter to input push stream
            m_plmRec = new DSP_PeakLevelMeter((int)m_inputPushStream, 0);
            m_plmRec.Notification += new EventHandler(Plm_Rec_Notification);

            // Register DSPPROC handler for input channel.  Make sure to hold the DSPPROC itself.
            // See documentation for BassAsioHandler.InputChannel
            m_inputDspProc = new DSPPROC(InputDspProc);

            // set up our recording DSP -- priority 10 hopefully means "run first first first!"
            CheckError(Bass.BASS_ChannelSetDSP((int)m_inputPushStream, m_inputDspProc, new IntPtr(0), 10) != 0);
        }

        Clock Clock { get { return m_bassAsio.Clock; } }

        void CheckError(bool ok)
        {
            m_bassAsio.CheckError(ok);
        }

        // ASIOPROC to feed ASIO input data to input push stream.
        // We happen to choose this proc to advance the clock and perform the internal Update() check.
        // [AsioThread]
        int InputToInputPushStreamAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            HoloDebug.Assert(input == HolofunkBassAsio.IsInputChannel);
            HoloDebug.Assert(channel == m_asioChannel);

            // Make sure we got a multiple of four, just for sanity's sake.
            // Note that we sometimes do NOT get a multiple of EIGHT -- in other words,
            // a stereo channel may not contain an even number of samples on each push.
            // Go figure.
            HoloDebug.Assert((lengthBytes & 0x3) == 0);

            Bass.BASS_StreamPutData((int)m_inputPushStream, buffer, lengthBytes);

            return lengthBytes;
        }

        /// <summary>Are we currently recording?</summary>
        /// <remarks>Updated and read by [MainThread], also read by [AsioThread]</remarks>
        public bool IsRecording { get { return m_sampleTarget == m_trackSampleTarget; } }

        /// <summary>Start recording a new track with the given id.</summary>
        /// <remarks>[AsioThread], invoked as command.</remarks>
        void StartRecording(int id, Moment now, ParameterMap startingParameters)
        {
            if (IsRecording || m_currentRecordingSampleCountLimit != -1) {
                // have to ignore this recording start
                return;
            }

            // Get a new Track.
            FloatTrack newTrack = new FloatTrack(m_bassAsio.HolofunkBass, id, Clock, now, startingParameters);

            // Aim the TrackSampleTarget at the new track.
            m_trackSampleTarget.Track = newTrack;

            // When does this track begin?
            // If you want to lock this to the actual beat turnover, then replace earlierDurationInTimepoints with now.TimepointsSinceLastBeat.
            Moment start = now.EarlierByTimepoints(m_earlierDurationInTimepoints);

            Spam.Audio.WriteLine("Start recording: current time " + now);
            Spam.Audio.WriteLine("Start recording: start time " + start);

            // Is Sample1 sufficient?
            Sample<float> sample1 = m_recycledSampleTarget.Sample1;
            Sample<float> sample2 = m_recycledSampleTarget.Sample2;

            // Now, get the sound data out of m_recycledSampleTarget and copy it into the initial chunk.
            long remainingLengthSamples = m_earlierDurationInTimepoints * HolofunkBassAsio.InputChannelCount;

            if (m_recycledSampleTarget.Sample1.Length >= remainingLengthSamples) {
                Spam.Audio.WriteLine("    Sample1 is sufficient; prepending it");

                Sample<float> src = new Sample<float>(sample1.Chunk, sample1.Length - (int)remainingLengthSamples, (int)remainingLengthSamples);
                m_trackSampleTarget.PushSampleData(src);
            }
            else {
                Spam.Audio.WriteLine("    Prepending from Sample2 then Sample1");

                long sample2Samples = remainingLengthSamples - sample1.Length;
                HoloDebug.Assert((sample2Samples + sample1.Length) == remainingLengthSamples);

                // need to put in any filler samples from sample2 first, then sample1
                Sample<float> sample2Tail = new Sample<float>(sample2.Chunk, sample2.Length - (int)sample2Samples, (int)sample2Samples);
                m_trackSampleTarget.PushSampleData(sample2Tail);
                m_trackSampleTarget.PushSampleData(sample1);
            }

            // Now update the main sample target to point to the track.
            m_sampleTarget = m_trackSampleTarget;

        }

        /// <summary>How many beats in the current recording?</summary>
        /// <remarks>0 if no current recording exists (any recording must be at least 1 beat long).</remarks>
        internal int CurrentRecordingBeatCount { get { return m_currentRecordingBeatCount; } }

        /// <summary>On what beat (since the beginning of time) did the current recording start?</summary>
        /// <remarks>If not currently recording, this returns -1.</remarks>
        internal int CurrentRecordingStartBeat { get { return IsRecording ? m_trackSampleTarget.Track.InitialBeat : -1; } }

        /// <summary>How long should this track be, given how much has been recorded so far?</summary>
        /// <remarks>The goal of this method is to round up the current track length to one of
        /// the following values:  1 beat, 2 beats, or a multiple of 4 beats.</remarks>
        int TrackBeatLength(long trackLengthTimepoints)
        {
            // How many full or fractional beats elapsed so far?
            Moment trackLengthMoment = m_bassAsio.Clock.Time(trackLengthTimepoints);

            int beats = trackLengthMoment.CompleteBeats + 1;

            // and let a track that's just slightly too long get clipped back by 1/20th of a beat
            // TODO: centralize tuning parameters like this
            if (trackLengthMoment.FractionalBeat < 0.05f) {
                beats -= 1;
            }

            switch (beats) {
                case 1:
                case 2:
                    return beats;
                default:
                    return (beats + 0x3) & ~0x3;
                    /*
                case 3:
                case 4:
                    return 4;
                default:
                    // round up to next multiple of 8
                    return (beats + 0x7) & ~0x7;
                    */
            }
        }

        /// <summary>Stop recording once the track becomes an exact number of beats long.</summary>
        /// <remarks>May need to also clip if we almost exactly hit the beat, but let's save that....
        /// 
        /// There are major threading issues here, so we are going to use a "set sentinel
        /// flags checked from 100Hz update thread" approach.
        /// 
        /// [AsioThread], invoked as command.</remarks>
        void StopRecordingAtNextBeat(Moment now)
        {
            if (!IsRecording) {
                return;
            }

            // If we are still waiting for an earlier track to finish, then leave now.
            if (m_currentRecordingSampleCountLimit != -1) {
                return;
            }

            // How long is the track, in seconds?
            // The track contains two data points per sample.
            long totalLengthTimePoints = m_trackSampleTarget.Track.TotalLengthTimepoints;

            // How long ought this track to be?
            int beatLength = TrackBeatLength(totalLengthTimePoints);

            // And samples?
            m_currentRecordingSampleCountLimit = beatLength * Clock.TimepointsPerBeat * HolofunkBassAsio.InputChannelCount;

            Spam.Audio.WriteLine("[" + m_asioChannel + "] StopRecordingAtNextBeat: now " + now);
            Spam.Audio.WriteLine("[" + m_asioChannel + "] StopRecordingAtNextBeat: totalLengthTimepoints " + totalLengthTimePoints
                + ", beatLength " + beatLength
                + ", limitTimepoints " + (m_currentRecordingSampleCountLimit / HolofunkBassAsio.InputChannelCount));
        }

        /// <summary>Update the current recording beat count prior to parsing current ASIO commands</summary>
        /// <remarks>[AsioThread]</remarks>
        internal void PreUpdate(Moment now)
        {
            // first update the track beat length
            if (IsRecording) {
                m_currentRecordingBeatCount = TrackBeatLength(
                    m_trackSampleTarget.Track.TotalLengthTimepoints);
            }
            else {
                m_currentRecordingBeatCount = 0;
            }

        }

        /// <summary>Called from ASIO output proc to execute any outstanding commands from main thread,
        /// and to check current recording and end it if it's over limit </summary>
        /// <remarks>[AsioThread]
        /// 
        /// Sends a TrackComplete response if the track actually finishes.</remarks>
        internal void Update(Moment now)
        {
            // first update the track beat length
            if (IsRecording) {
                m_currentRecordingBeatCount = TrackBeatLength(
                    m_trackSampleTarget.Track.TotalLengthTimepoints);
            }
            else {
                m_currentRecordingBeatCount = 0;
            }

            // now, check to see if current track is done
            if (!IsRecording || m_currentRecordingSampleCountLimit == -1) {
                return;
            }

            // check whether current track is over limit
            if (m_trackSampleTarget.Track.TotalLengthSamples >= m_currentRecordingSampleCountLimit) {
                // we're done!
                Track<float> track = StopRecording(now);

                // we always start playing synchronously and instantly
                track.StartPlaying(m_bassAsio.StreamPool, now);

                m_bassAsio.AsioToMainQueue.Enqueue(
                    new AsioResponse(m_asioChannel, AsioResponseType.TrackComplete, track));
            }
        }

        /// <summary>Execute the given command on this input channel.</summary>
        internal void Execute(AsioCommand command, Moment now)
        {
            HoloDebug.Assert(command.Channel == m_asioChannel);
            switch (command.Type) {
                case AsioCommandType.StartRecording: {
                        StartRecording(command.Param, now, command.StartingParameters);
                        return;
                    }

                case AsioCommandType.StopRecordingAtNextBeat: {
                        StopRecordingAtNextBeat(now);
                        return;
                    }
            }
        }

        /// <summary>Stop recording, and return the track that was just recorded.</summary>
        /// <remarks>[AsioThread]</remarks>
        Track<float> StopRecording(Moment now)
        {
            HoloDebug.Assert(IsRecording);
            HoloDebug.Assert(m_currentRecordingSampleCountLimit != -1);
            // We should not be here unless our track got long enough.
            HoloDebug.Assert(m_trackSampleTarget.Track.TotalLengthSamples >= m_currentRecordingSampleCountLimit);

            // Switch the sample target back to the recycling target.
            // This is the essential publication from the game thread back to the ASIO thread.
            m_sampleTarget = m_recycledSampleTarget;

            Spam.Audio.WriteLine("Stop recording: current time: " + now);

            // If the track got slightly longer, then trim the start to match the beat duration as exactly as possible.
            if (m_currentRecordingSampleCountLimit < m_trackSampleTarget.Track.TotalLengthSamples) {
                Spam.Audio.WriteLine("Trimming track; length " + m_trackSampleTarget.Track.TotalLengthSamples
                    + ", limit " + m_currentRecordingSampleCountLimit);

                int trackSamplesToTrim = (int)(m_trackSampleTarget.Track.TotalLengthSamples - m_currentRecordingSampleCountLimit);

                // Now trim that many from the end.
                m_trackSampleTarget.Track.TrimFromEnd(trackSamplesToTrim);

                // and set the track to start playing that many timepoints in,
                // since we ran late already
                m_trackSampleTarget.Track.InitialTimepointAdvance = trackSamplesToTrim / HolofunkBassAsio.InputChannelCount;

                Spam.Audio.WriteLine(", final length " + m_trackSampleTarget.Track.TotalLengthSamples);
            }

            Track<float> track = m_trackSampleTarget.Track;
            m_trackSampleTarget.Track = null;
            m_currentRecordingSampleCountLimit = -1;
            return track;
        }

        /// <summary>Apply these parameters to the input (microphone) channel.</summary>
        internal void UpdateMicrophoneParameters(ParameterMap set, Moment now)
        {
            m_inputPushEffects.Apply(set, now);
        }

        /// <summary>Consume incoming audio via our DSP function, and copy it into our recording track (if any).</summary>
        /// <remarks>[AsioThread], reads m_sampleTarget written from main thread</remarks>
        void InputDspProc(int handle, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            if (lengthBytes == 0 || buffer == IntPtr.Zero) {
                return;
            }

            // should be multiple of 4 since it's a float stream
            HoloDebug.Assert((lengthBytes & 0x3) == 0);

            m_sampleTarget.PushSampleData(buffer, lengthBytes >> 2);
        }

        // [AsioThread], writes state variables read from main thread
        void Plm_Rec_Notification(object sender, EventArgs e)
        {
            if (m_plmRec != null) {
                m_levelL = m_plmRec.LevelL;
                m_levelR = m_plmRec.LevelR;
            }
        }

        public int InputLevelL { get { return m_levelL; } }
        public int InputLevelR { get { return m_levelR; } }
    }
}
