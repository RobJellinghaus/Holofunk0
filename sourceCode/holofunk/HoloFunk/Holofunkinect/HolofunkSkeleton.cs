////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Kinect;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk.Kinect
{
    /// <summary>Holds skeleton data.</summary>
    /// <remarks>Supports copying data from a Kinect SkeletonFrame.  This allows us to double-buffer or
    /// otherwise recycle Skeletons to avoid per-frame allocation.</remarks>
    public class HolofunkSkeleton
    {
        // WARNING: FRAGILE if more joints are added!  Seems to be no JointType.Max....
        const int JointCount = (int)JointType.FootRight;

        // Was the corresponding Skeleton actually being tracked?
        // If this is not Tracked, then the joint data may be stale or
        // altogether missing.
        SkeletonTrackingState m_trackingState;

        // What is the tracking ID of this skeleton?
        int m_trackingId;

        // Array of joint positions, normalized to [0, 1]
        Vector2Averager[] m_joints = new Vector2Averager[JointCount];

        public HolofunkSkeleton() 
        {
            for (int i = 0; i < JointCount; i++) {
                // five data points is enough to get some smoothing without toooo much lag
                m_joints[i] = new Vector2Averager(5);
            }
        }

        /// <summary>Create a skeleton with these joints initialized to these locations.</summary>
        public HolofunkSkeleton(Vector2 headPoint, Vector2 leftHandPoint, Vector2 rightHandPoint)
            : this()
        {
            m_joints[(int)JointType.Head].Update(headPoint);
            m_joints[(int)JointType.HandLeft].Update(leftHandPoint);
            m_joints[(int)JointType.HandRight].Update(rightHandPoint);
        }

        internal void Update(HolofunKinect kinect, Skeleton skeleton)
        {
            m_trackingState = skeleton.TrackingState;
            m_trackingId = skeleton.TrackingId;

            if (skeleton.TrackingState == SkeletonTrackingState.Tracked) {
                for (int i = 0; i < JointCount; i++) {
                    JointType id = (JointType)i;
                    m_joints[i].Update(kinect.GetDisplayPosition(skeleton.Joints[id].Position));
                }
            }
        }

        public SkeletonTrackingState TrackingState { get { return m_trackingState; } }
        public int TrackingId { get { return m_trackingId; } }

        public Vector2 GetPosition(JointType id)
        {
            if (m_joints[(int)id].IsEmpty) {
                return Vector2.Zero;
            }
            else {
                Vector2 averagePos = m_joints[(int)id].Average;
                // It appears that the K4W SDK actually delivers joint positions in depth coordinate space,
                // whereas the beta SDK evidently delivered them as float values in the range [0, 1].
                // So, leave out the viewport scaling that we evidently no longer need.
                return averagePos;
            }
        }

        JointType Hand(bool rightHanded)
        {
            return rightHanded ? JointType.HandRight : JointType.HandLeft;
        }

        /// <summary>Is the microphone "close" to the mouth?</summary>
        /// <remarks>Returns None if there is no skeleton.</remarks>
        public bool IsMikeCloseToMouth(int distance, bool rightHanded)
        {
            Vector2 hand = GetPosition(Hand(rightHanded));
            Vector2 head = GetPosition(JointType.Head);

            // if distance is less than, oh, say, HandDiameter, then yes
            float distSquared;
            Vector2.DistanceSquared(ref hand, ref head, out distSquared);

            return distSquared < (distance * distance);
        }
    }
}

