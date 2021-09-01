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

namespace Holofunk.Core
{
    /// <summary>
    /// Simple scale-translate transform.
    /// </summary>
    /// <remarks>
    /// This represents the scale and translation explicitly, to facilitate the mathematically constrained
    /// amongst us.  
    /// </remarks>
    public struct Transform
    {
        readonly Vector2 m_translation;
        readonly Vector2 m_scale;

        public Transform(Vector2 translation, Vector2 scale)
        {
            m_translation = translation;
            m_scale = scale;
        }

        public Vector2 Translation 
        { 
            get { return m_translation; } 
        }

        public Vector2 Scale 
        { 
            get { return m_scale; } 
        }

        /*
        public static Transform operator *(Transform one, Transform other)
        {
            return new Transform(one.Scale * other.Translation + one.Translation, one.Scale);
        }
         */

        public static Vector2 operator *(Vector2 vector, Transform xform)
        {
            return vector * xform.Scale + xform.Translation;
        }

        public static Rectangle operator *(Rectangle rect, Transform xform)
        {
            Vector2 upperCorner = new Vector2(rect.Left, rect.Top);
            Vector2 lowerCorner = new Vector2(rect.Right, rect.Bottom);
            Vector2 upperXformCorner = upperCorner * xform;
            Vector2 lowerXformCorner = lowerCorner * xform;
            return new Rectangle(
                (int)upperXformCorner.X, 
                (int)upperXformCorner.Y, 
                (int)(lowerXformCorner.X - upperXformCorner.X), 
                (int)(lowerXformCorner.Y - upperXformCorner.Y));
        }

        public static Transform operator +(Transform xform, Vector2 offset)
        {
            return new Transform(xform.Translation + offset, xform.Scale);
        }

        public static Transform Identity
        {
            get { return new Transform(Vector2.Zero, Vector2.One); }
        }
    }
}
