////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Research.Kinect.Nui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk.Kinect
{
    /// <summary>
    /// Holds skeleton and joint connectivity data.
    /// </summary>
    /// <remarks>
    /// Supports copying data from a Kinect SkeletonFrame.  This allows us to double-buffer or
    /// otherwise recycle Skeletons to avoid per-frame allocation.
    /// </remarks>
    public class Skeleton
    {
        static JointID[] s_spine = new[] { JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head };
        static JointID[] s_leftArm = new[] { JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.HandLeft };
        static JointID[] s_rightArm = new[] { JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.HandRight };
        static JointID[] s_leftLeg = new[] { JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.FootLeft };
        static JointID[] s_rightLeg = new[] { JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.FootRight };

        static Dictionary<JointID, Color> jointColors = new Dictionary<JointID, Color>() { 
            {JointID.HipCenter, new Color(169, 176, 155)},
            {JointID.Spine, new Color(169, 176, 155)},
            {JointID.ShoulderCenter, new Color(168, 230, 29)},
            {JointID.Head, new Color(200, 0,   0)},
            {JointID.ShoulderLeft, new Color(79,  84,  33)},
            {JointID.ElbowLeft, new Color(84,  33,  42)},
            {JointID.WristLeft, new Color(255, 126, 0)},
            {JointID.HandLeft, new Color(215,  86, 0)},
            {JointID.ShoulderRight, new Color(33,  79,  84)},
            {JointID.ElbowRight, new Color(33,  33,  84)},
            {JointID.WristRight, new Color(77,  109, 243)},
            {JointID.HandRight, new Color(37,   69, 243)},
            {JointID.HipLeft, new Color(77,  109, 243)},
            {JointID.KneeLeft, new Color(69,  33,  84)},
            {JointID.AnkleLeft, new Color(229, 170, 122)},
            {JointID.FootLeft, new Color(255, 126, 0)},
            {JointID.HipRight, new Color(181, 165, 213)},
            {JointID.KneeRight, new Color(71, 222,  76)},
            {JointID.AnkleRight, new Color(245, 228, 156)},
            {JointID.FootRight, new Color(77,  109, 243)}
        };

        // Array of joint positions, normalized to [0, 1]
        Vector2Averager[] m_joints = new Vector2Averager[(int)JointID.Count];

        public Skeleton() 
        {
            for (int i = 0; i < (int)JointID.Count; i++) {
                // five data points is enough to get some smoothing without toooo much lag
                m_joints[i] = new Vector2Averager(5);
            }
        }

        internal void Update(HolofunKinect kinect, SkeletonData data)
        {
            for (int i = 0; i < (int)JointID.Count; i++) {
                JointID id = (JointID)i;
                m_joints[i].Update(kinect.GetDisplayPosition(data.Joints[id]));
            }
        }

        public Transform GetTransform(JointID id, Point viewportScale)
        {
            if (m_joints[(int)id].IsEmpty) {
                return Transform.Identity;
            }
            else {
                Vector2 averagePos = m_joints[(int)id].Average;
                return new Transform(averagePos * new Vector2(viewportScale.X, viewportScale.Y), Vector2.One);
            }
        }
    }
}

