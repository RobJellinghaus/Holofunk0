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

namespace Holofunk
{
    /// <summary>
    /// A sound track, capable of recording or looping.
    /// </summary>
    /// <remarks>
    /// This handles playing itself, managing sound buffers, etc.  Its state machine
    /// pertains only to sound.
    /// </remarks>
    class Loop
    {
        enum State
        {
            Stopped,
            Recording,
            Looping
        }

        State m_state = State.Stopped;
        byte[] m_data;
        MemoryStream m_stream = new MemoryStream();
        SoundEffect m_currentSoundEffect;
        TimeSpan m_playStopTime = new TimeSpan(long.MaxValue);
        EventHandler<EventArgs> m_bufferReady;

        Microphone Microphone { get { return Microphone.Default; } }

        public Loop()
        {
             m_bufferReady = new EventHandler<EventArgs>(BufferReady);
        }

        public void Record()
        {
            Debug.Assert(m_state == State.Stopped);

            m_data = new byte[Microphone.GetSampleSizeInBytes(Microphone.BufferDuration)];
            m_stream.Seek(0, SeekOrigin.Begin);
            Microphone.BufferReady += m_bufferReady;

            Microphone.Start();

            m_state = State.Recording;
        }

        void BufferReady(object sender, EventArgs e)
        {
            Microphone.Default.GetData(m_data);
            m_stream.Write(m_data, 0, m_data.Length);
        }

        public void Play()
        {
            Debug.Assert(m_state == State.Stopped);

            m_currentSoundEffect = new SoundEffect(m_stream.ToArray(), Microphone.SampleRate, AudioChannels.Mono);
            var duration = m_currentSoundEffect.Duration;

            m_currentSoundEffect.Play(1f, 0f, 0f);

            m_state = State.Looping;
        }

        public void Stop()
        {
            if (m_state == State.Recording)
            {
                Microphone.Stop();
                Microphone.BufferReady -= m_bufferReady;
            }
        }

        internal void Update(GameTime gameTime)
        {
            if (m_playStopTime < gameTime.TotalGameTime)
            {
                // we were playing and now we're not
                m_currentSoundEffect = null;
                m_playStopTime = new TimeSpan(long.MaxValue);
            }
        }
    }
}
