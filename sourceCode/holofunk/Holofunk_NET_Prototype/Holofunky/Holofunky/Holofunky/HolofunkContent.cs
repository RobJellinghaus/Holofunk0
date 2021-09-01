////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
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
    /// Container for resources defined in HolofunkContent project.
    /// </summary>
    public class HolofunkContent
    {
        Texture2D m_hollowCircle;
        Texture2D m_filledCircle;
        Texture2D m_microphone;

        SpriteFont m_spriteFont;

        public HolofunkContent(ContentManager content)
        {
            m_hollowCircle = content.Load<Texture2D>("HollowCircle");
            m_filledCircle = content.Load<Texture2D>("FilledCircle");
            m_microphone = content.Load<Texture2D>("Microphone");

            m_spriteFont = content.Load<SpriteFont>("SpriteFont1");
        }

        public Texture2D HollowCircle { get { return m_hollowCircle; } }
        public Texture2D FilledCircle { get { return m_filledCircle; } }
        public Texture2D Microphone { get { return m_microphone; } }

        public SpriteFont SpriteFont { get { return m_spriteFont; } }
    }
}
