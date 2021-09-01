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
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.AddOn.Vst;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>This class manages all interaction with BASS ASIO, including all ASIO and other callback procedures.</summary>
    /// <remarks>Communication between HolofunkBass and HolofunkBassAsio happens by means of the two
    /// synchronized queues.
    /// 
    /// All methods in this class, except for the constructor and StartAsio(), are intended to
    /// be called from the ASIO thread only.</remarks>
    public class HolofunkBassAsio : IDisposable
    {
        // Constants defined by BassASIO.  I am not entirely sure how these are chosen, but they
        // are consistent in the examples and -- on my M-Audio Fast Track Pro USB sound box --
        // they work.
        internal const int AsioInputDeviceId = 0;
        internal const int AsioOutputDeviceId = 1;
        public const int AsioInputChannelId0 = 0;
        public const int AsioInputChannelId1 = 1;
        internal const int AsioOutputChannelId = 0;
        internal const bool IsInputChannel = true;
        internal const bool IsOutputChannel = false;

        /// <summary>How many channels is stereo?</summary>
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

        /// <summary>The HolofunkBass with which we communicate; we get its queues from here.</summary>
        readonly HolofunkBass m_bass;

        /// <summary>Destination of all recorded audio</summary>
        readonly SamplePool<float> m_samplePool;

        /// <summary>The pool of preallocated push streams and associated (maybe VST) effects</summary>
        BassStreamPool m_streamPool;

        /// <summary>The form to use as the initial handle for new VST embedded editors.</summary>
        Form m_baseForm;

        /// <summary>Mixer stream (HSTREAM)... yes, it's a bit Hungarian... consider wrapping BASS API to distinguish int-based types....</summary>
        /// <remarks>Only accessed by [AsioThread]</remarks>
        StreamHandle m_mixerHStream;

        /// <summary>ASIOPROC to feed mixer stream data to ASIO output</summary>
        ASIOPROC m_mixerToOutputAsioProc;

        /// <summary>newly added stream volume ratio</summary>
        internal const float TopMixVolume = 1f; // MAGIC NUMBER

        /// <summary>friggin' wav encoder!  want something done right, got to have BASS do it :-)</summary>
        EncoderWAV m_wavEncoder;

        int m_asioBufferPreferredSize;

        // Mutated by [AsioThread]
        // Read by [MainThread]
        Clock m_clock;

        /// <summary>Input for channel 0</summary>
        HolofunkBassAsioInput m_input0;

        /// <summary>Input for channel 1</summary>
        HolofunkBassAsioInput m_input1;

        /// <summary>How many timepoints earlier do we start a track?  Originates in the MagicNumbers class.</summary>
        int m_earlierDurationInTimepoints;

        internal HolofunkBassAsio(HolofunkBass bass, Clock clock, int earlierDurationInTimepoints)
        {
            m_bass = bass;

            // This will allocate a mere 1GB or so!!!!!
            m_samplePool = new SamplePool<float>();

            m_mixerToOutputAsioProc = new ASIOPROC(MixerToOutputAsioProc);

            m_clock = clock;

            m_earlierDurationInTimepoints = earlierDurationInTimepoints;
        }

        internal HolofunkBass HolofunkBass { get { return m_bass; } }

        internal Clock Clock { get { return m_clock; } }

        internal StreamHandle MixerHStream { get { return m_mixerHStream; } }

        internal SynchronizedQueue<AsioResponse> AsioToMainQueue { get { return m_bass.AsioToMainQueue; } }

        internal SamplePool<float> SamplePool { get { return m_samplePool; } }

        internal BassStreamPool StreamPool { get { return m_streamPool; } }

        internal int StreamPoolFreeCount { get { return m_streamPool.FreeCount; } }

        /// <summary>ASIOPROC to feed mixer stream data to ASIO output buffer.</summary>
        /// <remarks>[AsioThread]</remarks>
        int MixerToOutputAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            HoloDebug.Assert(input == IsOutputChannel);
            HoloDebug.Assert(channel == AsioOutputChannelId);

            // ChannelGetData here is populating the output buffer for us.
            int bytesAvailable = Bass.BASS_ChannelGetData((int)m_mixerHStream, buffer, lengthBytes);

            // Stereo sample pairs x 4 bytes/sample = shift by 3.
            m_clock.AddSampleCount(lengthBytes >> 3);

            Moment now = m_clock.Now;

            // And update ourselves, by pulling any commands and completing any tracks that are done.
            Update(now);

            return bytesAvailable;
        }

        /// <summary>Initialize the base ASIO streams, and actually start ASIO running.</summary>
        /// <remarks>[MainThread]</remarks>
        internal void StartASIO()
        {
            // not playing anything via BASS, so don't need an update thread
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATEPERIOD, 0);

            // setup BASS - "no sound" device but SampleFrequencyHz (default for ASIO)
            Bass.BASS_Init(0, Clock.TimepointRateHz, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            BassAsio.BASS_ASIO_Init(AsioInputDeviceId, BASSASIOInit.BASS_ASIO_THREAD);

            BassFx.LoadMe();
            BassVst.LoadMe();

            // testing scaffolding; retain for reference
            // TempFrobBassVst();

            ////////////////////// DEVICE SETUP

            BassAsio.BASS_ASIO_SetDevice(AsioOutputDeviceId);
            BassAsio.BASS_ASIO_SetRate(Clock.TimepointRateHz);

            BassAsio.BASS_ASIO_SetDevice(AsioInputDeviceId);
            BassAsio.BASS_ASIO_SetRate(Clock.TimepointRateHz);

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

            m_mixerHStream = (StreamHandle)BassMix.BASS_Mixer_StreamCreate(
                Clock.TimepointRateHz,
                StereoChannels,
                BASSFlag.BASS_MIXER_RESUME | BASSFlag.BASS_MIXER_NONSTOP | BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);

            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            CheckError(Bass.BASS_ChannelGetInfo((int)m_mixerHStream, mixerInfo));

            // connect to ASIO output channel
            CheckError(BassAsio.BASS_ASIO_ChannelEnable(IsOutputChannel, AsioOutputChannelId, m_mixerToOutputAsioProc, new IntPtr((int)m_mixerHStream)));

            // Not really sure about the next few lines, but it's all from the BASS documentation for ASIOPROC...
            // and it seems to work....

            // Join second mixer channel (right stereo channel).
            CheckError(BassAsio.BASS_ASIO_ChannelJoin(IsOutputChannel, 1, AsioOutputChannelId));

            CheckError(BassAsio.BASS_ASIO_ChannelSetFormat(IsOutputChannel, AsioOutputChannelId, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT));
            CheckError(BassAsio.BASS_ASIO_ChannelSetRate(IsOutputChannel, AsioOutputChannelId, Clock.TimepointRateHz));

            // start recording dammit!!!
            // setup an encoder on the asio input channel
            // Note: this will write a 32-bit, 48kHz, stereo Wave file
            const bool saveWav = false;
            if (saveWav) {
                m_wavEncoder = new EncoderWAV((int)m_mixerHStream);
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
                m_wavEncoder.Start(null, IntPtr.Zero, false);
            }

            ////////////////////// INPUT SETUP

            CheckError(BassAsio.BASS_ASIO_SetDevice(HolofunkBassAsio.AsioInputDeviceId));

            m_input0 = new HolofunkBassAsioInput(this, 0, m_earlierDurationInTimepoints);
            m_input1 = new HolofunkBassAsioInput(this, 1, m_earlierDurationInTimepoints);

            ////////////////////// ASIO LAUNCH

            CheckError(BassAsio.BASS_ASIO_Start(m_asioBufferPreferredSize));

            // get the info again, see if latency has changed
            asioInfo = BassAsio.BASS_ASIO_GetInfo();
            inputLatency = BassAsio.BASS_ASIO_GetLatency(IsInputChannel);
            outputLatency = BassAsio.BASS_ASIO_GetLatency(IsOutputChannel);

        }

        internal void SetBaseForm(Form baseForm)
        {
            ////////////////////// STREAM POOL PREALLOCATION
            const int streamPoolCapacity = 50;
            m_streamPool = new BassStreamPool(streamPoolCapacity, baseForm);
            m_baseForm = baseForm;
        }

        internal Form BaseForm { get { return m_baseForm; } }

        // Purely test scaffolding for BassVst; retain for future reference.
        void TempFrobBassVst()
        {
            BASSError err;

            StreamHandle vstStream = (StreamHandle)BassVst.BASS_VST_ChannelSetDSP(
                0,
                Path.Combine(System.Environment.CurrentDirectory.ToString(), "Turnado.dll"),
                BASSVSTDsp.BASS_VST_DEFAULT,
                0);

            err = Bass.BASS_ErrorGetCode();

            int paramCount = BassVst.BASS_VST_GetParamCount((int)vstStream);
            BASS_VST_PARAM_INFO[] paramInfos = new BASS_VST_PARAM_INFO[paramCount];

            for (int paramIndex = 0; paramIndex < paramCount; paramIndex++) {
                paramInfos[paramIndex] = BassVst.BASS_VST_GetParamInfo((int)vstStream, paramIndex);
            }

            int programCount = BassVst.BASS_VST_GetProgramCount((int)vstStream);

            string[] programNames = new string[0];

            for (int k = 0; k < 10; k++) {
                programCount = BassVst.BASS_VST_GetProgramCount((int)vstStream);
                int prevLen = programNames.Length;
                string[] newNames = new string[prevLen + programCount];
                int index = 0;
                foreach (string s in programNames) {
                    newNames[index++] = s;
                }
                programNames = newNames;

                for (int i = 0; i < programCount; i++) {
                    programNames[index++] = BassVst.BASS_VST_GetProgramName((int)vstStream, i);
                }
                string name16 = BassVst.BASS_VST_GetProgramName((int)vstStream, programCount + 1);
                HoloDebug.Assert(name16 == null);
            }

            BASS_VST_INFO vstInfo = new BASS_VST_INFO();
            if (BassVst.BASS_VST_GetInfo((int)vstStream, vstInfo) && vstInfo.hasEditor) {
                // create a new System.Windows.Forms.Form
                Form f = new Form();
                f.Width = vstInfo.editorWidth + 4;
                f.Height = vstInfo.editorHeight + 34;
                f.Closing += (sender, e) => f_Closing(sender, e, vstStream);
                f.Text = vstInfo.effectName;
                f.Show();
                BassVst.BASS_VST_EmbedEditor((int)vstStream, f.Handle);
            }
        }

        void f_Closing(object sender, System.ComponentModel.CancelEventArgs e, StreamHandle vstStream)
        {
            // unembed the VST editor
            BassVst.BASS_VST_EmbedEditor((int)vstStream, IntPtr.Zero);
        }

        internal void CheckError(bool ok)
        {
            if (!ok) {
                BASSError error = BassAsio.BASS_ASIO_ErrorGetCode();
                string str = error.ToString();

                BASSError error2 = Bass.BASS_ErrorGetCode();
                string str2 = error2.ToString();

                string s = str + str2;
            }
        }

        internal HolofunkBassAsioInput GetInput(int channel)
        {
            switch (channel) {
                case 0: return m_input0;
                case 1: return m_input1;
                default: HoloDebug.Assert(false, "Invalid channel"); return null;
            }
        }

        /// <summary>Add track to mixer.</summary>
        /// <remarks>[MainThread]
        /// 
        /// This is safe provided that m_samplePool is thread-safe (which it is) and 
        /// provided that trackSyncProc is prepared to be called immediately.</remarks>
        /// <param name="trackHStream">HSTREAM of the track to add.</param>
        /// <param name="trackUserData">Track's user data.</param>
        /// <param name="trackSync">the syncproc that will push more track data</param>
        internal void AddStreamToMixer(int trackHStream)
        {
            bool ok;

            BASS_CHANNELINFO trackInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo(trackHStream, trackInfo);
            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo((int)m_mixerHStream, mixerInfo);

            ok = BassMix.BASS_Mixer_StreamAddChannel(
                (int)m_mixerHStream,
                trackHStream,
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN);

            // try setting to 40% volume to reduce over-leveling
            ok = Bass.BASS_ChannelSetAttribute(trackHStream, BASSAttribute.BASS_ATTRIB_VOL, (float)TopMixVolume);

            ok = BassMix.BASS_Mixer_ChannelPlay(trackHStream);

            // end the current chunk so we don't overlap data in a single chunk
            // (wild-assed guess about the occasional mangled track/chunk that seems to come up infrequently....)
            m_samplePool.EndChunk();
        }

        /// <summary>Remove this stream from the mixer's inputs.</summary>
        /// <remarks>[MainThread] but hard to see how this could be racy given proper BASS multithread handling.
        /// Since no evidence of any issues there, will leave this alone.</remarks>
        internal void RemoveStreamFromMixer(StreamHandle trackHStream)
        {
            bool ok = BassMix.BASS_Mixer_ChannelRemove((int)trackHStream);
        }

        /// <summary>Number from 0 to 1, representing the amount of available recording space that has been used</summary>
        /// <remarks>Updated by [AsioThread], read by [MainThread]</remarks>
        public float FractionOccupied { get { return m_samplePool.FractionOccupied; } }

        /// <summary>Called from ASIO output proc to execute any outstanding commands from main thread,
        /// and to check current recording and end it if it's over limit </summary>
        /// <remarks>[AsioThread]
        /// 
        /// Sends a TrackComplete response if the track actually finishes.</remarks>
        void Update(Moment now)
        {
            m_input0.PreUpdate(now);
            m_input1.PreUpdate(now);

            // first, take any commands off the queue and run them
            AsioCommand command;
            while (m_bass.MainToAsioQueue.TryDequeue(out command)) {
                GetInput(command.Channel).Execute(command, now);
            }

            m_input0.Update(now);
            m_input1.Update(now);
        }

        /// <summary>Given a beat amount, how many timepoints is it?</summary>
        int BeatsToTimepoints(double beats)
        {
            return (int)(beats / m_clock.BeatsPerSecond * Clock.TimepointRateHz);
        }

        public float CpuUsage { get { return BassAsio.BASS_ASIO_GetCPU() + Bass.BASS_GetCPU(); } }

        #region IDisposable Members

        public void Dispose()
        {
            if (m_wavEncoder != null) {
                m_wavEncoder.Stop();
            }

            // close bass
            BassVst.FreeMe();
            BassFx.FreeMe();

            BassAsio.BASS_ASIO_Free();
            Bass.BASS_Free();
        }

        #endregion
    }
}
