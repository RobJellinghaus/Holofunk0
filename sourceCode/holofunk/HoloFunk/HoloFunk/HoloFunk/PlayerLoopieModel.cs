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
    /// <summary>The model of which loopies the current player is touching or influencing.</summary>
    class PlayerLoopieModel
    {
        #region Fields

        /// <summary>The single closest loopie.</summary>
        // Will be unset if this is not known; will be null if we know there is none in range.
        // The state of this is used to track whether we need to recalculate loopie proximities.
        Option<Loopie> m_closestLoopie;

        /// <summary>Where should the newly placed loop go?</summary>
        // If we released the trigger before the end of the current beat, then we might wiggle around
        // after releasing the trigger but before the beat starts to play.  But you want to "drop" the
        // loopie at the point where you *released* the trigger.  This field allows us to save where
        // exactly that was.
        Transform m_newLoopPosition;

        /// <summary>What effect are we applying to loopies we touch?</summary>
        // Default: nada.
        // This must be idempotent since right now we apply it like mad on every update!
        Action<Loopie> m_loopieTouchEffect = loopie => { };

        /// <summary>Are we directly touching one or more loopies with the cursor hand?</summary>
        // This affects whether we are doing region selection.
        bool m_directlyTouchingLoopies = false;

        /// <summary>Is there a hand region?</summary>
        // This is the empty rectangle if we are DirectlyTouchingLoopies;
        // otherwise, it is the rectangle containing both the hand cursor and one or more loopies.
        Rectangle m_handRegion = default(Rectangle);

        /// <summary>The loopies currently touched by this player.</summary>
        readonly List<Loopie> m_touchedLoopies = new List<Loopie>();

        #endregion

        internal PlayerLoopieModel()
        {
            m_closestLoopie = Option<Loopie>.None;
        }

        #region Properties

        /// <summary>Position of the newly placed loopie.</summary>
        internal Transform NewLoopPosition { get { return m_newLoopPosition; } set { m_newLoopPosition = value; } }

        /// <summary>The effect applied to loopies being touched.</summary>
        internal Action<Loopie> LoopieTouchEffect { get { return m_loopieTouchEffect; } set { m_loopieTouchEffect = value; } }

        /// <summary>The quadrant of the screen currently occupied by the hand cursor.</summary>
        internal Rectangle HandRegion { get { return m_handRegion; } }

        /// <summary>Get the Loopie that is closest to the Wii hand and within grabbing distance.</summary>
        internal Option<Loopie> ClosestLoopie
        {
            get
            {
                return m_closestLoopie;
            }
        }

        internal List<Loopie> TouchedLoopies
        {
            get
            {
                return m_touchedLoopies;
            }
        }

        #endregion

        internal void CalculateTouchedLoopies(List<Loopie> loopies, Vector2 handPosition, float handDiameter, Color playerColor, Vector2 screenSize)
        {
            HoloDebug.Assert(m_touchedLoopies.Count == 0);
            HoloDebug.Assert(!m_closestLoopie.HasValue);
            HoloDebug.Assert(m_directlyTouchingLoopies == false);
            HoloDebug.Assert(m_handRegion == default(Rectangle));
            HoloDebug.Assert(!m_closestLoopie.HasValue);

            // Reset state variables
            m_directlyTouchingLoopies = false;
            m_handRegion = default(Rectangle);
            m_closestLoopie = Option<Loopie>.None;

            if (loopies.Count == 0) {
                return;
            }

            // Transform handPosition = PlayerSceneGraph.WiiHandNode.LocalTransform;

            Loopie closest = null;
            double minDistSquared = double.MaxValue;
            double handSize = handDiameter / 1.5; // hand radius too small, hand diameter too large
            double handSizeSquared = handSize * handSize;

            foreach (Loopie loopie in loopies) {
                Transform loopiePosition = loopie.Position;
                double xDist = loopiePosition.Translation.X - handPosition.X;
                double yDist = loopiePosition.Translation.Y - handPosition.Y;
                double distSquared = xDist * xDist + yDist * yDist;
                if (distSquared < handSizeSquared) {
                    loopie.Touched = true;
                    loopie.TouchedColor = playerColor;
                    m_touchedLoopies.Add(loopie);
                    if (distSquared < minDistSquared) {
                        closest = loopie;
                        minDistSquared = distSquared;
                    }
                }
            }

            if (closest != null) {
                m_closestLoopie = new Option<Loopie>(closest);
            }

            // if there are no touched loopies at all, then we effectively touch all loopies in the quadrant
            if (m_touchedLoopies.Count == 0) {
                float handX = handPosition.X;
                float handY = handPosition.Y;
                if (handX >= 0 && handY >= 0) {
                    // this player exists

                    // where is the wii hand?
                    int w = (int)screenSize.X;
                    int h = (int)screenSize.Y;
                    int w2 = w / 2;
                    int h2 = h / 2;

                    // now we have a hand region
                    Rectangle handRegion = new Rectangle(
                        handPosition.X < w2 ? 0 : w2,
                        handPosition.Y < h2 ? 0 : h2,
                        handPosition.X < w2 ? w2 : w,
                        handPosition.Y < h2 ? h2 : h);

                    foreach (Loopie loopie in loopies) {
                        Transform loopiePosition = loopie.Position;
                        if (handRegion.Contains(
                            (int)loopiePosition.Translation.X,
                            (int)loopiePosition.Translation.Y)) {
                            loopie.Touched = true;
                            loopie.TouchedColor = playerColor;
                            m_touchedLoopies.Add(loopie);
                        }
                    }

                    if (m_touchedLoopies.Count > 0) {
                        // we can tell the world
                        m_handRegion = handRegion;
                    }
                }
            }
            else {
                m_directlyTouchingLoopies = true;
            }
        }

        /// <summary>Recalculate the loopies touched by this player.</summary>
        // Call this once after a frame change, before querying GrabbedLoopie or TouchedLoopies;
        // otherwise their cached values will be reused without checking for new positions of
        // anything.
        internal void InvalidateTouchedLoopies()
        {
            m_closestLoopie = Option<Loopie>.None;
            m_directlyTouchingLoopies = false;
            m_handRegion = default(Rectangle);

            foreach (Loopie loopie in m_touchedLoopies) {
                loopie.Touched = false;
                loopie.TouchedColor = new Color(0);
            }

            m_touchedLoopies.Clear();
        }

        /// <summary>Is the cursor directly touching one or more loopies?</summary>
        // If not, we are quadrant-selecting, which changes how we handle some user gestures
        // (such as muting).
        internal bool DirectlyTouchingLoopies
        {
            get
            {
                HoloDebug.Assert(m_closestLoopie.HasValue);
                return m_directlyTouchingLoopies;
            }
        }
    }
}