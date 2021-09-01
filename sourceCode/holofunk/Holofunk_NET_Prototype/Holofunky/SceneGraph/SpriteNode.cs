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
    public class SpriteNode : ASceneNode
    {
        Texture2D m_texture;
        Vector2 m_origin;
        Color m_color = Color.White;

        public SpriteNode(AParentSceneNode parent, Texture2D texture)
            : base(parent, new Transform(new Vector2(-texture.Width / 2, -texture.Height  / 2), Vector2.One))
        {
            m_texture = texture;
        }

        /// <summary>
        /// The sprite texture.
        /// </summary>
        public Texture2D Texture
        {
            get { return m_texture; }
            set { m_texture = value; }
        }

        /// <summary>
        /// The color to tint when rendering.
        /// </summary>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        /// <summary>
        /// The origin of the sprite texture; 0,0 is upper left, 1,1 is lower right.
        /// </summary>
        public Vector2 Origin
        {
            get 
            { 
                return m_origin; 
            }
            set 
            {
                Debug.Assert(value.X >= 0f && value.X <= 1f);
                Debug.Assert(value.Y >= 0f && value.Y <= 1f);
                m_origin = value; 
            }
        }

        public override void Render(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            // no texture = no-op
            if (m_texture == null) {
                return;
            }

            Rectangle rect = new Rectangle(
                -(int)((float)m_texture.Width * m_origin.X),
                -(int)((float)m_texture.Height * m_origin.Y),
                m_texture.Width,
                m_texture.Height) 
                * LocalTransform;

            spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

            spriteBatch.Draw(
                m_texture,
                rect,
                null,
                m_color,
                0,
                m_origin,
                SpriteEffects.None,
                0);

            spriteBatch.End();
        }
    }
}
