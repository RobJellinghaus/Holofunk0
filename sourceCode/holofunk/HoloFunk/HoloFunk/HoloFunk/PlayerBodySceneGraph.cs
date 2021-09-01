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
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to the player's body.</summary>
    class PlayerBodySceneGraph : SceneGraph
    {
        /// <summary>The parent player scene graph.</summary>
        readonly PlayerSceneGraph m_parent;

        /// <summary>The microphone sprite; left hand if m_rightHanded.</summary>
        readonly SpriteNode m_mikeHand;

        // The group node which contains the wii hand sprite and the effect labels surrounding it.
        readonly GroupNode m_wiiHandGroup;

        /// <summary>The hollow circle "selection" sprite; right hand if m_rightHanded.</summary>
        readonly SpriteNode m_wiiHand;

        /// <summary>A group for the labels so we can reuse them when dragging the effect.</summary>
        readonly GroupNode m_effectLabelGroup;

        /// <summary>The labels for the current effect preset.</summary>
        readonly TextNode[] m_effectLabels;

        /// <summary>The red circle that tracks the current mike signal, and that sticks to the mike
        /// or the Wii-hand depending on whether the trigger is held down.</summary>
        readonly TrackNode m_mikeSignal;

        /// <summary>The line connecting the mike and the cursor.</summary>
        readonly LineNode m_mikeToHandLine;

        /// <summary>The rectangle around the hand's region.</summary>
        readonly RectangleNode m_handRegionRectangle;

        /// <summary>A placeholder GroupNode for the microphone signal, to give it a place to be after the
        /// trigger is released (at which point it detaches from the Wii hand), but before recording
        /// actually stops (at which point it goes back to the mike hand).</summary>
        readonly GroupNode m_newTrackLocation;

        /// <summary>Will point to one of m_leftHand or m_rightHand; represents the hand where the mike signal
        /// should be drawn.</summary>
        ASceneNode m_mikeSignalNode;

        /// <summary>
        /// Will point to either the wiiHandGroup or the current base position for effect dragging.
        /// </summary>
        ASceneNode m_effectLabelNode;

        /// <summary>Head node, for debugging mike/head proximity.</summary>
        readonly SpriteNode m_headNode;

        /// <summary>Is there a moment in the recent past when we showed the effect labels?</summary>
        /// <remarks>If so, we are progressively fading them out and we want to update their color.</remarks>
        Option<Moment> m_effectLabelShownMoment;

        internal PlayerBodySceneGraph(PlayerSceneGraph parent)
            : base()
        {
            m_parent = parent;

            RootNode = new GroupNode(parent.RootNode, Transform.Identity, "Body");

            m_mikeSignal = new TrackNode(
                RootNode,
                new Transform(Vector2.Zero, new Vector2(MagicNumbers.LoopieScale)),
                "MikeSignal",
                parent.Content,
                -1,
                () => parent.Audio.LevelRatio(parent.Channel),
                () => Color.Red,
                () => {
                    int currentRecordingBeatCount = parent.Audio.GetCurrentRecordingBeatCount(parent.Channel);
                    if (currentRecordingBeatCount > 0) {
                        return parent.Clock.Time(parent.Clock.TimepointsPerBeat * currentRecordingBeatCount);
                    }
                    else {
                        return parent.Clock.Time(0);
                    }
                },
                () => m_parent.Audio.GetCurrentRecordingStartBeat(parent.Channel));

            m_mikeHand = new SpriteNode(RootNode, "Mike", parent.Content.Microphone);
            m_mikeHand.Origin = new Vector2(0.5f);
            m_mikeHand.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_wiiHandGroup = new GroupNode(RootNode, Transform.Identity, "Wii group");

            m_wiiHand = new SpriteNode(m_wiiHandGroup, "Wii", parent.Content.HollowCircle);
            m_wiiHand.Origin = new Vector2(0.5f);
            m_wiiHand.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_effectLabelGroup = new GroupNode(RootNode, Transform.Identity, "Effect label group");

            m_effectLabels = MakeEffectLabels(parent, m_effectLabelGroup);

            m_effectLabelNode = m_wiiHandGroup;
            
            m_headNode = new SpriteNode(
                RootNode,
                "Head",
                parent.PlayerIndex == 0 ? parent.Content.HollowOneOval : parent.Content.HollowTwoOval);

            m_headNode.Origin = new Vector2(0.5f, 0f);
            // semi-transparent heads, hopefully this will make them seem "less interactive"
            m_headNode.Color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            m_headNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored | SecondaryViewOption.SecondTexture);
            m_headNode.SecondaryTexture = parent.Content.HollowFace0;

            m_mikeToHandLine = new LineNode(RootNode, "Mike-to-hand line");
            m_mikeToHandLine.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_handRegionRectangle = new RectangleNode(RootNode, "Hand region");
            m_handRegionRectangle.SetSecondaryViewOption(SecondaryViewOption.Hidden);

            m_mikeSignalNode = m_mikeHand;

            m_newTrackLocation = new GroupNode(RootNode, Transform.Identity, "New track location");
            m_newTrackLocation.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);
        }

        TextNode[] MakeEffectLabels(PlayerSceneGraph scene, AParentSceneNode group)
        {
            TextNode[] ret = new TextNode[4];
            ret[0] = MakeEffectLabel(group, HandDiameter / 2, 0, 0, false);
            ret[1] = MakeEffectLabel(group, 0, -HandDiameter / 2, Math.PI * 3 / 2, false);
            ret[2] = MakeEffectLabel(group, -HandDiameter / 2, 0, 0, true);
            ret[3] = MakeEffectLabel(group, 0, HandDiameter / 2, Math.PI / 2, false);
            return ret;
        }

        TextNode MakeEffectLabel(AParentSceneNode group, float x, float y, double rotation, bool rightJustified)
        {
            TextNode ret = new TextNode(group, "");
            ret.LocalTransform = new Transform(new Vector2(x, y), new Vector2(MagicNumbers.LoopieScale));
            ret.Rotation = (float)rotation;
            if (rightJustified) {
                ret.Alignment = Alignment.TopRight;
            }
            return ret;
        }

        internal void ShowEffectLabels(EffectSettings settings, Moment now)
        {
            m_effectLabels[0].Text.Clear();
            m_effectLabels[0].Text.Append(settings.RightLabel);
            m_effectLabels[1].Text.Clear();
            m_effectLabels[1].Text.Append(settings.UpLabel);
            m_effectLabels[2].Text.Clear();
            m_effectLabels[2].Text.Append(settings.LeftLabel);
            m_effectLabels[3].Text.Clear();
            m_effectLabels[3].Text.Append(settings.DownLabel);

            m_effectLabelShownMoment = now;
        }

        internal void HideEffectLabels()
        {
            m_effectLabelShownMoment = Option<Moment>.None;
        }

        /// <summary>Update the scene's background based on the current beat, and the positions of
        /// the two hands based on the latest data polled from Kinect.</summary>
        /// <remarks>We pass in the current value of "now" to ensure consistent
        /// timing between the PlayerState update and the scene graph udpate.</remarks>
        internal void Update(
            PlayerModel playerModel,
            HolofunKinect kinect,
            Moment now)
        {
            // The position adjustment here is purely ad hoc -- the depth image still
            // doesn't line up well with the skeleton-to-depth-mapped hand positions.
            m_mikeHand.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(
                    playerModel.PlayerIndex,
                    playerModel.RightHanded ? JointType.HandLeft : JointType.HandRight) + MagicNumbers.ScreenHandAdjustment,
                new Vector2(MagicNumbers.LoopieScale));

            m_mikeHand.Texture = playerModel.MicrophoneSelected ? m_parent.Content.MicrophoneHighlighted : m_parent.Content.Microphone;

            m_wiiHandGroup.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(
                    playerModel.PlayerIndex,
                    playerModel.RightHanded ? JointType.HandRight : JointType.HandLeft) + MagicNumbers.ScreenHandAdjustment,
                new Vector2(MagicNumbers.LoopieScale));

            m_effectLabelGroup.LocalTransform = new Transform(m_effectLabelNode.LocalTransform.Translation);

            m_headNode.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(playerModel.PlayerIndex, JointType.Head) + MagicNumbers.ScreenHandAdjustment,
                new Vector2(1f));

            // make the mike signal show up at the appropriate node (e.g. m_mikeSignalNode)
            m_mikeSignal.LocalTransform = m_mikeSignalNode.LocalTransform;

            m_mikeToHandLine.SetEndpoints(m_mikeHand.LocalTransform.Translation, m_wiiHandGroup.LocalTransform.Translation);
            // line is invisible unless mike is non-white
            m_mikeToHandLine.Color = m_mikeHand.Color == playerModel.PlayerColor ? new Color(0) : playerModel.PlayerColor;

            // and make the mike signal update appropriately
            m_mikeSignal.Update(now, false, playerModel.PlayerColor);

            { // TODO: should really be PlayerLoopieSceneGraph?  or PlayerPaletteSceneGraph?  but for now leave in Body
                
                if (playerModel.LoopieModel.HandRegion == default(Rectangle)) {
                    m_handRegionRectangle.Color = new Color(0);
                }
                else {
                    Rectangle handRegion = playerModel.LoopieModel.HandRegion;
                    m_handRegionRectangle.Color = playerModel.PlayerColor;
                    m_handRegionRectangle.SetCorners(
                        new Vector2(handRegion.Left, handRegion.Top),
                        new Vector2(handRegion.Right, handRegion.Bottom));
                }
            }

            float averageMikeLevel = m_parent.AverageLevelRatio.Average;
            if (averageMikeLevel < 0.4f) {
                m_headNode.SecondaryTexture = m_parent.Content.HollowFace0;
            }
            else if (averageMikeLevel < 0.8f) {
                m_headNode.SecondaryTexture = m_parent.Content.HollowFace1;
            }
            else {
                m_headNode.SecondaryTexture = m_parent.Content.HollowFace2;
            }

            if (m_effectLabelShownMoment.HasValue) {
                long timepointsElapsed = now.TimepointCount - m_effectLabelShownMoment.Value.TimepointCount;
                if (timepointsElapsed > MagicNumbers.InitialEffectLabelTimepoints) {
                    m_effectLabelShownMoment = Option<Moment>.None;
                }
                else {
                    float fraction = 1f - ((float)timepointsElapsed / MagicNumbers.InitialEffectLabelTimepoints);
                    Color color = Alpha(fraction);
                    m_effectLabels[0].Color = color;
                    m_effectLabels[1].Color = color;
                    m_effectLabels[2].Color = color;
                    m_effectLabels[3].Color = color;
                }
            }
        }

        Color Alpha(float fraction)
        {
            Debug.Assert(fraction >= 0);
            Debug.Assert(fraction <= 1);

            byte b = (byte)(255 * fraction);
            return new Color(b, b, b, b);
        }

        // internal SpriteNode MikeHandNode { get { return m_mikeHand; } }

        // internal SpriteNode WiiHandNode { get { return m_wiiHand; } }

        /// <summary>Get the Wii hand's group node, as an ASceneNode (Transform access only, basically).</summary>
        internal ASceneNode WiiHandGroup { get { return m_wiiHandGroup; } }

        internal Texture2D WiiHandTexture { get { return m_wiiHand.Texture; } set { m_wiiHand.Texture = value; } }

        internal Color WiiHandColor { get { return m_wiiHand.Color; } set { m_wiiHand.Color = value; } }

        /// <summary>We don't really have a group node for the mike hand, so we just return the mike hand itself as its base class.</summary>
        internal ASceneNode MikeHandGroup { get { return m_mikeHand; } }

        internal void SetEffectLabelNode(ASceneNode node)
        {
            m_effectLabelNode = node;
        }

        internal Color MikeHandColor { get { return m_mikeHand.Color; } set { m_mikeHand.Color = value; } }

        internal GroupNode NewTrackLocation { get { return m_newTrackLocation; } }

        // The hand diameter is scaled based on the underlying texture size.
        internal int HandDiameter { get { return (int)(WiiHandTexture.Width * MagicNumbers.LoopieScale); } }

        internal void SetMikeSignalNode(ASceneNode node)
        {
            m_mikeSignalNode = node;
        }
    }
}
