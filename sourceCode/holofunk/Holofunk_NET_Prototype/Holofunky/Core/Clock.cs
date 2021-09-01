////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>
    /// Tracks the current time (driven from the ASIO input sample DSP), and 
    /// converts it to seconds and beats.
    /// </summary>
    /// <remarks>
    /// Since the ASIO thread is fundamentally driving the time, the current clock
    /// reading is subject to change out from under the UI thread.  So the Clock
    /// hands out immutable Moment instances, which represent the time at the moment the
    /// clock was asked.  Moments in turn can be converted to timepoint-counts, 
    /// seconds, and beats, consistently and without racing.
    /// </remarks>
    public class Clock
    {
        /// <summary>
        /// The rate of sound measurements (individual sample data points) per second.
        /// </summary>
        public const int TimepointRateHz = 48000;

        // The current BPM of this Clock.
        int m_beatsPerMinute;

        // The number of samples -- NOT timepoints! -- since the beginning of Holofunk.
        // The Clock needs to keep samples internally, as evidently a stereo channel in
        // BassASIO can contain an odd number of samples.  For instance, an ASIOPROC
        // input buffer length can be 1036, which is divisible by four (since it's a
        // float channel), but not by eight (one sample per channel).
        //
        // So to prevent roundoff error, which would accumulate very quickly, we track
        // the samples themselves but we expose a timepoint count (e.g. sample count >> 1
        // if the input channel is stereo).
        int m_sampleLength;

        // How many input channels are there?
        readonly int m_inputChannelCount;

        const double TicksPerSecond = 10 * 1000 * 1000;

        public Clock(int beatsPerMinute, int inputChannelCount)
        {
            m_beatsPerMinute = beatsPerMinute;
            m_inputChannelCount = inputChannelCount;
        }

        /// <summary>
        /// Add the given number of samples to the time tracked by this Clock.
        /// </summary>
        public void AddSampleLength(int additionalSampleLength)
        {
            m_sampleLength += additionalSampleLength;
        }

        public Moment Now
        {
            // Stereo channel, hence twice as many samples as timepoints.
            get 
            { 
                return new Moment(m_sampleLength / m_inputChannelCount, m_beatsPerMinute); 
            }
        }

        public double BeatsPerSecond
        {
            get { return ((double)m_beatsPerMinute) / 60.0; }
        }
    }

    /// <summary>
    /// Moments are immutable points in time, that can be converted to various
    /// time measurements (timepoint-count, second, beat).
    /// </summary>
    public class Moment
    {
        /// <summary>
        /// The number of timepoints since the start of Holofunk.
        /// </summary>
        readonly int m_timepointCount;

        /// <summary>
        /// The number of beats per second.
        /// </summary>
        readonly double m_beatsPerSecond;

        internal Moment(int timepointCount, int beatsPerMinute)
        {
            m_timepointCount = timepointCount;
            m_beatsPerSecond = ((double)beatsPerMinute) / 60;
        }

        public int TimepointCount { get { return m_timepointCount; } }

        public double Seconds { get { return ((double)TimepointCount) / Clock.TimepointRateHz; } }

        public double Beats { get { return Seconds * m_beatsPerSecond; } }
    }
}
