////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Holofunk.SceneGraphs
{
    /// <summary>Wrapper for a sprite batch that scales coordinates (and scale factors).</summary>
    public class SpriteBatchWrapper : ISpriteBatch
    {
        readonly SpriteBatch m_spriteBatch;
        readonly Vector2 m_viewport;
        readonly float m_scaleFactor;

        public SpriteBatchWrapper(SpriteBatch spriteBatch, Vector2 viewport, float scaleFactor)
        {
            m_spriteBatch = spriteBatch;
            m_viewport = viewport;
            m_scaleFactor = scaleFactor;
        }

        public Vector2 Viewport { get { return m_viewport; } }

        public void Begin()
        {
            m_spriteBatch.Begin();
        }

        public void Begin(SpriteSortMode spriteSortMode, BlendState blendState)
        {
            m_spriteBatch.Begin(spriteSortMode, blendState);
        }

        public void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            Vector2 position,
            DrawingRectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.Draw(
                texture,
                position * m_scaleFactor,
                sourceRectangle,
                color,
                rotation,
                origin,
                scale * m_scaleFactor,
                spriteEffects,
                layerDepth);
        }

        public void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            DrawingRectangle destRectangle,
            DrawingRectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.Draw(
                texture,
                new DrawingRectangle(
                    (int)(destRectangle.X * m_scaleFactor),
                    (int)(destRectangle.Y * m_scaleFactor),
                    (int)(destRectangle.Width * m_scaleFactor),
                    (int)(destRectangle.Height * m_scaleFactor)),
                sourceRectangle,
                color,
                rotation,
                origin,
                spriteEffects,
                layerDepth);
        }

        public void DrawString(
            SpriteFont spriteFont,
            StringBuilder text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            float scale,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.DrawString(
                spriteFont,
                text,
                position * m_scaleFactor,
                color,
                rotation,
                origin,
                scale * m_scaleFactor,
                spriteEffects,
                layerDepth);
        }

        public void End()
        {
            m_spriteBatch.End();
        }

        public GraphicsDevice GraphicsDevice { get { return m_spriteBatch.GraphicsDevice; } }
    }
}
