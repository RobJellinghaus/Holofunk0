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

namespace Holofunk.SceneGraphs
{
    /// <summary>A node class that draws little boxes and lights one of them up, to represent how
    /// many beats long a particular track is.</summary>
    /// <remarks>The node renders its drawing horizontally centered and below its target transform
    /// position.</remarks>
    public class BeatNode : ASceneNode
    {
        // how long is the track we purport to be rendering?
        Func<Moment> m_trackLengthFunc;

        // on what beat (since the beginning of time) did the "track" begin?
        // This is necessary to get the phase right for short 
        Func<int> m_initialBeatFunc;

        // what color is our base color?
        Func<Color> m_colorFunc;

        // what beat are we on now?  (mutated only by Update method)
        long m_currentBeat;
        // of how many beats?
        long m_totalBeats;
        // what fractional beat is it now?
        double m_fractionalBeat;
        
        public BeatNode(
            AParentSceneNode parent,
            Transform localTransform,
            string label,
            Func<Moment> trackLengthFunc,
            Func<int> initialBeatFunc,
            Func<Color> colorFunc)
            : base(parent, localTransform, label)
        {
            m_trackLengthFunc = trackLengthFunc;
            m_initialBeatFunc = initialBeatFunc;
            m_colorFunc = colorFunc;
        }

        public void Update(Moment now)
        {
            // what is our track length?
            Moment length = m_trackLengthFunc();
            m_totalBeats = length.CompleteBeats;

            if (m_totalBeats == 0) {
                // there is no actual track duration here; we do nothing
                return;
            }

            // must be an exact number of beats
            HoloDebug.Assert(length.TimepointsSinceLastBeat == 0);

            // make sure it matches our 4/4 expectation
            HoloDebug.Assert(
                m_totalBeats == 1 
                || m_totalBeats == 2 
                || (m_totalBeats & 0x3) == 0);

            // what beat are we actually on now?
            long nowBeat = now.CompleteBeats;
            // what beat did we start at?
            int initialBeat = m_initialBeatFunc();

            // if we got a -1 for initial beat, we also don't really exist
            // (this is a bit of a sleazy way to handle the ASIO race condition that exists
            // because ASIO may change state between m_trackLengthFunc() and m_initialBeatFunc())
            if (initialBeat == -1) {
                m_totalBeats = 0;
                return;
            }

            // how many beats is that from when we started recording?
            long beatsSinceTrackStart = nowBeat - initialBeat;

            // what is that modulo our number of beats?
            m_currentBeat = beatsSinceTrackStart % m_totalBeats;
            m_fractionalBeat = now.FractionalBeat;

            HoloDebug.Assert(m_fractionalBeat >= 0);
            HoloDebug.Assert(m_fractionalBeat < 1);
        }

        internal override void DoRender(
            GraphicsDevice graphicsDevice,
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform,
            int depth)
        {
            if (m_totalBeats == 0) {
                // there is no actual track here; we do not render
                return;
            }

            if (view == HolofunkView.Secondary) {
                // no dots for you, audience
                return;
            }

            Transform combinedTransform = parentTransform.CombineWith(LocalTransform);

            // what is the width of the grid of beat-boxes (heh) that we will be drawing?
            long gridWidth = m_totalBeats < 4 ? m_totalBeats : 4;

            // how many rows of boxes?
            long gridHeight = (m_totalBeats + 3) / 4;

            Rectangle rect = TextureRect(content.Dot, combinedTransform.Scale);

            Vector2 gridOrigin = combinedTransform.Translation;
            gridOrigin.X -= ((gridWidth * rect.Width) / 2);

            Color color = m_colorFunc();

            Spam.Graphics.WriteLine(new string(' ', depth * 4) + Label + ": parentTransform " + parentTransform + ", localTransform " + LocalTransform + ", combinedTransform " + combinedTransform + "; gridW " + gridWidth + ", gridH " + gridHeight + "; total beats " + m_totalBeats + ", current beat " + m_currentBeat + ", color " + color.ToString());

            // draw the grid
            for (int j = 0; j < gridHeight; j++) {
                for (int i = 0; i < gridWidth; i++) {
                    DrawSquare(spriteBatch, content.Dot, rect, gridOrigin, i, j, color, depth);
                }
            }

            // and draw the lone filled square
            long i2 = m_currentBeat % gridWidth;
            long j2 = m_currentBeat / gridWidth;

            // scale its alpha appropriately; remember, premultiplied alpha!
            float alpha = (float)((color.A / 255f) * (1 - m_fractionalBeat));
            color = new Color(
                (byte)(color.R * alpha),
                (byte)(color.G * alpha), 
                (byte)(color.B * alpha), 
                (byte)(color.A * alpha));
            DrawSquare(spriteBatch, content.BigDot, rect, gridOrigin, (int)i2, (int)j2, color, depth);
        }

        // Draw one of the squares at a grid coordinate.
        void DrawSquare(ISpriteBatch spriteBatch, Texture2D texture, Rectangle rect, Vector2 gridOrigin, int i, int j, Color color, int depth)
        {
            Vector2 position = gridOrigin;
            position.X += i * rect.Width;
            position.Y += j * rect.Height;

            Rectangle destRect = new Rectangle(
                rect.Left + (int)position.X,
                rect.Top + (int)position.Y,
                rect.Right + (int)position.X,
                rect.Bottom + (int)position.Y);
            // destRect.Offset((int)position.X, (int)position.Y);

            Spam.Graphics.WriteLine(new string(' ', depth * 4 + 4) + Label + ": grid dot: i " + i + ", j " + j + ", destRect " + destRect.FormatToString());

            // Use NonPremultiplied, as our sprite textures are not premultiplied
            spriteBatch.Begin(SpriteSortMode.Deferred, spriteBatch.GraphicsDevice.BlendStates.NonPremultiplied);

            spriteBatch.Draw(
                texture,
                destRect,
                null,
                color,
                0,
                Vector2.Zero,
                SpriteEffects.None,
                0);

            spriteBatch.End();
        }

        static Rectangle TextureRect(Texture2D texture, Vector2 scale)
        {
            Rectangle rect = new Rectangle(0, 0, (int)(texture.Width * scale.X), (int)(texture.Height * scale.Y));
            return rect;
        }
    }
}

