////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Kinect;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Content;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>A scene graph for Holofunk itself, with all sorts of customized accessors, events, etc.</summary>
    class HolofunkSceneGraph : SceneGraph
    {
        /// <summary>The color of silence.</summary>
        public static readonly Color MuteColor = new Color((byte)50, (byte)50, (byte)50, (byte)255);
        
        readonly HolofunkBass m_audio;
        readonly HolofunkTextureContent m_content;
        readonly Clock m_clock;

        /// <summary>The pulsing rainbow background.</summary>
        readonly SpriteNode m_background;

        /// <summary>The status text.</summary>
        readonly TextNode m_statusText;

        /// <summary>A little tiddly current-beat indicator in bottom center.</summary>
        readonly BeatNode m_beatNode;

        /// <summary>The layer into which all tracks go.</summary>
        readonly GroupNode m_trackGroupNode;

        /// <summary>How large is our canvas?</summary>
        readonly Vector2 m_canvasSize;

        /// <summary>current frame count; used to determine when to update status text</summary>
        int m_frameCount;

        /// <summary>number of ticks in a second; a tick = 100 nanoseconds</summary>
        const long TicksPerSecond = 10 * 1000 * 1000;

        internal HolofunkSceneGraph(
            GraphicsDevice graphicsDevice,
            Vector2 canvasSize,
            Texture2D depthTexture,
            HolofunkTextureContent holofunkContent,
            HolofunkBass audio,
            Clock clock)
            : base()
        {
            m_content = holofunkContent;
            m_clock = clock;

            RootNode = new GroupNode(null, Transform.Identity, "Root");
            m_canvasSize = canvasSize;

            m_background = new SpriteNode(
                RootNode,
                "Background",
                TextureFactory.ShadedCornerColor(
                    graphicsDevice,
                    canvasSize,
                    Color.Black,
                    Color.Lime,
                    Color.Blue,
                    Color.Red));
            m_background.LocalTransform = Transform.Identity;

            // constructing the nodes adds them as children of the parent, in first-at-bottom Z order.

            SpriteNode depthNode = new SpriteNode(
                RootNode,
                "DepthImage",
                depthTexture);
            depthNode.LocalTransform = new Transform(
                Vector2.Zero,
                new Vector2((float)canvasSize.X / depthTexture.Width, (float)canvasSize.Y / depthTexture.Height));

            // we want the depth node texture (only) to be mirrored about the center of the viewport
            depthNode.SetSecondaryViewOption(SecondaryViewOption.TextureMirrored); // B4CR: should this also be | PositionMirrored?

            m_audio = audio;

            // Center the textures.
            Vector2 origin = new Vector2(0.5f);

            m_statusText = new TextNode(RootNode, "StatusText");
            m_statusText.SetSecondaryViewOption(SecondaryViewOption.Hidden);
            m_statusText.LocalTransform = new Transform(new Vector2(30f, 20f), new Vector2(0.8f));

            // make sure that first update pushes status text
            m_frameCount = MagicNumbers.StatusTextUpdateInterval - 1;

            m_beatNode = new BeatNode(
                RootNode,
                new Transform(new Vector2(m_canvasSize.X / 2, m_canvasSize.Y / 8 * 7)),
                "Root Beater",
                () => clock.Time(clock.TimepointsPerBeat * 4),
                () => 0,
                () => Color.White);

            m_trackGroupNode = new GroupNode(RootNode, Transform.Identity, "Track group");
        }

        internal HolofunkTextureContent Content { get { return m_content; } }

        internal Vector2 ViewportSize { get { return m_canvasSize; } }
        internal int TextureRadius { get { return m_content.HollowCircle.Width; } }

        internal HolofunkBass Audio { get { return m_audio; } }

        internal Clock Clock { get { return m_clock; } }

        internal GroupNode TrackGroupNode { get { return m_trackGroupNode; } }

        /// <summary>Update the scene's background based on the current beat, and the positions of
        /// the two hands based on the latest data polled from Kinect.</summary>
        /// <remarks>We pass in the current value of "now" to ensure consistent
        /// timing between the PlayerState update and the scene graph udpate.</remarks>
        internal void Update(
            HolofunkModel holofunkModel,
            HolofunKinect kinect,
            WiimoteController wiimote0,
            WiimoteController wiimote1,
            Moment now,
            long updateCount,
            long totalTickCount)
        {
            // should do this once a second or so, to reduce garbage...
            if (++m_frameCount == MagicNumbers.StatusTextUpdateInterval) {
                m_frameCount = 0;

                m_statusText.Text.Clear();

                float durationInSeconds = totalTickCount / TicksPerSecond;
                float updatesPerSecond = updateCount / durationInSeconds;

                float averagePaintTicksInMs = 0;
                
                m_statusText.Text.AppendFormat(
                    "BPM: {0} | Updates per second: {1} | Avg paint time (ms): {2}\nBattery: {3}%{4} | Space: {5}%  | Free streams: {6} | CPU: {7}%\n",
                    Clock.BPM,
                    Math.Floor(updatesPerSecond * 10) / 10,
                    averagePaintTicksInMs,
                    Math.Floor(wiimote0.BatteryLevel * 10) / 10,
                    (wiimote1 != null ? ("/" + (Math.Floor(wiimote1.BatteryLevel * 10) / 10) + "%") : ""),
                    100f - (Math.Floor(Audio.FractionOccupied * 1000f) / 10),
                    Audio.StreamPoolFreeCount,
                    Math.Floor(Audio.CpuUsage * 10) / 10);

                // m_statusText.Color = Color.Red;
            }

            // Scale to byte and use for all RGBA components (premultiplied alpha, don't you know)
            Color backgroundColor = FloatScaleToRGBAColor(1 - now.FractionalBeat);
            m_background.Color = backgroundColor;
            m_background.SecondaryColor = backgroundColor;

            m_beatNode.Update(now);

            Spam.Graphics.WriteLine("EndUpdate");
        }

        static Color FloatScaleToRGBAColor(double scale)
        {
            scale *= 255;
            byte byteScale = (byte)scale;
            Color scaleColor = new Color(byteScale, byteScale, byteScale, byteScale);
            return scaleColor;
        }
    }
}
