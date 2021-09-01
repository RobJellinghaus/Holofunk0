////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>The visual representation of a Loopie, which may or may not still be being recorded.</summary>
    /// <remarks>This class is parameterized by functions which it polls to get the state of the model
    /// (e.g. track) underlying it.
    /// 
    /// This is actually </remarks>
    class TrackNode : AParentSceneNode
    {
        // same as loopie's ID
        readonly int m_id;

        // The node representing our sound
        readonly SpriteNode m_soundNode;
        
        // The node representing our highlight when touched
        readonly SpriteNode m_selectNode;

        // The function we poll for our volume level ratio
        readonly Func<float> m_levelRatioFunc;

        // The function we poll for our color
        readonly Func<Color> m_colorFunc;

        // The node representing our beats
        BeatNode m_beatNode;

        readonly static Color[] s_colors = new[] {
            Color.Blue,
            Color.Purple,
            Color.SeaGreen,
            Color.Honeydew,
            Color.DarkOrchid,
            Color.Aqua,
            Color.Magenta,
            Color.SteelBlue,
            Color.Tomato
        };

        internal TrackNode(
            AParentSceneNode parent,
            Transform transform,
            string label,
            TextureContent content,
            int id,
            Func<float> levelRatioFunc,
            Func<Color> colorFunc,
            Func<Moment> trackDurationFunc,
            Func<int> initialBeatFunc)
            : base(parent, transform, label)
        {
            m_id = id;

            m_levelRatioFunc = levelRatioFunc;
            m_colorFunc = colorFunc;

            // create this first so it is Z-ordered behind m_soundNode
            m_selectNode = new SpriteNode(this, "TrackHighlight", content.FilledCircle);
            m_selectNode.Color = Color.White;
            m_selectNode.Origin = new Vector2(0.5f);

            m_soundNode = new SpriteNode(this, "TrackSound", content.FilledCircle);
            m_soundNode.Color = Color.Blue;
            m_soundNode.Origin = new Vector2(0.5f);

            m_beatNode = new BeatNode(
                this,
                // move it down a bit from the sprite node
                new Transform(new Vector2(0, 25), new Vector2(0.5f)),
                "TrackBeats",
                trackDurationFunc,
                initialBeatFunc,
                colorFunc);
            m_beatNode.SetSecondaryViewOption(SecondaryViewOption.Hidden);

            // we always mirror track node position
            SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);
        }

        internal void Update(Moment now, bool touched, Color playerColor)
        {
            m_soundNode.LocalTransform = new Transform(
                m_soundNode.LocalTransform.Translation, 
                new Vector2(MagicNumbers.LoopieScale) * m_levelRatioFunc());

            m_soundNode.Color = m_colorFunc();

            // just SLIGHTLY larger scale than the sound node
            m_selectNode.LocalTransform = new Transform(
                m_soundNode.LocalTransform.Translation,
                new Vector2(m_soundNode.LocalTransform.Scale.X + 0.05f));
            m_selectNode.Color = touched ? playerColor : new Color(0);

            m_beatNode.Update(now);
        }        

        internal int Id { get { return m_id; } }
    }
}
