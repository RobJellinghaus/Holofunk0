////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
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

namespace Holofunk.SceneGraphs
{
    /// <summary>
    /// Simple node class containing a square texture (e.g. a sprite).
    /// </summary>
    /// <remarks>
    /// The extent of the texture is considered to be its entire area, regardless of transparency.
    /// 
    /// The origin of the texture is the texture's center.
    /// </remarks>
    public class TextNode : ASceneNode
    {
        string m_text;

        Color m_color = Color.White;

        SpriteFont m_font;

        public TextNode(AParentSceneNode parent, string text, SpriteFont font)
            : base(parent, Transform.Identity)
        {
            m_text = text;
            m_font = font;
        }

        /// <summary>
        /// The color to tint when rendering.
        /// </summary>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        public string Text
        {
            get { return m_text; }
            set { m_text = value; }
        }

        public override void Render(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

            spriteBatch.DrawString(m_font, m_text, new Vector2(30, 20), Color);

            spriteBatch.End();
        }
    }
}
