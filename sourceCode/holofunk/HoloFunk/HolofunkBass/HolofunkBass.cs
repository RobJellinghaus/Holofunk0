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
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    // The ASIO thread and the main thread communicate through synchronized command /
    // response queues.  These are the commands sent by the main thread.
    enum AsioCommandType
    {
        // Start recording more or less immediately
        StartRecording,

        // Stop the current recording at the next beat boundary
        StopRecordingAtNextBeat,
    }

    // A command from the main thread to the ASIO thread
    struct AsioCommand
    {
        internal readonly int Channel;
        internal readonly AsioCommandType Type;
        internal readonly int Param;
        internal readonly ParameterMap StartingParameters;

        internal AsioCommand(int channel, AsioCommandType type, int param, ParameterMap startingParameters)
        {
            Channel = channel;
            Type = type;
            Param = param;
            StartingParameters = startingParameters;
        }
    }

    // These are the responses from the ASIO thread to the main thread.
    enum AsioResponseType
    {
        // A track has finished recording; the track itself is in the response
        TrackComplete,
    }

    // A response from the ASIO thread to the main thread
    struct AsioResponse
    {
        internal int Channel;
        internal AsioResponseType Type;
        internal Track<float> Track;

        internal AsioResponse(int channel, AsioResponseType type, Track<float> track)
        {
            Channel = channel;
            Type = type;
            Track = track;
        }
    }

    /// <summary>A queue which synchronizes its enqueues and dequeues, and which asserts that each
    /// is called by a single different thread.</summary>
    /// <remarks>Oddly, there is a Queue.Synchronized method, but no Queue<typeparam name="T"/>.Synchronized
    /// method....</remarks>
    class SynchronizedQueue<T>
    {
        readonly Queue<T> m_queue = new Queue<T>();

        internal void Enqueue(T t)
        {
            lock (this) {
                m_queue.Enqueue(t);
            }
        }

        internal bool TryDequeue(out T t)
        {
            lock (this) {
                if (m_queue.Count == 0) {
                    t = default(T);
                    return false;
                }
                else {
                    t = m_queue.Dequeue();
                    return true;
                }
            }
        }
    }

    /// <summary>Manager object for almost all interaction with the BASS library.</summary>
    /// <remarks>Manages recording, track creation, mixing, and generally all other top-level functions.
    /// 
    /// The term "timepoint" is used here to mean "a point in time at which a sample was taken."
    /// Since our channels are stereo, there are technically two mono samples per timepoint.
    /// Therefore "sample count" is a bit ambiguous -- are those mono samples or stereo samples?
    /// To avoid confusion, we use "timepoint" analogously to "sample" whenever we are calculating
    /// purely based on time.
    /// 
    /// This object is created by the main thread, and manages communication from the main thread
    /// to the ASIO thread, as well as exposing responses from the ASIO thread.  There are two
    /// communication queues: one from the main thread to ASIO, and one the other way.  This
    /// ensures that the main Holofunk game thread can communicate with the ASIO thread in an
    /// orderly and synchronized manner; the ASIO thread, via the HolofunkBassAsio object,
    /// manages essentially all the ASIO state.</remarks>
    public class HolofunkBass : IDisposable
    {
        readonly HolofunkBassAsio m_asio;

        readonly SynchronizedQueue<AsioCommand> m_mainToAsioQueue;
        readonly SynchronizedQueue<AsioResponse> m_asioToMainQueue;

        public HolofunkBass(Clock clock, int earlierDurationInTimepoints)
        {
            m_mainToAsioQueue = new SynchronizedQueue<AsioCommand>();
            m_asioToMainQueue = new SynchronizedQueue<AsioResponse>();

            m_asio = new HolofunkBassAsio(this, clock, earlierDurationInTimepoints);
        }

        public void SetBaseForm(Form baseForm)
        {
            m_asio.SetBaseForm(baseForm);
        }

        /// <summary>Set up and start the ASIO subsystem running.</summary>
        /// <remarks>[MainThread], obviously.</remarks>
        public void StartASIO()
        {
            m_asio.StartASIO();
        }

        // Add this HSTREAM to our mixer.  (Called by Track.)
        internal void AddStreamToMixer(StreamHandle streamHandle)
        {
            m_asio.AddStreamToMixer((int)streamHandle);
        }

        // Remove this HSTREAM from our mixer.  (Called by Track.)
        internal void RemoveStreamFromMixer(StreamHandle streamHandle)
        {
            m_asio.RemoveStreamFromMixer(streamHandle);
        }

        public int StreamPoolFreeCount { get { return m_asio.StreamPoolFreeCount; } }

        public BassStream Reserve()
        {
            return m_asio.StreamPool.Reserve();
        }

        public void Free(BassStream bassStream)
        {
            m_asio.StreamPool.Free(bassStream);
        }

        /// <summary>Number from 0 to 1, representing the amount of available recording space that has been used</summary>
        /// <remarks>Updated by [AsioThread], read by [MainThread]</remarks>
        public float FractionOccupied { get { return m_asio.FractionOccupied; } }

        /// <summary>Are we currently recording?</summary>
        /// <remarks>Updated and read by [MainThread], also read by [AsioThread]</remarks>
        public bool IsRecording(int channel)
        {
            return m_asio.GetInput(channel).IsRecording;
        }

        /// <summary>The level value we show as maximum volume when rendering.</summary>
        /// <remarks>Chosen purely by feel with Rob Jellinghaus's specific hardware....</remarks>
        const int MaxLevel = 7000;

        internal float CalculateLevelRatio(int inputLevelL, int inputLevelR)
        {
            int maxLevel = Math.Max(inputLevelL, inputLevelR) / 3; 

            return (float)Math.Min(1f, (Math.Log(maxLevel, 2) / Math.Log(MaxLevel, 2)));
        }

        /// <summary>Normalize this level to the interval [0, 1], clamping at MaxLevel.</summary>
        public float LevelRatio(int channel)
        {
            HolofunkBassAsioInput input = m_asio.GetInput(channel);
            return CalculateLevelRatio(input.InputLevelL, input.InputLevelR);
        }

        public float CpuUsage { get { return m_asio.CpuUsage; } }

        public int GetCurrentRecordingBeatCount(int channel)
        {
            return m_asio.GetInput(channel).CurrentRecordingBeatCount; 
        }

        public int GetCurrentRecordingStartBeat(int channel)
        { 
            return m_asio.GetInput(channel).CurrentRecordingStartBeat; 
        }

        internal SynchronizedQueue<AsioCommand> MainToAsioQueue { get { return m_mainToAsioQueue; } }
        internal SynchronizedQueue<AsioResponse> AsioToMainQueue { get { return m_asioToMainQueue; } }

        /// <summary>As part of the main thread's update loop, check whether any tracks have completed.</summary>
        /// <param name="doneTrack"></param>
        /// <returns></returns>
        public bool TryUpdate(out int channel, out Track<float> doneTrackIfAny)
        {
            AsioResponse response;
            bool got = AsioToMainQueue.TryDequeue(out response);
            if (got) {
                channel = response.Channel;
                doneTrackIfAny = response.Track;
            }
            else {
                channel = -1;
                doneTrackIfAny = null;
            }
            return got;
        }

        /// <summary>Start recording a track with the given ID.</summary>
        public void StartRecording(int channel, int id, ParameterMap startingParameters)
        {
            HoloDebug.Assert(channel == 0 || channel == 1);

            MainToAsioQueue.Enqueue(new AsioCommand(channel, AsioCommandType.StartRecording, id, startingParameters));
        }

        /// <summary>Stop recording the track currently being recorded, at the next beat.</summary>
        public void StopRecordingAtCurrentDuration(int channel)
        {
            MainToAsioQueue.Enqueue(new AsioCommand(
                channel,
                AsioCommandType.StopRecordingAtNextBeat,
                /* ignored */ 0,
                /* ignored */ null));
        }

        /// <summary>Update the effect parameters on the input channel.</summary>
        /// <remarks>[MainThread]
        /// 
        /// This calls directly through to m_asio WITHOUT queueing.  This is because the
        /// effect parameter setting is thread-safe, and the set of parameters and effects
        /// will not change during this operation.</remarks>
        public void UpdateMicrophoneParameters(int channel, ParameterMap set, Moment now)
        {
            m_asio.GetInput(channel).UpdateMicrophoneParameters(set, now);
        }

        public void Dispose()
        {
            m_asio.Dispose();
        }
    }
}
