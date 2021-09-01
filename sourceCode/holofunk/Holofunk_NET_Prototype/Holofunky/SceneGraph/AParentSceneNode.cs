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
    /// Abstract superclass of all SceneGraph nodes containing children.
    /// </summary>
    public abstract class AParentSceneNode : ASceneNode
    {
        // For now, we use a simple linear list.  Eventually perhaps some kind of
        // spatial data structure....
        readonly List<ASceneNode> m_children = new List<ASceneNode>();

        protected AParentSceneNode(AParentSceneNode parent, Transform localTransform) : base(parent, localTransform)
        {
        }

        protected List<ASceneNode> Children
        {
            get { return m_children; }
        }

        /// <summary>
        /// Attach this child to this parent, returning a function that will provide the
        /// parent's local-to-world matrix for this child on demand.
        /// </summary>
        /// <remarks>
        /// The function allows the child to obtain its transform matrix without exposing
        /// any details of how the parent maintains or represents it.
        /// </remarks>
        public void AttachChild(ASceneNode child)
        {
            m_children.Add(child);
        }

        /// <summary>
        /// Remove this child.
        /// </summary>
        /// <remarks>
        /// O(N) but N is expected to be tiny.
        /// </remarks>
        public void DetachChild(ASceneNode child)
        {
            Debug.Assert(m_children.Contains(child));

            m_children.Remove(child);
        }
    }
}
