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
    /// <summary>An abstract "widget" that allows control of a Loop.</summary>
    /// <remarks>Contains the user interaction state machine for creating and controlling loops.
    /// This is essentially the model/controller.</remarks>
    class Loopie
    {
        readonly int m_id;
        
        LoopieCondition m_condition;

        // Is this loopie currently touched by the cursor?
        bool m_touched;
        // color of the player touching it (or white if both players)
        Color m_touchedColor;

        readonly Track<float> m_track;
        readonly HolofunkSceneGraph m_sceneGraph;
        readonly TrackNode m_loopieNode;

        /// <summary>Index of the creating player.</summary>
        readonly int m_playerIndex;

        readonly static Color[] s_colors = new[] {
            Color.Blue,
            Color.Purple,
            Color.SeaGreen,
            Color.DarkOrchid,
            Color.Aqua,
            Color.Magenta,
            Color.SteelBlue,
            Color.Tomato,
            Color.Turquoise,
            Color.RoyalBlue,
            Color.MediumVioletRed,
            Color.Maroon,
            Color.LimeGreen,
            Color.HotPink
        };

        // Loop m_loop;

        internal Loopie(
            int id, 
            Track<float> track, 
            HolofunkSceneGraph sceneGraph, 
            TextureContent content,
            Transform transform,
            int playerIndex)
        {
            m_id = id;
            m_track = track;
            m_playerIndex = playerIndex;

            SetCondition(LoopieCondition.Loop);

            m_sceneGraph = sceneGraph;

            m_loopieNode = new TrackNode(
                m_sceneGraph.TrackGroupNode,
                transform, 
                "Track #" + id,
                content,
                id,
                // set the scale proportionately to the maximum level (on both channels)
                () => m_track.LevelRatio,
                // set the color: gray if muted, otherwise based on our unique ID
                () => m_condition == LoopieCondition.Mute 
                    ? HolofunkSceneGraph.MuteColor
                    : s_colors[m_id % s_colors.Length],
                () => m_track.LengthAsMoment,
                () => m_track.InitialBeat);
        }

        internal int Id { get { return m_id; } }
        internal LoopieCondition Condition { get { return m_condition; } }
        internal Transform Position { get { return m_loopieNode.LocalTransform; } }
        internal int PlayerIndex { get { return m_playerIndex; } }
        internal bool Touched 
        { 
            get 
            { return m_touched; } 
            set 
            { m_touched = value; } 
        }
        internal Color TouchedColor { get { return m_touchedColor; } set { m_touchedColor = value; } }
        internal Track<float> Track { get { return m_track; } }

        internal void SetCondition(LoopieCondition condition)
        {
            m_condition = condition;

            if (m_condition == LoopieCondition.Mute) {
                m_track.SetMuted(true);
            }
            else {
                m_track.SetMuted(false);
            }
        }

        internal void Update(Moment now)
        {
            m_loopieNode.Update(now, Touched, TouchedColor);
        }

        public void Dispose(Moment now)
        {
            m_track.SetMuted(true);
            m_track.Dispose(now);

            m_loopieNode.Detach();
        }
    }
}
