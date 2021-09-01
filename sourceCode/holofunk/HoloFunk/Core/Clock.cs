////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>Tracks the current time (driven from the ASIO input sample DSP), and 
    /// converts it to seconds and beats.</summary>
    /// <remarks>Since the ASIO thread is fundamentally driving the time, the current clock
    /// reading is subject to change out from under the UI thread.  So the Clock
    /// hands out immutable Moment instances, which represent the time at the moment the
    /// clock was asked.  Moments in turn can be converted to timepoint-counts, 
    /// seconds, and beats, consistently and without racing.</remarks>
    public class Clock
    {
        /// <summary>The rate of sound measurements (individual sample data points) per second.</summary>
        public const int TimepointRateHz = 48000;

        // The current BPM of this Clock.
        float m_beatsPerMinute;

        // The beats per MEASURE.  e.g. 3/4 time = 3 beats per measure.
        // TODO: make this actually mean something; it is only partly implemented right now.
        readonly int m_beatsPerMeasure;

        // The number of samples -- NOT timepoints! -- since the beginning of Holofunk.
        // The Clock needs to keep samples internally, as evidently a stereo channel in
        // BassASIO can contain an odd number of samples.  For instance, an ASIOPROC
        // input buffer length can be 1036, which is divisible by four (since it's a
        // float channel), but not by eight (one sample per channel).
        //
        // So to prevent roundoff error, which would accumulate very quickly, we track
        // the samples themselves but we expose a timepoint count (e.g. sample count >> 1
        // if the input channel is stereo).
        long m_sampleCount;

        // How many input channels are there?
        readonly int m_inputChannelCount;

        // How many timepoints per beat?
        // This is THE critical number that actually sets the tempo of the entire
        // experience.  This is an integer, so there is no roundoff -- we choose to
        // accept roundoff error in the number of seconds (e.g. we may not be exactly
        // on the requested BPM) rather than in the number of timepoints, because
        // ideally we want to play exactly this many timepoints for every beat.
        // TODO: This actually will lead to clock drift, and will need to be fixed at some point.
        int m_timepointsPerBeat;

        const double TicksPerSecond = 10 * 1000 * 1000;

        public Clock(float beatsPerMinute, int beatsPerMeasure, int inputChannelCount)
        {
            m_beatsPerMinute = beatsPerMinute;
            m_beatsPerMeasure = beatsPerMeasure;
            m_inputChannelCount = inputChannelCount;

            CalculateTimepointsPerBeat();
        }

        void CalculateTimepointsPerBeat()
        {
            m_timepointsPerBeat = (int)Math.Floor(((float)TimepointRateHz * 60f) / m_beatsPerMinute);
        }

        /// <summary>Add the given number of samples to the time tracked by this Clock.</summary>
        public void AddSampleCount(int additionalSampleCount)
        {
            m_sampleCount += additionalSampleCount;
        }

        /// <summary>The beats per minute of this clock.</summary>
        /// <remarks>This is the most useful value for humans to control and see, and in fact pretty much all 
        /// time in the system is derived from this.  This value can only currently be changed when
        /// no tracks exist.</remarks>
        public float BPM 
        { 
            get 
            { 
                return m_beatsPerMinute; 
            }
            set 
            { 
                m_beatsPerMinute = value;
                CalculateTimepointsPerBeat();
            } 
        }

        public Moment Now
        {
            // Stereo channel, hence twice as many samples as timepoints.
            get 
            { 
                return Time(m_sampleCount / m_inputChannelCount); 
            }
        }

        public Moment Time(long timepointCount)
        {
            return new Moment(timepointCount, m_timepointsPerBeat, m_beatsPerMeasure);
        }

        public double BeatsPerSecond
        {
            get { return ((double)m_beatsPerMinute) / 60.0; }
        }

        public int TimepointsPerBeat
        {
            get { return m_timepointsPerBeat; }
        }

        public int BeatsPerMeasure
        {
            get { return m_beatsPerMeasure; }
        }
    }

    /// <summary>Moments are immutable points in time, that can be converted to various
    /// time measurements (timepoint-count, second, beat).</summary>
    public struct Moment
    {
        /// <summary>The number of timepoints since the start of Holofunk.</summary>
        readonly long m_timepointCount;

        /// <summary>The number of timepoints per beat.</summary>
        readonly int m_timepointsPerBeat;

        /// <summary>The number of beats per measure.</summary>
        readonly int m_beatsPerMeasure;

        public static Moment Start = new Moment(0, 0, 0);

        internal Moment(long timepointCount, int timepointsPerBeat, int beatsPerMeasure)
        {
            HoloDebug.Assert(timepointCount >= 0);
            HoloDebug.Assert(timepointsPerBeat >= 0);
            HoloDebug.Assert(beatsPerMeasure >= 0);

            m_timepointCount = timepointCount;
            m_timepointsPerBeat = timepointsPerBeat;
            m_beatsPerMeasure = beatsPerMeasure;
        }

        // Exactly how many timepoints?
        public long TimepointCount { get { return m_timepointCount; } }

        /// <summary>Approximately how many seconds?</summary>
        public double Seconds { get { return ((double)TimepointCount) / Clock.TimepointRateHz; } }

        /// <summary>Approximately how many beats?</summary>
        /// <remarks>Note that because beats are measured in timepoints, not seconds, there will
        /// be some roundoff error -- a beat may be very slightly shorter or longer, in seconds,
        /// than the clock's BPM, if the TimepointHz value is not evenly divisible by the
        /// requested BPM.</remarks>
        public double Beats { get { return ((double)m_timepointCount) / ((double)m_timepointsPerBeat); } }

        /// <summary>Exactly how many complete beats?</summary>
        /// <remarks>Beats are represented by ints as it's hard to justify longs; 2G beats = VERY LONG TRACK</remarks>
        public int CompleteBeats { get { return (int)(m_timepointCount / m_timepointsPerBeat); } }

        /// <summary>What fraction of a beat?</summary>
        public double FractionalBeat { get { return Beats - CompleteBeats; } }

        /// <summary>How many timepoints since the last complete beat?</summary>
        public int TimepointsSinceLastBeat { get { return (int)(m_timepointCount % m_timepointsPerBeat); } }

        /// <summary>Approximately how many measures?</summary>
        public double Measures { get { return Beats / m_beatsPerMeasure; } }

        /// <summary>How many complete measures?</summary>
        public int CompleteMeasures { get { return CompleteBeats / m_beatsPerMeasure; } }

        /// <summary>What fraction of a measure are we at now?</summary>
        public double FractionalMeasure { get { return Measures - CompleteMeasures; } }

        /// <summary>What beat is it in the current measure?</summary>
        public int BeatInMeasure { get { return CompleteBeats % m_beatsPerMeasure; } }

        /// <summary>Get a new Moment that is this much earlier.</summary>
        public Moment EarlierByTimepoints(long timepointsAgo)
        {
            long earlierTime = Math.Max(0, m_timepointCount - timepointsAgo);
            return new Moment(earlierTime, m_timepointsPerBeat, m_beatsPerMeasure);
        }

        /// <summary>This moment offset by N beats.</summary>
        public Moment PlusBeats(int beats)
        {
            return new Moment(TimepointCount + (m_timepointsPerBeat * beats), m_timepointsPerBeat, m_beatsPerMeasure);
        }

        public override string ToString()
        {
            return "Moment[" + TimepointCount + " timepoints, " + Seconds + " secs, " + Beats + " beats, " + Measures + " measures]";
        }
    }
}
