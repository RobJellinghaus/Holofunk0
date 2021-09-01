////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    /// <summary>A simple container node rendering children sequentially.</summary>
    public class GroupNode : AParentSceneNode
    {
        public GroupNode(AParentSceneNode parent, Transform localTransform, string label)
            : base(parent, localTransform, label)
        {
        }
    }
}
