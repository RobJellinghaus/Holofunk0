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
    public interface ISpriteBatch
    {
        // TODO: what is Game.Viewport?  Whatever type it is, it has width and height, that would be better here
        Vector2 Viewport { get; }

        void Begin();
        void Begin(SpriteSortMode spriteSortMode, BlendState blendState);

        void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            Vector2 position,
            DrawingRectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            SpriteEffects spriteEffects,
            float layerDepth);

        void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            DrawingRectangle sourceRectangle,
            DrawingRectangle? destRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            SpriteEffects spriteEffects,
            float layerDepth);

        void DrawString(
            SpriteFont spriteFont,
            StringBuilder text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            float scale,
            SpriteEffects spriteEffects,
            float layerDepth);

        void End();

        GraphicsDevice GraphicsDevice { get; }
    }
}
