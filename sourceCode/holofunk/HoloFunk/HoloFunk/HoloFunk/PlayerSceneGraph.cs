////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Kinect;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to an individual player.</summary>
    class PlayerSceneGraph : SceneGraph
    {
        /// <summary>The parent scene graph in which this player participates</summary>
        readonly HolofunkSceneGraph m_parent;

        /// <summary>The recently averaged volume level ratio (for making funny faces).</summary>
        readonly FloatAverager m_averageLevelRatio;

        readonly int m_playerIndex;
        readonly int m_channel;

        internal readonly PlayerBodySceneGraph Body;

        internal PlayerSceneGraph(HolofunkSceneGraph parent, int playerIndex, int channel)
            : base()
        {
            m_parent = parent;

            m_playerIndex = playerIndex;
            m_channel = channel;

            m_averageLevelRatio = new FloatAverager(15); // don't flicker face changes too fast
            m_averageLevelRatio.Update(0); // make sure it's initially zero

            // Center the textures.
            Vector2 origin = new Vector2(0.5f);

            RootNode = new GroupNode(parent.RootNode, Transform.Identity, "Player #" + playerIndex);

            Body = new PlayerBodySceneGraph(this);
        }

        internal int PlayerIndex { get { return m_playerIndex; } }
        internal int Channel { get { return m_channel; } }
        internal TextureContent Content { get { return m_parent.Content; } }
        internal HolofunkBass Audio { get { return m_parent.Audio; } }
        internal Clock Clock { get { return m_parent.Clock; } }
        internal FloatAverager AverageLevelRatio { get { return m_averageLevelRatio; } }

        internal Vector2 WiiHandPosition { get { return Body.WiiHandGroup.LocalTransform.Translation; } }

        // TODO: abstract over this; use real world space coordinates instead of screen space calculations everywher
        internal Vector2 ViewportSize { get { return m_parent.ViewportSize; } }
        // TODO: abstract over this; use real world space coordinates instead of screen space calculations everywher
        internal int TextureRadius { get { return m_parent.TextureRadius; } }

        /// <summary>Update the scene's background based on the current beat, and the positions of
        /// the two hands based on the latest data polled from Kinect.</summary>
        /// <remarks>We pass in the current value of "now" to ensure consistent
        /// timing between the PlayerState update and the scene graph udpate.</remarks>
        internal void Update(
            PlayerModel playerState,
            HolofunKinect kinect,
            Moment now)
        {
            Body.Update(playerState, kinect, now);
        }
    }
}
