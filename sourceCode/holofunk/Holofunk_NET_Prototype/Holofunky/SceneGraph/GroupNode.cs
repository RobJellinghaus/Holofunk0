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
    /// A simple container node rendering children sequentially.
    /// </summary>
    public class GroupNode : AParentSceneNode
    {
        public GroupNode(AParentSceneNode parent, Transform localTransform)
            : base(parent, localTransform)
        {
        }

        public override void Render(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Render(graphicsDevice, spriteBatch);
            }
        }
    }
}
