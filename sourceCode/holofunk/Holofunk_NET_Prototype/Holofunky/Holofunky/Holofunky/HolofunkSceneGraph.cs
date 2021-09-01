////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Research.Kinect.Nui;
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

namespace Holofunk
{
    struct LevelScaledSprite
    {
        readonly SpriteNode m_node;
        readonly Transform m_transform;
        readonly Func<int> m_levelFunc;
        readonly Func<Color> m_colorFunc;

        internal LevelScaledSprite(SpriteNode node, Transform transform, Func<int> levelFunc, Func<Color> colorFunc)
        {
            m_node = node;
            m_transform = transform;
            m_levelFunc = levelFunc;
            m_colorFunc = colorFunc;
        }

        internal Transform Transform { get { return m_transform; } }
        internal SpriteNode Sprite { get { return m_node; } }
        internal Func<int> LevelFunc { get { return m_levelFunc; } }
        internal Func<Color> ColorFunc { get { return m_colorFunc; } }
    }

    /// <summary>
    /// A scene graph for Holofunk itself, with all sorts of customized accessors, events,
    /// etc.
    /// </summary>
    class HolofunkSceneGraph : SceneGraph
    {
        // If true, the Wii is in the right hand and the mike in the left.
        const bool RightHanded = true;

        public static readonly Color MuteColor = new Color(50, 50, 50);

        readonly SpriteNode m_background;
        readonly SpriteNode m_mikeHand;
        readonly SpriteNode m_wiiHand;
        readonly SpriteNode m_mikeSignal;
        readonly TextNode m_statusText;
        readonly Point m_canvasSize;

        readonly HolofunkContent m_content;

        readonly List<LevelScaledSprite> m_levelScaledSprites = new List<LevelScaledSprite>();

        // update status text every 10 frames, to conserve on garbage
        const int StatusTextUpdateInterval = 20;

        // current frame count; used to determine when to update status text
        int m_frameCount; 

        // Will point to one of m_leftHand or m_rightHand; represents the hand where the mike signal
        // should be drawn.
        ASceneNode m_mikeSignalNode;

        // number of ticks in a second; a tick = 100 nanoseconds
        const long TicksPerSecond = 10 * 1000 * 1000;

        internal HolofunkSceneGraph(
            GraphicsDevice graphicsDevice, 
            Point canvasSize, 
            Texture2D depthTexture,
            HolofunkContent holofunkContent)
            : base()
        {
            m_content = holofunkContent;

            RootNode = new GroupNode(null, new Transform());
            m_canvasSize = canvasSize;

            m_background = new SpriteNode(
                RootNode,
                TextureFactory.ShadedCornerColor(
                    graphicsDevice,
                    canvasSize,
                    Color.Black,
                    Color.Lime,
                    Color.Blue,
                    Color.Red));
            m_background.LocalTransform = Transform.Identity;

            // constructing the nodes adds them as children of the parent
            SpriteNode depthNode = new SpriteNode(
                RootNode,
                depthTexture);
            depthNode.LocalTransform = new Transform(
                Vector2.Zero,
                new Vector2((float)canvasSize.X / depthTexture.Width, (float)canvasSize.Y / depthTexture.Height));

            m_mikeSignal = new SpriteNode(RootNode, holofunkContent.FilledCircle);
            m_mikeSignal.Color = Color.Red;
            m_mikeSignal.Origin = new Vector2(0.5f);

            m_mikeHand = new SpriteNode(RootNode, holofunkContent.Microphone);
            m_mikeHand.Origin = new Vector2(0.5f);
            m_wiiHand = new SpriteNode(RootNode, holofunkContent.HollowCircle);
            m_wiiHand.Origin = new Vector2(0.5f);

            m_statusText = new TextNode(RootNode, "WOOOO", holofunkContent.SpriteFont);

            m_mikeSignalNode = m_mikeHand;

            // make sure that first update pushes status text
            m_frameCount = StatusTextUpdateInterval - 1;
        }

        /// <summary>
        /// Transform the given position, adding a scale proportionate to maxLevel.
        /// </summary>
        internal Transform LevelTransform(Transform positionTransform, int maxLevel)
        {
            // arbitrary 4096 constant here -- max is really 32768 but we want it to look dramatic
            Transform signalTransform = new Transform
                (positionTransform.Translation, 
                new Vector2(Math.Min(1.0f, ((float)maxLevel) / 2048)));

            return signalTransform;
        }

        internal void Update(HolofunKinect kinect, HolofunkBass audio, Clock clock)
        {
            // clock.Now gets updated by ASIO thread, so take a snapshot (e.g. Moment),
            // and use it throughout the update
            Moment now = clock.Now;

            m_mikeHand.LocalTransform = kinect.GetJointViewportPosition(
                RightHanded ? JointID.HandLeft : JointID.HandRight);
            m_wiiHand.LocalTransform = kinect.GetJointViewportPosition(
                RightHanded ? JointID.HandRight : JointID.HandLeft);

            int maxLevel = Math.Max(audio.InputLevelL, audio.InputLevelR);
            m_mikeSignal.LocalTransform = LevelTransform(m_mikeSignalNode.LocalTransform, maxLevel);

            // should do this once a second or so, to slash garbage...
            // should also pass time to Update method....
            if (++m_frameCount == StatusTextUpdateInterval) {
                m_frameCount = 0;

                m_statusText.Text = string.Format(
                    "Space used: {0}% - CPU: {1}% ",
                    audio.FractionOccupied * 100f,
                    Math.Floor(audio.CpuUsage * 10) / 10);
            }

            // get the beat
            double beats = now.Beats;
            // get the fractional part
            beats -= Math.Floor(beats);
            // invert
            beats = 1 - beats;
            // subtract 0.5, clamp to 0, and add 0.2 back (resulting in a ramp from 0.7 to 0.2)
            beats = Math.Max(beats - 0.5, 0) + 0.2;
            // multiply by 255
            beats *= 255;
            // go to int
            int intBeat = (int)beats;
            // scale color
            Color beatColor = new Color(intBeat, intBeat, intBeat, intBeat);
            m_background.Color = beatColor;

            foreach (LevelScaledSprite sprite in m_levelScaledSprites) {
                sprite.Sprite.LocalTransform = LevelTransform(sprite.Transform, sprite.LevelFunc());
                sprite.Sprite.Color = sprite.ColorFunc();
            }
        }

        public SpriteNode CreateScaledSprite(Transform transform, Func<int> levelFunc, Func<Color> colorFunc)
        {
            SpriteNode newSprite = new SpriteNode(RootNode, m_content.FilledCircle);
            newSprite.Color = Color.Blue;
            newSprite.Origin = new Vector2(0.5f);

            m_levelScaledSprites.Add(new LevelScaledSprite(newSprite, transform, levelFunc, colorFunc));

            return newSprite;
        }

        public void RemoveScaledSprite(SpriteNode sprite)
        {
            for (int i = 0; i < m_levelScaledSprites.Count; i++) {
                if (m_levelScaledSprites[i].Sprite == sprite) {
                    m_levelScaledSprites.RemoveAt(i);
                    sprite.Detach();
                    return;
                }
            }

            Debug.Assert(false); // should never hit this
        }

        internal SpriteNode MikeHandNode { get { return m_mikeHand; } }
        internal SpriteNode WiiHandNode { get { return m_wiiHand; } }

        internal int HandDiameter { get { return WiiHandNode.Texture.Width; } }

        internal void SetMikeSignalNode(ASceneNode handNode)
        {
            Debug.Assert(handNode == MikeHandNode || handNode == WiiHandNode);
            m_mikeSignalNode = handNode;
        }
    }
}
