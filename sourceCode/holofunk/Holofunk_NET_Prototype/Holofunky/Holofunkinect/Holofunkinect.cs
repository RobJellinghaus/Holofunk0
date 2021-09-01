////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

// This file contains code based on the Kinect SDK SkeletonViewer sample,
// which is licensed under the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU

using Holofunk.Core;
using Microsoft.Research.Kinect.Nui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Kinect
{
    /// <summary>
    /// Provides access to Kinect data streams and manages connection to Kinect device.
    /// </summary>
    public class HolofunKinect
    {
        Runtime m_nui;
        int m_totalFrames = 0;
        int m_lastFrames = 0;
        DateTime m_lastTime = DateTime.MaxValue;

        Texture2D m_depthTexture;
        Color[] m_depthTextureData;

        Skeleton m_skeleton1;

        Point m_viewportSize;

        const int DEPTH_WIDTH = 320;
        const int DEPTH_HEIGHT = 240;
        const int VIDEO_WIDTH = 640;
        const int VIDEO_HEIGHT = 480;

        public HolofunKinect(GraphicsDevice graphicsDevice, Point viewportSize)
        {
            m_nui = new Runtime();
            m_viewportSize = viewportSize;
            m_skeleton1 = new Skeleton();

            try
            {
                m_nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking /* | RuntimeOptions.UseColor */);
            }
            catch (InvalidOperationException)
            {
                Debug.Assert(false);
                return;
            }


            try
            {
                //m_nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                m_nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                Debug.Assert(false);
                return;
            }

            m_lastTime = DateTime.Now;

            m_nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            m_nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
            // m_nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);

            m_depthTextureData = new Color[DEPTH_WIDTH * DEPTH_HEIGHT];
            m_depthTexture = new Texture2D(graphicsDevice, DEPTH_WIDTH, DEPTH_HEIGHT, false, SurfaceFormat.Color);

            m_nui.NuiCamera.ElevationAngle = 5;
        }

        /// <summary>
        /// The depth camera's data.
        /// </summary>
        public Texture2D DepthTexture
        {
            get { return m_depthTexture; }
        }

        /// <summary>
        /// The first player's skeleton.
        /// </summary>
        internal Skeleton Skeleton1
        {
            get { return m_skeleton1; }
        }

        /// <summary>
        /// Get the position of the given joint in viewport coordinates.
        /// </summary>
        public Transform GetJointViewportPosition(JointID joint)
        {
            return Skeleton1.GetTransform(joint, m_viewportSize);
        }

        /// <summary>
        /// Which joint is holding the Wii?
        /// </summary>
        /// <remarks>
        /// This lets us get smarter about detecting/changing this later.
        /// </remarks>
        public JointID WiiHand
        {
            get { return JointID.HandRight; }
        }

        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        static void ConvertDepthFrame(byte[] inputDepthFrame16, Color[] outputColorFrame)
        {
            Debug.Assert(inputDepthFrame16.Length >> 1 == outputColorFrame.Length);
            for (int i = 0; i < inputDepthFrame16.Length >> 1; i++)
            {
                int i16 = i << 1;

                // see http://social.msdn.microsoft.com/Forums/en-US/kinectsdknuiapi/thread/4da8c75e-9aad-4dc3-bd83-d77ab4cd2f82
                // for details on depth stream bit format

                // three least significant bits are player index
                int player = inputDepthFrame16[i16] & 0x07;

                // twelve bits above that are the depth
                int realDepth = (inputDepthFrame16[i16+1] << 5) | (inputDepthFrame16[i16] >> 3);

                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)((255 - (255 * realDepth / 0x0fff)) >> 2);

                outputColorFrame[i] = Color.Black;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        outputColorFrame[i] = new Color();
                        break;
                    default:
                        outputColorFrame[i] = new Color(intensity, intensity, intensity, intensity);
                        break;
                        /*
                    case 1:
                        outputColorFrame[i] = new Color(intensity, 0, 0, 128);
                        break;
                    case 2:
                        outputColorFrame[i] = new Color(0, intensity, 0, 128);
                        break;
                    case 3:
                        outputColorFrame[i] = new Color((byte)(intensity / 4), (byte)(intensity), (byte)(intensity), 128);
                        break;
                    case 4:
                        outputColorFrame[i] = new Color((byte)(intensity), (byte)(intensity), (byte)(intensity / 4), 128);
                        break;
                    case 5:
                        outputColorFrame[i] = new Color(intensity, (byte)(intensity / 4), (byte)(intensity), 128);
                        break;
                    case 6:
                        outputColorFrame[i] = new Color((byte)(intensity / 2), (byte)(intensity / 2), (byte)(intensity), 128);
                        break;
                    case 7:
                        outputColorFrame[i] = new Color((byte)(255 - intensity), (byte)(255 - intensity), (byte)(255 - intensity), 128);
                        break;
                         */
                }
            }
        }

        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage image = e.ImageFrame.Image;

            ConvertDepthFrame(image.Bits, m_depthTextureData);
            m_depthTexture.SetData(m_depthTextureData);

            ++m_totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(m_lastTime) > TimeSpan.FromSeconds(1))
            {
                int frameDiff = m_totalFrames - m_lastFrames;
                m_lastFrames = m_totalFrames;
                m_lastTime = cur;
                // Title = frameDiff.ToString() + " fps";
            }
        }

        // Get the coordinates in color-pixel space, normalized to [0, 1].
        internal Vector2 GetDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            m_nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * DEPTH_WIDTH, DEPTH_WIDTH));  //convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * DEPTH_HEIGHT, DEPTH_HEIGHT));  //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            m_nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            return new Vector2(colorX / (float)VIDEO_WIDTH, colorY / (float)VIDEO_HEIGHT);
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;

            foreach (SkeletonData data in skeletonFrame.Skeletons) {
                if (SkeletonTrackingState.Tracked == data.TrackingState) {
                    m_skeleton1.Update(this, data);
                    break; // only take first tracked skeleton
                }
            }
        }

        /*
        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage image = e.ImageFrame.Image;

            video.Source = CreateBitmapSource(image);

            image.CopyOnto(ref lastVideoFrame);
        }
         */

        /*
        private ImageSource CreateBitmapSource(PlanarImage Image)
        {
            ImageSource image = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
            return image;
        }
         */

        public void Close()
        {
            m_nui.Uninitialize();
            m_nui = null;
        }
    }
}