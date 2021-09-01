////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>The status of a given Loopie.</summary>
    [Flags]
    enum LoopieCondition
    {
        Loop = 0x2,
        Mute = 0x4,
    }

    /// <summary>The state of Holofunk as a whole, viewed from the LoopieStateMachine.</summary>
    /// <remarks>This is really mostly a passive container class with some helper methods on it.
    /// External code currently manages the state quite imperatively.  This contains two
    /// PlayerStates, among lots of other content.</remarks>
    class HolofunkModel : Model
    {
        // basic component access
        readonly Clock m_clock;
        readonly HolofunkSceneGraph m_sceneGraph;
        readonly List<Loopie> m_loopies = new List<Loopie>();

        // loopies that should be removed on the next world update (to avoid concurrent modification of loopie list)
        readonly List<Loopie> m_loopiesToRemove = new List<Loopie>();

        readonly HolofunkBass m_bass;
        readonly HolofunKinect m_kinect;

        readonly HolofunkTextureContent m_holofunkContent;
        readonly Vector2 m_viewportSize;

        readonly PlayerModel m_player0;
        readonly PlayerModel m_player1;

        WiimoteController m_wiimote0;
        WiimoteController m_wiimote1;

        // new requested BPM value, if any -- the Wiimote thread updates this, and
        // the XNA update thread actually changes the clock (to avoid racing against
        // the main metronome BeatNode)
        float m_requestedBPM;

        long m_updateCount;
        long m_totalTickCount;

        /// <summary>Are we showing the secondary view in the secondary window?  (If not, the primary view is shown.)</summary>
        HolofunkView m_secondaryView = HolofunkView.Secondary;

        internal HolofunkModel(
            GraphicsDevice graphicsDevice,
            Clock clock,
            HolofunkBass bass, 
            HolofunKinect kinect,
            HolofunkTextureContent content,
            Vector2 viewportSize,
            float initialBPM)
        {
            m_clock = clock;
            m_bass = bass;
            m_kinect = kinect;
            m_viewportSize = viewportSize;
            m_holofunkContent = content;

            m_requestedBPM = initialBPM;

            m_sceneGraph = new HolofunkSceneGraph(
                graphicsDevice,
                m_viewportSize,
                m_kinect.PlayerTexture,
                m_holofunkContent,
                bass,
                m_clock);

            m_player0 = new PlayerModel(0, HolofunkBassAsio.AsioInputChannelId0, this);
            m_player1 = new PlayerModel(1, HolofunkBassAsio.AsioInputChannelId1, this);
        }

        internal Clock Clock { get { return m_clock; } }
        internal HolofunkSceneGraph SceneGraph { get { return m_sceneGraph; } }
        internal List<Loopie> Loopies { get { return m_loopies; } }
        internal List<Loopie> LoopiesToRemove { get { return m_loopiesToRemove; } }
        internal HolofunkBass BassAudio { get { return m_bass; } }
        internal HolofunKinect Kinect { get { return m_kinect; } }
        internal HolofunkTextureContent Content { get { return m_holofunkContent; } }

        internal PlayerModel Player0 { get { return m_player0; } }
        internal PlayerModel Player1 { get { return m_player1; } }

        internal long UpdateCount { get { return m_updateCount; } set { m_updateCount = value; } }
        internal long TotalTickCount { get { return m_totalTickCount; } set { m_totalTickCount = value; } }

        internal HolofunkView SecondaryView { get { return m_secondaryView; } set { m_secondaryView = value; } }

        internal PlayerModel GetPlayerModel(int playerIndex)
        {
            switch (playerIndex) {
                case 0: return m_player0;
                case 1: return m_player1;
                default: HoloDebug.Assert(false, "unknown player"); return null;
            }
        }

        internal void SetWiimote(int index, WiimoteController c)
        {
            if (index == 0) {
                m_wiimote0 = c;
            }
            else if (index == 1) {
                m_wiimote1 = c;
            }
            else {
                HoloDebug.Assert(false, "should not happen");
            }
        }

        // The requested BPM.
        internal float RequestedBPM { get { return m_requestedBPM; } set { m_requestedBPM = value < 10 ? 10 : value; } }

        public override void Update(Moment now)
        {
            m_sceneGraph.Update(this, Kinect, m_wiimote0, m_wiimote1, now, m_updateCount, m_totalTickCount);
        }
    }
}
