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
using SharpDX.Toolkit.Content;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Container for resources defined in HolofunkContent project.</summary>
    public class HolofunkTextureContent : TextureContent
    {
        const string ext = ".png";

        Texture2D m_bigDot;
        Texture2D m_dot;
        Texture2D m_effectCircle;
        Texture2D m_filledCircle;
        Texture2D m_filledSquare;
        Texture2D m_hollowCircle;
        Texture2D m_hollowFace0;
        Texture2D m_hollowFace1;
        Texture2D m_hollowFace2;
        Texture2D m_hollowOneOval;
        Texture2D m_hollowOval;
        Texture2D m_hollowSquare;
        Texture2D m_hollowTwoOval;
        Texture2D m_lessHollowSquare;
        Texture2D m_microphone;
        Texture2D m_microphoneHighlighted;
        Texture2D m_muteCircle;
        Texture2D m_recordCircle;
        Texture2D m_rewindCircle;
        Texture2D m_tinyDot;
        Texture2D m_unmuteCircle;

        SpriteFont m_spriteFont;

        public HolofunkTextureContent(ContentManager content)
        {
            m_bigDot = content.Load<Texture2D>("20x20_big_dot" + ext);
            m_dot = content.Load<Texture2D>("20x20_dot" + ext);
            m_effectCircle = content.Load<Texture2D>("EffectCircle" + ext);
            m_filledCircle = content.Load<Texture2D>("FilledCircle" + ext);
            m_filledSquare = content.Load<Texture2D>("20x20_filled_square" + ext);
            m_hollowCircle = content.Load<Texture2D>("HollowCircle" + ext);
            m_hollowFace0 = content.Load<Texture2D>("HollowFace0" + ext);
            m_hollowFace1 = content.Load<Texture2D>("HollowFace1" + ext);
            m_hollowFace2 = content.Load<Texture2D>("HollowFace2" + ext);
            m_hollowOneOval = content.Load<Texture2D>("HollowOneOval" + ext);
            m_hollowOval = content.Load<Texture2D>("HollowOval" + ext);
            m_hollowSquare = content.Load<Texture2D>("20x20_hollow_square" + ext);
            m_hollowTwoOval = content.Load<Texture2D>("HollowTwoOval" + ext);
            m_lessHollowSquare = content.Load<Texture2D>("20x20_less_hollow_square" + ext);
            m_microphone = content.Load<Texture2D>("Microphone" + ext);
            m_microphoneHighlighted = content.Load<Texture2D>("MicrophoneHighlighted" + ext);
            m_muteCircle = content.Load<Texture2D>("MuteCircle" + ext);
            m_recordCircle = content.Load<Texture2D>("RecCircle" + ext);
            m_rewindCircle = content.Load<Texture2D>("RewindCircle" + ext);
            m_tinyDot = content.Load<Texture2D>("2x2_filled_square" + ext);
            m_unmuteCircle = content.Load<Texture2D>("UnmuteCircle" + ext);

            m_spriteFont = content.Load<SpriteFont>("Arial16.tkfnt");
        }

        public override Texture2D BigDot { get { return m_bigDot; } }
        public override Texture2D Dot { get { return m_dot; } }
        public override Texture2D EffectCircle { get { return m_effectCircle; } }
        public override Texture2D FilledCircle { get { return m_filledCircle; } }
        public override Texture2D FilledSquare { get { return m_filledSquare; } }
        public override Texture2D HollowCircle { get { return m_hollowCircle; } }
        public override Texture2D HollowFace0 { get { return m_hollowFace0; } }
        public override Texture2D HollowFace1 { get { return m_hollowFace1; } }
        public override Texture2D HollowFace2 { get { return m_hollowFace2; } }
        public override Texture2D HollowOneOval { get { return m_hollowOneOval; } }
        public override Texture2D HollowOval { get { return m_hollowOval; } }
        public override Texture2D HollowSquare { get { return m_hollowSquare; } }
        public override Texture2D HollowTwoOval { get { return m_hollowTwoOval; } }
        public override Texture2D LessHollowSquare { get { return m_lessHollowSquare; } }
        public override Texture2D Microphone { get { return m_microphone; } }
        public override Texture2D MicrophoneHighlighted { get { return m_microphoneHighlighted; } }
        public override Texture2D MuteCircle { get { return m_muteCircle; } }
        public override Texture2D RecordCircle { get { return m_recordCircle; } }
        public override Texture2D RewindCircle { get { return m_rewindCircle; } }
        public override Texture2D TinyDot { get { return m_tinyDot; } }
        public override Texture2D UnmuteCircle { get { return m_unmuteCircle; } }

        public override SpriteFont SpriteFont { get { return m_spriteFont; } }
    }
}
