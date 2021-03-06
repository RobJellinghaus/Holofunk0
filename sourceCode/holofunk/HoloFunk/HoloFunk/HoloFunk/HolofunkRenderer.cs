////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

// Copyright (c) 2010-2012 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace Holofunk
{
    /// <summary>Renders the secondary view.</summary>
    public class HolofunkRenderer : GameWindowRenderer
    {
        SpriteBatch m_spriteBatch;
        Vector2 m_viewportSize;

        public HolofunkRenderer(Holofunk game, object windowContext = null)
            : base(game, windowContext)
        {
            BackgroundColor = Color.CornflowerBlue;
            ForegroundColor = Color.Red;
            Visible = true;

            m_viewportSize = game.ViewportSize;

            /*
            PreferredBackBufferFormat = PixelFormat.R8G8B8A8.UNorm;
             */
            PreferredBackBufferWidth = (int)game.ViewportSize.X * MagicNumbers.ScreenRatio;
            PreferredBackBufferHeight = (int)game.ViewportSize.Y * MagicNumbers.ScreenRatio;
        }

        /// <summary>Initializes a new instance of the <see cref="MiniTriRenderer" /> class.</summary>
        protected override void LoadContent()
        {
            Window.AllowUserResizing = true;
        }

        public Color BackgroundColor { get; set; }

        public Color ForegroundColor { get; set; }

        public override void Draw(GameTime gameTime)
        {
            if (m_spriteBatch == null) {
                m_spriteBatch = new SpriteBatch(GraphicsDevice);
            }

            SpriteBatchWrapper wrapper = new SpriteBatchWrapper(m_spriteBatch, m_viewportSize, MagicNumbers.ScreenRatio);

            ((Holofunk)Game).Render(
                GraphicsDevice, 
                wrapper, 
                gameTime, 
                ((Holofunk)Game).SecondaryView, 
                BackgroundColor);
        }
    }
}
