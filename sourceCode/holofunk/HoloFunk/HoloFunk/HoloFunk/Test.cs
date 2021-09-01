////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Holofunk.Tests;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Holofunk
{
    class Test : TestSuite
    {
        readonly GraphicsDevice m_device;
        // We have to have a GraphicsDevice to get even a basic Texture2D, and we do not want to mock
        // every damn XNA type in existence.
        internal Test(GraphicsDevice device) { m_device = device; }

        public void TestSceneGraph_02()
        {
            MockSpriteBatch batch = new MockSpriteBatch(Log);

            Texture2D tex = Texture2D.New(m_device, 10, 10, PixelFormat.R8G8B8A8.SInt);

            batch.Draw(tex, new Rectangle(20, 30, 40, 50), Color.AliceBlue);

            Log.CheckOnly("[Draw 10x10 @ (20,30)-(40,50) in A:255 R:240 G:248 B:255]");
        }
    }
}
