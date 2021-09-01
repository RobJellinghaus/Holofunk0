////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

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
    /// A hierarchy of ASceneNodes, supporting render and pick operations.
    /// </summary>
    /// <remarks>
    /// Note that a SceneGraph contains only spatial state; it describes an instantaneous
    /// snapshot of a scene (specifically, *the* instantaneous snapshot next to be rendered).
    /// If animation is desired it needs to be performed externally through some means,
    /// probably IObservable interaction with Rx.
    /// 
    /// TBD exactly how we "layer animation behavior" onto a scene graph as such....
    /// </remarks>
    public class SceneGraph
    {
        AParentSceneNode m_rootNode;

        public SceneGraph()
        {
        }

        public AParentSceneNode RootNode
        {
            get { return m_rootNode; }
            set { m_rootNode = value; }
        }

        public void Render(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            if (RootNode != null) {
                RootNode.Render(graphicsDevice, spriteBatch);
            }
            
            //
            // MAGIC ALERT!
            //
            // Was getting this exception previously:
            // "You may not call SetData on a resource while it is actively set on the GraphicsDevice. 
            // Unset it from the device before calling SetData."
            //
            // This post:
            // http://forums.create.msdn.com/forums/p/60865/374981.aspx#374981
            // and especially this post:
            // http://forums.create.msdn.com/forums/p/69440/423719.aspx#423719 
            // led to the magic below, which seems reasonably robust.
            // 
            // Still, keep an eye on this, as per Shawn Hargreaves again:
            // http://blogs.msdn.com/b/shawnhar/archive/2008/04/15/stalls-part-two-beware-of-setdata.aspx
            //
            for (int i = 0; i < 16; i++) {
                if (graphicsDevice.Textures[i] != null) {
                    graphicsDevice.Textures[i] = null;
                }
            }
        }
    }
}
