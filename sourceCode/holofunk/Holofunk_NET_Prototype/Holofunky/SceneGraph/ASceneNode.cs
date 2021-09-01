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
    /// Abstract superclass of all SceneGraph nodes.
    /// </summary>
    /// <remarks>
    /// Defines common operations such as obtaining the local transform, rendering,
    /// and picking.
    /// 
    /// Heavily based on _Essential Mathematics for Games and Interactive Applications:
    /// A Programmer's Guide_, James M. Van Verth and Lars M. Bishop, 2004.
    /// </remarks>
    public abstract class ASceneNode
    {
        /// <summary>
        /// The parent SceneNode, if any.
        /// </summary>
        /// <remarks>
        /// A SceneGraph has exactly one root ASceneNode.
        /// </remarks>
        readonly AParentSceneNode m_parent;

        /// <summary>
        /// Local transformation owned by this node, used for calculating parent-visible coordinates.
        /// </summary>
        /// <remarks>
        /// This transform applies to the bounding box exposed to the parent.
        /// </remarks>
        Transform m_localTransform = Transform.Identity;

        public ASceneNode(AParentSceneNode parent, Transform localTransform)
        {
            m_parent = parent;
            m_localTransform = localTransform;
            if (parent != null) {
                parent.AttachChild(this);
            }
        }

        /// <summary>
        /// Render this node and all its children.  
        /// </summary>
        /// <remarks>
        /// This is called on every Game.Draw() cycle.
        /// </remarks>
        public abstract void Render(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch);

        /// <summary>
        /// The local transform managed by this node, that it exposes to the parent.
        /// </summary>
        public Transform LocalTransform
        {
            get { return m_localTransform; }
            set { m_localTransform = value; }
        }

        public AParentSceneNode Parent
        {
            get { return m_parent; }
        }

        public void Detach()
        {
            Debug.Assert(m_parent != null);

            m_parent.DetachChild(this);
        }
    }
}
