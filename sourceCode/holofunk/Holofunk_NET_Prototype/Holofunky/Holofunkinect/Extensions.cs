////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Research.Kinect.Nui;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk.Kinect
{
    public static class Extensions
    {
        public static PlanarImage Copy(this PlanarImage image)
        {
            byte[] data = new byte[image.Bits.Length];
            return new PlanarImage() { Bits = data, BytesPerPixel = image.BytesPerPixel, Width = image.Width, Height = image.Height };
        }

        /// <summary>
        /// Copy an image to a destination image.
        /// </summary>
        /// <remarks>
        /// Image formats must be identical.
        /// </remarks>
        public static void CopyTo(this PlanarImage image, PlanarImage target)
        {
            Debug.Assert(image.Width == target.Width);
            Debug.Assert(image.Height == target.Height);
            Debug.Assert(image.BytesPerPixel == target.BytesPerPixel);
            Debug.Assert(image.Bits.Length == target.Bits.Length);
            image.Bits.CopyTo(target.Bits, 0);
        }

        public static void CopyOnto(this PlanarImage image, ref PlanarImage target)
        {
            if (!image.IsInitialized()) {
                return;
            }
            if (target.IsInitialized()) {
                image.CopyTo(target);
            }
            else {
                target = image.Copy();
            }
        }

        public static bool IsInitialized(this PlanarImage image)
        {
            return image.Bits != null;
        }
    }

}

