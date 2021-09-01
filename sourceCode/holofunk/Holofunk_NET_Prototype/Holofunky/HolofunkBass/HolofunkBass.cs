////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
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
    /// <summary>
    /// Manager object for almost all interaction with the BASS library.
    /// </summary>
    /// <remarks>
    /// Manages recording, track creation, mixing, and generally all other top-level functions.
    /// 
    /// The term "timepoint" is used here to mean "a point in time at which a sample was taken."
    /// Since our channels are stereo, there are technically two mono samples per timepoint.
    /// Therefore "sample count" is a bit ambiguous -- are those mono samples or stereo samples?
    /// To avoid confusion, we use "timepoint" analogously to "sample" whenever we are calculating
    /// purely based on time.
    /// </remarks>
    public class HolofunkBass : IDisposable
    {
        // Constants defined by BassASIO.  I am not entirely sure how these are chosen, but they
        // are consistent in the examples and -- on my M-Audio Fast Track Pro USB sound box --
        // they work.
        internal const int AsioInputDeviceId = 0;
        internal const int AsioOutputDeviceId = 1;
        internal const int AsioInputChannelId = 0;
        internal const int AsioOutputChannelId = 0;
        internal const bool IsInputChannel = true;
        internal const bool IsOutputChannel = false;

        // How many data points per second?  (See class remarks.)
        internal const int TimepointFrequencyHz = 48000;

        // How many retroactive timepoints do we want to apply at the start of a new track?
        // This is 1/10 second... let's experiment....
        internal const int RetroactiveTimepoints = TimepointFrequencyHz / 10; 

        // How many channels is stereo?
        internal const int StereoChannels = 2;

        // How many channels coming from our input?
        // (This is admittedly hardcoded to my personal Fast Track Pro setup with a mono
        // microphone.)
        // Also, this is static rather than const, as making it const produces all kinds of
        // unreachable code warnings.
        // It is public because it needs to be visible from the main Holofunk assembly, which
        // constructs the Clock, which keeps time by converting total ASIO input sample count 
        // into total timepoint count.
        public static readonly int InputChannelCount = 1;

        // Destination of all recorded audio
        readonly SamplePool<float> m_samplePool;

        // SampleTarget that ASIO is currently recording to
        SampleTarget<float> m_sampleTarget;

        // TrackSampleTarget, reused for each newly recorded track
        TrackSampleTarget<float> m_trackSampleTarget;

        // RecycledSampleTarget, reused between recording tracks
        RecycledSampleTarget<float> m_recycledSampleTarget;

        // Track sample count that we want to stop at;
        // set by StopRecordingAtNextBeat; if -1, we are not waiting for a track to finish
        int m_currentRecordingSampleCountLimit = -1;

        // Measuring peak levels
        DSP_PeakLevelMeter m_plmRec;
        int m_levelL;
        int m_levelR;

        // Hook for processing incoming audio from ASIO; 
        // this copies it into m_currentRecordingTrack
        DSPPROC m_inputDspProc;

        // Mixer stream (HSTREAM)... yes, it's a bit Hungarian... consider wrapping BASS API
        // to distinguish int-based types....
        int m_mixerHStream;

        // ASIOPROC to feed mixer stream data to ASIO output
        ASIOPROC m_mixerToOutputAsioProc;

        // ASIOPROC to feed ASIO input channel data to input push stream
        ASIOPROC m_inputToInputPushStreamAsioProc;

        // Push stream for input data
        int m_inputPushStream;

        // we make newly added streams a bit quiet...
        internal const double TopMixVolume = 0.7;

        // friggin' wav encoder!  want something done right, got to have BASS do it :-)
        EncoderWAV m_wavEncoder;
        
        int m_asioBufferPreferredSize;

        Clock m_clock;

        public HolofunkBass(Clock clock)
        {
            // This will allocate a mere 1GB or so!!!!!
            m_samplePool = new SamplePool<float>();

            m_inputToInputPushStreamAsioProc = new ASIOPROC(InputToInputPushStreamAsioProc);
            m_mixerToOutputAsioProc = new ASIOPROC(MixerToOutputAsioProc);

            m_clock = clock;

            m_recycledSampleTarget = new RecycledSampleTarget<float>(
                (buffer, destSample) => destSample.CopyFrom(buffer),
                m_samplePool);

            m_trackSampleTarget = new TrackSampleTarget<float>(
                (buffer, destSample) => destSample.CopyFrom(buffer),
                m_samplePool);

            m_sampleTarget = m_recycledSampleTarget;
        }

        // ASIOPROC to feed mixer stream data to ASIO output buffer.
        int MixerToOutputAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            Debug.Assert(input == IsOutputChannel);
            Debug.Assert(channel == AsioOutputChannelId);

            // ChannelGetData here is populating the output buffer for us.
            int bytesAvailable = Bass.BASS_ChannelGetData(m_mixerHStream, buffer, lengthBytes);

            return bytesAvailable;
        }

        // ASIOPROC to feed ASIO input data to input push stream.
        public int InputToInputPushStreamAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            Debug.Assert(input == IsInputChannel);
            Debug.Assert(channel == AsioInputChannelId);

            Bass.BASS_StreamPutData(m_inputPushStream, buffer, lengthBytes);

            // And move time forwards!
            m_clock.AddSampleLength(lengthBytes >> 2);

            return lengthBytes;
        }

        public void StartASIO()
        {
            // not playing anything via BASS, so don't need an update thread
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATEPERIOD, 0);

            // setup BASS - "no sound" device but SampleFrequencyHz (default for ASIO)
            Bass.BASS_Init(0, TimepointFrequencyHz, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            BassAsio.BASS_ASIO_Init(AsioInputDeviceId, BASSASIOInit.BASS_ASIO_THREAD);

            ////////////////////// DEVICE SETUP

            BassAsio.BASS_ASIO_SetDevice(AsioOutputDeviceId);
            BassAsio.BASS_ASIO_SetRate(TimepointFrequencyHz);

            BassAsio.BASS_ASIO_SetDevice(AsioInputDeviceId);
            BassAsio.BASS_ASIO_SetRate(TimepointFrequencyHz);

            BASS_ASIO_INFO asioInfo = BassAsio.BASS_ASIO_GetInfo();
            int inputLatency = BassAsio.BASS_ASIO_GetLatency(IsInputChannel);
            int outputLatency = BassAsio.BASS_ASIO_GetLatency(IsOutputChannel);

            // looks like ASIO can't keep up with bufpref (= 256 on RJELLING-HOMEPC)
            m_asioBufferPreferredSize = asioInfo.bufpref * 2;

            ////////////////////// OUTPUT SETUP

            // converted away from BassAsioHandler, to enable better viewing of intermediate data
            // (and full control over API use, allocation, etc.)

            // set output device
            BassAsio.BASS_ASIO_SetDevice(AsioOutputDeviceId);

            m_mixerHStream = BassMix.BASS_Mixer_StreamCreate(
                TimepointFrequencyHz,
                StereoChannels,
                BASSFlag.BASS_MIXER_RESUME | BASSFlag.BASS_MIXER_NONSTOP | BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);

            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo(m_mixerHStream, mixerInfo);

            // connect to ASIO output channel
            BassAsio.BASS_ASIO_ChannelEnable(IsOutputChannel, AsioOutputChannelId, m_mixerToOutputAsioProc, new IntPtr(m_mixerHStream));

            // Not really sure about the next few lines, but it's all from the BASS documentation for ASIOPROC...
            // and it seems to work....

            // Join second mixer channel (right stereo channel).
            BassAsio.BASS_ASIO_ChannelJoin(IsOutputChannel, 1, AsioOutputChannelId);

            BassAsio.BASS_ASIO_ChannelSetFormat(IsOutputChannel, AsioOutputChannelId, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT);
            BassAsio.BASS_ASIO_ChannelSetRate(IsOutputChannel, AsioOutputChannelId, TimepointFrequencyHz);

            // start recording dammit!!!
            // setup an encoder on the asio input channel
            // Note: this will write a 32-bit, 48kHz, stereo Wave file
            m_wavEncoder = new EncoderWAV(m_mixerHStream);
            m_wavEncoder.InputFile = null; // use STDIN (the above channel)
            DateTime now = DateTime.Now;
            string dateTime = string.Format("{0:D4}{1:D2}{2:D2}_{3:D2}{4:D2}{5:D2}",
                now.Year,
                now.Month,
                now.Day,
                now.Hour,
                now.Minute,
                now.Second);
            m_wavEncoder.OutputFile = Path.Combine(Directory.GetCurrentDirectory(), Path.Combine("Recordings", "holofunk_" + dateTime + ".wav"));
            bool ok = m_wavEncoder.Start(null, IntPtr.Zero, false);

            ////////////////////// INPUT SETUP

            BassAsio.BASS_ASIO_SetDevice(AsioInputDeviceId);

            // create input push stream
            m_inputPushStream = Bass.BASS_StreamCreatePush(
                TimepointFrequencyHz, 
                InputChannelCount, 
                BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT, 
                new IntPtr(AsioInputChannelId));

            // connect to ASIO input channel
            BassAsio.BASS_ASIO_ChannelEnable(IsInputChannel, AsioInputChannelId, m_inputToInputPushStreamAsioProc, new IntPtr(AsioInputChannelId));

            // join right channel if we have more than one input channel
            // (this is not generalized for >stereo)
            if (InputChannelCount == 2) {
                BassAsio.BASS_ASIO_ChannelJoin(IsInputChannel, 1, AsioInputChannelId);
            }

            // set format and rate of input channel
            BassAsio.BASS_ASIO_ChannelSetFormat(IsInputChannel, AsioInputChannelId, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT);
            BassAsio.BASS_ASIO_ChannelSetRate(IsInputChannel, AsioInputChannelId, TimepointFrequencyHz);

            // add input push stream to mixer
            BassMix.BASS_Mixer_StreamAddChannel(
                m_mixerHStream,
                m_inputPushStream, 
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN);

            // connect peak level meter to input push stream
            m_plmRec = new DSP_PeakLevelMeter(m_inputPushStream, 0);
            m_plmRec.Notification += new EventHandler(Plm_Rec_Notification);

            // Register DSPPROC handler for input channel.  Make sure to hold the DSPPROC itself.
            // See documentation for BassAsioHandler.InputChannel
            m_inputDspProc = new DSPPROC(InputDspProc);

            // set up our recording DSP -- priority 10 hopefully means "run first first first!"
            Bass.BASS_ChannelSetDSP(m_inputPushStream, m_inputDspProc, new IntPtr(0), 10);

            BassAsio.BASS_ASIO_Start(m_asioBufferPreferredSize);
            
            // get the info again, see if latency has changed
            asioInfo = BassAsio.BASS_ASIO_GetInfo();
            inputLatency = BassAsio.BASS_ASIO_GetLatency(IsInputChannel);
            outputLatency = BassAsio.BASS_ASIO_GetLatency(IsOutputChannel);
        }

        /// <summary>
        /// Add track to mixer.
        /// </summary>
        /// <param name="trackHStream">HSTREAM of the track to add.</param>
        /// <param name="trackUserData">Track's user data.</param>
        /// <param name="trackSync">the syncproc that will push more track data</param>
        internal void AddStreamToMixer(int trackHStream, IntPtr trackUserData, SYNCPROC trackSyncProc)
        {
            bool ok;

            BASS_CHANNELINFO trackInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo(trackHStream, trackInfo);
            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo(m_mixerHStream, mixerInfo);

            ok = BassMix.BASS_Mixer_StreamAddChannel(
                m_mixerHStream, 
                trackHStream, 
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN);

            // try setting to 40% volume to reduce over-leveling
            ok = Bass.BASS_ChannelSetAttribute(trackHStream, BASSAttribute.BASS_ATTRIB_VOL, (float)TopMixVolume);

            ok = BassMix.BASS_Mixer_ChannelPlay(trackHStream);

            // end the current chunk so we don't overlap data in a single chunk
            // (wild-assed guess about the occasional mangled track/chunk that seems to come up infrequently....)
            m_samplePool.EndChunk();
        }

        /// <summary>
        /// Remove this stream from the mixer's inputs.
        /// </summary>
        internal void RemoveStreamFromMixer(int trackHStream)
        {
            bool ok = BassMix.BASS_Mixer_ChannelRemove(trackHStream);
        }

        /// <summary>
        /// Number from 0 to 1, representing the amount of available recording space that has been used
        /// </summary>
        public float FractionOccupied { get { return m_samplePool.FractionOccupied; } }

        public bool IsRecording { get { return m_sampleTarget == m_trackSampleTarget; } }

        public void StartRecording(int id)
        {
            if (IsRecording || m_currentRecordingSampleCountLimit != -1) {
                // have to ignore this recording start
                return;
            }

            // First, get a new Chunk to write the recycled data into.
            Chunk<float> initialTrackChunk = m_samplePool.AllocateChunk();
            // Now get a new Track with an initially empty sample.
            FloatTrack newTrack = new FloatTrack(this, id, m_clock, initialEmptySample: true);
            // Aim the TrackSampleTarget at it.
            m_trackSampleTarget.Track = newTrack;

            // Now update the main sample target to point to the track.
            // This is the crucial publication from the game thread to the ASIO thread.
            m_sampleTarget = m_trackSampleTarget;

            // Wait for the recycled data to become stable.  Since ASIO will not invoke this target from scratch,
            // the only way this could be unstable would be for the ASIO thread to be right in the middle of running
            // it, and there is only an O(1) amount of work for it to do, so a busy wait is reasonable.
            while (!m_recycledSampleTarget.IsStable) {
                // do nothing?!  pure spinloop!  TBD whether this gets hung up, but it shouldn't....
            }

            // Now, get the sound data out of m_recycledSampleTarget and copy it into the initial chunk.
            int remainingLengthSamples = RetroactiveTimepoints * InputChannelCount;

            Spam.Write("Starting recording; getting ");
            Spam.Write(remainingLengthSamples);
            Spam.Write(" retroactive samples...");
            Spam.WriteLine();

            // Is Sample1 sufficient?
            Sample<float> sample1 = m_recycledSampleTarget.Sample1;
            Sample<float> sample2 = m_recycledSampleTarget.Sample2;

            if (m_recycledSampleTarget.Sample1.Length >= remainingLengthSamples) {
                Spam.Write("    Sample1 is sufficient; prepending it");
                Spam.WriteLine();

                Sample<float> dest = initialTrackChunk.GetSample(remainingLengthSamples);
                Sample<float> src = new Sample<float>(sample1.Chunk, sample1.Length - remainingLengthSamples, remainingLengthSamples);
                dest.CopyFrom(src);
                newTrack.UpdateEmptySample(dest);
            }
            else {
                Spam.Write("    Prepending from Sample2 then Sample1");
                Spam.WriteLine();

                // need to put in any filler samples from sample2 first, then sample1
                int sample2Samples = remainingLengthSamples - sample1.Length;
                Debug.Assert((sample2Samples + sample1.Length) == remainingLengthSamples);

                Sample<float> dest = initialTrackChunk.GetSample(sample2Samples);
                Sample<float> src2 = new Sample<float>(sample2.Chunk, sample2.Length - sample2Samples, sample2Samples);
                dest.CopyFrom(src2);

                Sample<float> dest2 = initialTrackChunk.GetSample(sample1.Length);
                dest2.CopyFrom(sample1);

                // now dest and dest2 are adjacent
                Debug.Assert(dest.AdjacentTo(dest2));

                // stick their concatenation into the track
                newTrack.UpdateEmptySample(dest.MergeWith(dest2));
            }

            Moment now = m_clock.Now;

            Spam.Write("Start recording: current time: ");
            Spam.Write(now);
            Spam.WriteLine();
        }

        /// <summary>
        /// Stop recording once the track becomes an exact number of beats long.
        /// </summary>
        /// <remarks>
        /// May need to also clip if we almost exactly hit the beat, but let's save that....
        /// 
        /// There are major threading issues here, so we are going to use a "set sentinel
        /// flags checked from 100Hz update thread" approach.
        /// </remarks>
        public void StopRecordingAtNextBeat()
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
            int totalSampleLength = m_trackSampleTarget.Track.TotalLengthSamples;
            int totalLengthTimePoints = totalSampleLength / InputChannelCount;
            double trackLengthSeconds = ((double)totalLengthTimePoints) / TimepointFrequencyHz;

            // now how many seconds per beat, Mr. Clock?
            double trackLengthBeats = trackLengthSeconds * m_clock.BeatsPerSecond;
            int trackLengthFullBeats = (int)Math.Floor(trackLengthBeats); // redundant, but pure in heart

            // are we JUST over a full beat?  if so, then we'll be done at the next game tick.
            if (trackLengthFullBeats > 0 && trackLengthBeats - trackLengthFullBeats < 0.1) {
                // we only wanted to be as long as the full beats
                m_currentRecordingSampleCountLimit = 
                    (int)(trackLengthFullBeats 
                        * m_clock.BeatsPerSecond
                        * HolofunkBass.TimepointFrequencyHz)
                        * HolofunkBass.InputChannelCount;

                return;
            }

            // and add one to get to the beat we're waiting for
            int trackLengthDesiredFullBeats = trackLengthFullBeats + 1;

            // How many timepoints is that?
            int desiredTotalLengthTimepoints = BeatsToTimepoints(trackLengthDesiredFullBeats);
            // And samples?
            int desiredTotalLengthSamples = desiredTotalLengthTimepoints * InputChannelCount;
                
            // Now add that onto our current track sample length to get the length we're
            // waiting for.
            m_currentRecordingSampleCountLimit = desiredTotalLengthSamples;

            Spam.Write("StopRecordingAtNextBeat: totalSampleLength ");
            Spam.Write(totalSampleLength);
            Spam.Write(", trackLengthSeconds ");
            Spam.Write(trackLengthSeconds);
            Spam.Write(", trackLengthFullBeats ");
            Spam.Write(trackLengthFullBeats);
            Spam.Write(", trackLengthDesiredSamples ");
            Spam.Write(desiredTotalLengthSamples);
            Spam.WriteLine();
        }

        /// <summary>
        /// Called from game loop update routine to check current recording and end it if it's over limit 
        /// </summary>
        /// <remarks>
        /// Returns null if there is no completed track; returns the completed track if it is actually done.
        /// 
        /// TODO: this should arguably be happening from the ASIO thread, as who knows how long delayed or
        /// batched-up the game thread might get....
        /// </remarks>
        public Track<float> Update()
        {
            if (!IsRecording || m_currentRecordingSampleCountLimit == -1) {
                return null;
            }

            // check whether current track is over limit
            if (m_trackSampleTarget.Track.TotalLengthSamples >= m_currentRecordingSampleCountLimit) {
                // we're done!
                return StopRecording();
            }
            else {
                return null;
            }
        }

        /// <summary>
        /// Stop recording, and return the track that was just recorded.
        /// </summary>
        /// <returns></returns>
        Track<float> StopRecording()
        {
            Debug.Assert(IsRecording);
            Debug.Assert(m_currentRecordingSampleCountLimit != -1);
            // We should not be here unless our track got long enough.
            Debug.Assert(m_trackSampleTarget.Track.TotalLengthSamples >= m_currentRecordingSampleCountLimit);

            // Switch the sample target back to the recycling target.
            // This is the essential publication from the game thread back to the ASIO thread.
            m_sampleTarget = m_recycledSampleTarget;

            // ASIO thread may be calling into m_trackSampleTarget right now; busy-wait until it's done.
            while (!m_trackSampleTarget.IsStable) {
                // spin-loop for the moment
            }

            Moment now = m_clock.Now;
            Spam.Write("Stop recording: current time: ");
            Spam.Write(now);
            Spam.WriteLine();

            // If the track got slightly longer, then trim the start to match the beat duration as exactly as possible.
            if (m_currentRecordingSampleCountLimit < m_trackSampleTarget.Track.TotalLengthSamples) {
                Spam.Write("Trimming track; length ");
                Spam.Write(m_trackSampleTarget.Track.TotalLengthSamples);
                Spam.Write(", limit ");
                Spam.Write(m_currentRecordingSampleCountLimit);

                int trackSamplesToTrim = m_trackSampleTarget.Track.TotalLengthSamples - m_currentRecordingSampleCountLimit;

                // Now trim that many from the end.
                m_trackSampleTarget.Track.TrimFromEnd(trackSamplesToTrim);

                Spam.Write(", final length ");
                Spam.Write(m_trackSampleTarget.Track.TotalLengthSamples);
                Spam.WriteLine();
            }

            Track<float> track = m_trackSampleTarget.Track;
            m_trackSampleTarget.Track = null;
            m_currentRecordingSampleCountLimit = -1;            
            return track;
        }

        /// <summary>
        /// Given a beat amount, how many timepoints is it?
        /// </summary>
        int BeatsToTimepoints(double beats)
        {
            return (int)(beats / m_clock.BeatsPerSecond * TimepointFrequencyHz);
        }

        /// <summary>
        /// Consume incoming audio via our DSP function, and copy it into our recording track (if any).
        /// </summary>
		void InputDspProc(int handle, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            if (lengthBytes == 0 || buffer == IntPtr.Zero) {
                return;
            }

            // should be multiple of 4 since it's a float stream
            Debug.Assert((lengthBytes & 0x3) == 0);

            m_sampleTarget.PushSampleData(buffer, lengthBytes >> 2);
        }            

        void Plm_Rec_Notification(object sender, EventArgs e)
        {
            if (m_plmRec != null) {
                m_levelL = m_plmRec.LevelL;
                m_levelR = m_plmRec.LevelR;
            }
        }

        public int InputLevelL { get { return m_levelL; } }
        public int InputLevelR { get { return m_levelR; } }

        public float CpuUsage { get { return BassAsio.BASS_ASIO_GetCPU() + Bass.BASS_GetCPU(); } }

        #region IDisposable Members

        public void Dispose()
        {
            m_wavEncoder.Stop();

            // close bass
            BassAsio.BASS_ASIO_Free();
            Bass.BASS_Free(); 
        }

        #endregion
    }
}
