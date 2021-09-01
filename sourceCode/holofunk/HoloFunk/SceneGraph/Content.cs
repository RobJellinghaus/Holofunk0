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
    /// <summary>Container for resources, implemented elsewhere.</summary>
    public abstract class TextureContent
    {
        public abstract Texture2D BigDot { get; }
        public abstract Texture2D Dot { get; }
        public abstract Texture2D EffectCircle { get; }
        public abstract Texture2D FilledCircle { get; }
        public abstract Texture2D FilledSquare { get; }
        public abstract Texture2D HollowCircle { get; }
        public abstract Texture2D HollowFace0 { get; }
        public abstract Texture2D HollowFace1 { get; }
        public abstract Texture2D HollowFace2 { get; }
        public abstract Texture2D HollowOneOval { get; }
        public abstract Texture2D HollowOval { get; }
        public abstract Texture2D HollowSquare { get; }
        public abstract Texture2D HollowTwoOval { get; }
        public abstract Texture2D LessHollowSquare { get; }
        public abstract Texture2D Microphone { get; }
        public abstract Texture2D MicrophoneHighlighted { get; }
        public abstract Texture2D MuteCircle { get; }
        public abstract Texture2D RecordCircle { get; }
        public abstract Texture2D RewindCircle { get; }
        public abstract Texture2D TinyDot { get; }
        public abstract Texture2D UnmuteCircle { get; }
                                                                                      
        public abstract SpriteFont SpriteFont { get; }
    }
}
