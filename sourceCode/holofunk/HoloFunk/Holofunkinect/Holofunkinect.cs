////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

// This file contains code based on the Kinect SDK SkeletonViewer sample,
// which is licensed under the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU

using Holofunk.Core;
using Microsoft.Kinect;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Holofunk.Kinect
{
    /// <summary>The current algorithm used for tracking the player(s).</summary>
    public enum TrackingMode
    {
        /// <summary>Only one player tracked; other player(s) ignored.</summary>
        OnePlayer,

        /// <summary>One player tracked; second player simulated as statically positioned on right.</summary>
        /// <remarks>This is useful for debugging when only one actual player is available.</remarks>
        OnePlayerOneDummy,

        /// <summary>Two players tracked; full stickiness engaged for both.</summary>
        TwoPlayers
    }

    /// <summary>Provides access to Kinect data streams and manages connection to Kinect device.</summary>
    /// <remarks>This class tracks two distinct players, Player0 and Player1.  The default tracking
    /// algorithm is "sticky" -- the first recognized player becomes Player0, and the second becomes
    /// Player1.  If two are recognized simultaneously, the leftmost is Player0.  If one player goes
    /// out of view, that player slot becomes available, and the next recognized player gets it.
    /// But as long as a player is in view, they will be persistently tracked.
    /// 
    /// This deliberately does not support multiple Kinect sensors.</remarks>
    public class HolofunKinect : IDisposable
    {
        // color divisors for tinting depth pixels
        static readonly int[] IntensityShiftByPlayerR = { 1, 2, 0, 2, 0, 0, 2, 0 };
        static readonly int[] IntensityShiftByPlayerG = { 1, 2, 2, 0, 2, 0, 0, 1 };
        static readonly int[] IntensityShiftByPlayerB = { 1, 0, 2, 2, 0, 2, 0, 2 };

        const int AlphaIndex = 3; 
        const int RedIndex = 2;
        const int GreenIndex = 1;
        const int BlueIndex = 0;

        // thank you, http://mark-dot-net.blogspot.com/2008/06/wavebuffer-casting-byte-arrays-to-float.html
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct ColorConversion
        {
            [FieldOffset(0)]
            public byte[] Bytes;
            [FieldOffset(0)]
            public short[] Shorts;
            [FieldOffset(0)]
            public float[] Floats;
            [FieldOffset(0)]
            public Color[] Colors;
        }

        KinectSensor m_kinect;
        int m_totalFrames = 0;
        int m_lastFrames = 0;
        DateTime m_lastTime = DateTime.MaxValue;

        /// <summary>The current tracking mode; this can be reset between frames.</summary>
        TrackingMode m_trackingMode;

        /// <summary>A colorized rendition of the player/depth information.</summary>
        Texture2D m_playerTexture;

        /// <summary>The current player 0 skeleton, if any.</summary>
        HolofunkSkeleton m_skeleton0;

        /// <summary>The current player 1 skeleton, if any.</summary>
        HolofunkSkeleton m_skeleton1;

        /// <summary>The backing buffer for m_playerTexture.</summary>
        byte[] m_playerTextureData;

        /// <summary>A buffer containing the raw depth data.</summary>
        short[] m_depthData;

        /// <summary>The latest skeletal data received.</summary>
        Skeleton[] m_skeletons;

        /// <summary>The HolofunkSkeletons tracking the raw skeletal data.</summary>
        HolofunkSkeleton[] m_holofunkSkeletons;

        /// <summary>How big is our viewport?  Used when calculating skeleton -> viewport translation.</summary>
        Vector2 m_viewportSize;

        /// <summary>The graphics device from which we can create a player texture.</summary>
        GraphicsDevice m_graphicsDevice;

        /// <summary>A reusable, temporary list of HolofunkSkeletons (kept as a field to avoid churning the GC).</summary>
        List<HolofunkSkeleton> m_tempSkeletonList = new List<HolofunkSkeleton>();

        /// <summary>Action to invoke when Kinect gets a frame.</summary>
        /// <remarks>Typically causes invalidation and update.</remarks>
        // readonly Action m_kinectFrameAction; 

        /// <summary>Dummy skeleton if we're in OnePlayerOneDummy mode.</summary>
        readonly HolofunkSkeleton m_dummySkeleton;

        const int DEPTH_WIDTH = 320;
        const int DEPTH_HEIGHT = 240;
        public const int VIDEO_WIDTH = 640;
        public const int VIDEO_HEIGHT = 480;

        public HolofunKinect(GraphicsDevice graphicsDevice, Vector2 viewportSize, int elevationAngle)
        {
            m_trackingMode = TrackingMode.TwoPlayers;

            m_graphicsDevice = graphicsDevice;
            m_viewportSize = viewportSize;
            m_playerTexture = Texture2D.New(m_graphicsDevice, DEPTH_WIDTH, DEPTH_HEIGHT, new MipMapCount(1), PixelFormat.R8G8B8A8.UNorm);
            // m_kinectFrameAction = kinectFrameAction;

            // Simulated skeleton centered in the right half of the viewport.
            m_dummySkeleton = new HolofunkSkeleton(
                new Vector2(0.75f * viewportSize.X, 0.3f * viewportSize.Y),
                new Vector2(0.65f * viewportSize.X, 0.6f * viewportSize.Y),
                new Vector2(0.85f * viewportSize.X, 0.6f * viewportSize.Y));
            
            KinectStart(elevationAngle);
        }

        #region Multi-Kinect discovery + setup

        void KinectStart(int elevationAngle)
        {
            // listen to any status change for Kinects, so we can handle the "none" case
            KinectSensor.KinectSensors.StatusChanged += this.KinectsStatusChanged;

            InitializeKinect(FindFirstSensor(), elevationAngle);
        }

        void InitializeKinect(KinectSensor kinect, int elevationAngle)
        {
            HoloDebug.Assert(kinect != null);

            m_kinect = kinect;

            try {
                m_kinect.SkeletonStream.Enable();
                // m_kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                m_kinect.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);

                m_kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(Kinect_AllFramesReady);
            }
            catch (InvalidOperationException) {
                HoloDebug.Assert(false);
                return;
            }


            m_lastTime = DateTime.Now;

            m_kinect.Start();

            m_kinect.ElevationAngle = elevationAngle;
        }

        void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame()) {
                if (depthFrame != null) {
                    DepthFrameReady(depthFrame);
                }
            }

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame()) {
                if (skeletonFrame != null) {
                    SkeletonFrameReady(skeletonFrame);
                }
            }

            // m_kinectFrameAction();
        }

        // Find the first sensor that is not disconnectingSensor.
        KinectSensor FindFirstSensor(KinectSensor disconnectingSensor = null)
        {
            foreach (KinectSensor sensor in KinectSensor.KinectSensors) {
                // skip the one that's disconnecting (not sure when it leaves the KinectSensors collection
                // relative to the KinectsSStatusChanged event)
                if (sensor == disconnectingSensor) {
                    continue;
                }

                // return the first one we hit otherwise
                return sensor;
            }

            // if none, then null
            return null;
        }

        public void Dispose()
        {
            if (m_kinect != null) {
                m_kinect.Stop();
                m_kinect.Dispose();
                m_kinect = null;
            }
        }

        void KinectsStatusChanged(object sender, StatusChangedEventArgs e)
        {
            switch (e.Status) {
                case KinectStatus.Disconnected:
                case KinectStatus.NotPowered:
                    if (m_kinect == e.Sensor) {
                        // is there any other sensor?
                        InitializeKinect(FindFirstSensor(disconnectingSensor: m_kinect), 0);
                    }
                    break;
                default:
                    break;
            }
        }

        #endregion Multi-Kinect discovery + setup
        
        /// <summary>The colorized, depth-based player texture.</summary>
        public Texture2D PlayerTexture
        {
            get { return m_playerTexture; }
        }

        /// <summary>very useful to know</summary>
        public Vector2 ViewportSize
        {
            get { return m_viewportSize; }
        }

        /// <summary>The currently locked skeleton for player 0, if any (null if none).</summary>
        internal HolofunkSkeleton Skeleton0 { get { return m_skeleton0; } }

        /// <summary>The currently locked skeleton for player 1, if any (null if none).</summary>
        internal HolofunkSkeleton Skeleton1 { get { return m_skeleton1; } }

        public void SwapSkeletons()
        {
            lock (this) {
                HolofunkSkeleton temp = m_skeleton0;
                m_skeleton0 = m_skeleton1;
                m_skeleton1 = temp;
            }
        }

        /// <summary>Update the current skeleton(s).</summary>
        void Update()
        {
            lock (this) {
                bool skeleton0Found = false;
                // if we are not tracking two players, then consider skeleton 1 as "pre-found"
                bool skeleton1Found = m_trackingMode == TrackingMode.OnePlayer;

                if (m_holofunkSkeletons == null) {
                    return;
                }

                m_tempSkeletonList.Clear();
                for (int i = 0; i < m_holofunkSkeletons.Length; i++) {
                    HolofunkSkeleton skeleton = m_holofunkSkeletons[i];
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked) {
                        if (!skeleton0Found && m_skeleton0 != null && skeleton.TrackingId == m_skeleton0.TrackingId) {
                            m_skeleton0 = skeleton;
                            skeleton0Found = true;
                        }
                        else if (!skeleton1Found && m_skeleton1 != null && skeleton.TrackingId == m_skeleton1.TrackingId) {
                            m_skeleton1 = skeleton;
                            skeleton1Found = true;
                        }
                        else {
                            // add it to the list
                            m_tempSkeletonList.Add(m_holofunkSkeletons[i]);
                        }

                        if (skeleton0Found && skeleton1Found) {
                            break;
                        }
                    }
                }

                // if we didn't find both by now, then let's pick arbitrarily in order of the temp list.
                if (!(skeleton0Found && skeleton1Found) && m_tempSkeletonList.Count > 0) {
                    int i = 0;

                    if (!skeleton0Found) {
                        m_skeleton0 = m_tempSkeletonList[i];
                        skeleton0Found = true;
                        i++;
                    }

                    if (!skeleton1Found && i < m_tempSkeletonList.Count) {
                        m_skeleton1 = m_tempSkeletonList[i];
                        skeleton1Found = true;
                    }
                }

                if (!skeleton0Found) {
                    m_skeleton0 = null;
                }

                if (m_trackingMode == TrackingMode.OnePlayer) {
                    m_skeleton1 = null;
                }
                else if (!skeleton1Found && m_trackingMode == TrackingMode.OnePlayerOneDummy) {
                    m_skeleton1 = m_dummySkeleton;
                }
                else {
                    if (!skeleton1Found) {
                        m_skeleton1 = null;
                    }
                }
            }
        }

        HolofunkSkeleton GetSkeleton(int playerId)
        {
            switch (playerId) {
                case 0: return m_skeleton0;
                case 1: return m_skeleton1;
                default: HoloDebug.Assert(false, "Invalid playerId"); return null;
            }
        }

        /// <summary>Get the position of the given joint in viewport coordinates.</summary>
        /// <remarks>If there is no first skeleton, returns the identity transform.  Should rearrange
        /// all this to support persistent skeleton identification....</remarks>
        public Vector2 GetJointViewportPosition(int playerId, JointType joint)
        {
            HolofunkSkeleton skeleton = GetSkeleton(playerId);
            // if no skeleton, all joints are waaaay offscreen (to the upper left)
            return skeleton == null ? new Vector2(-1000) : skeleton.GetPosition(joint);
        }

        // A depth frame has come in; colorize the data by player, and update PlayerTexture.
        void DepthFrameReady(DepthImageFrame depthFrame)
        {
            // this can happen if we blow our frame, but I want to know right away if that starts to happen
            HoloDebug.Assert(depthFrame != null);

            if (m_playerTextureData == null) {
                m_playerTextureData = new byte[depthFrame.PixelDataLength * 4];
                m_depthData = new short[depthFrame.PixelDataLength];
            }

            depthFrame.CopyPixelDataTo(m_depthData);

            ConvertDepthFrame(m_depthData, m_kinect.DepthStream, m_playerTextureData);

            unsafe {
                fixed (byte* datap = m_playerTextureData) {
                    m_playerTexture.SetData(m_playerTextureData, arraySlice: 0, mipSlice: 0);
                }
            }

            // Due to the bizarre way the ColorConversion hack works, the Color[] array has a
            // length equal to the byte[] array -- the length field is reinterpreted verbatim.
            // Referring to data beyond the true end of the array is an error.  Fortunately,
            // we can safely access the data in the appropriate range of the array (e.g. 1/4
            // the byte[] array length, in the byte[] -> Color[] case).
            // ... except we don't have a SetData that takes a length, so we need all this unsafe rigmarole
            // that should not be necessary due to the COlorConversion hack :-\
            /*
            ColorConversion conversion = new ColorConversion();
            conversion.Bytes = m_playerTextureData;
            Color[] colors = conversion.Colors;
             
            unsafe {
                fixed (Color* colorp = colors) {
                    m_playerTexture.SetData(
                        new DataPointer(colorp, colors.Length * Utilities.SizeOf<Color>() / 4),
                        arraySlice: 0,
                        mipSlice: 0);
                }
            }
             */

            ++m_totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(m_lastTime) > TimeSpan.FromSeconds(1)) {
                int frameDiff = m_totalFrames - m_lastFrames;
                m_lastFrames = m_totalFrames;
                m_lastTime = cur;
                // Title = frameDiff.ToString() + " fps";
            }
        }

        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors.
        // depthFrame32 is overwritten by this method.
        void ConvertDepthFrame(short[] depthFrame, DepthImageStream depthStream, byte[] depthFrame32)
        {
            int tooNearDepth = depthStream.TooNearDepth;
            int tooFarDepth = depthStream.TooFarDepth;
            int unknownDepth = depthStream.UnknownDepth;

            for (int i16 = 0, i32 = 0; i16 < depthFrame.Length && i32 < depthFrame32.Length; i16++, i32 += 4)
            {
                int player = depthFrame[i16] & DepthImageFrame.PlayerIndexBitmask;
                int realDepth = depthFrame[i16] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(~(realDepth >> 4));

                if (player == 0 && realDepth == 0)
                {
                    // black
                    depthFrame32[i32 + RedIndex] = 255;
                    depthFrame32[i32 + GreenIndex] = 255;
                    depthFrame32[i32 + BlueIndex] = 255;
                    depthFrame32[i32 + AlphaIndex] = 255;
                }
                else if (player == 0 && (realDepth == tooFarDepth || realDepth == unknownDepth))
                {
                    // black
                    depthFrame32[i32 + RedIndex] = 0;
                    depthFrame32[i32 + GreenIndex] = 0;
                    depthFrame32[i32 + BlueIndex] = 0;
                    depthFrame32[i32 + AlphaIndex] = 255;
                }
                else
                {
                    // tint the intensity by dividing by per-player values
                    depthFrame32[i32 + RedIndex] = (byte)(intensity >> IntensityShiftByPlayerR[player]);
                    depthFrame32[i32 + GreenIndex] = (byte)(intensity >> IntensityShiftByPlayerG[player]);
                    depthFrame32[i32 + BlueIndex] = (byte)(intensity >> IntensityShiftByPlayerB[player]);
                    depthFrame32[i32 + AlphaIndex] = (byte)intensity;
                }
            }
        }

        // Get the coordinates in color-pixel space.
        internal Vector2 GetDisplayPosition(SkeletonPoint jointPosition)
        {
            DepthImagePoint depthPoint = m_kinect.MapSkeletonPointToDepth(jointPosition, DepthImageFormat.Resolution640x480Fps30);
            return new Vector2(depthPoint.X, depthPoint.Y);
        }

        void SkeletonFrameReady(SkeletonFrame frame)
        {
            if (m_skeletons == null) {
                m_skeletons = new Skeleton[frame.SkeletonArrayLength];
                m_holofunkSkeletons = new HolofunkSkeleton[frame.SkeletonArrayLength];

                for (int i = 0; i < frame.SkeletonArrayLength; i++) {
                    m_holofunkSkeletons[i] = new HolofunkSkeleton();
                }
            }

            frame.CopySkeletonDataTo(m_skeletons);

            for (int i = 0; i < frame.SkeletonArrayLength; i++) {
                m_holofunkSkeletons[i].Update(this, m_skeletons[i]);
            }

            Update();
        }
    }
}