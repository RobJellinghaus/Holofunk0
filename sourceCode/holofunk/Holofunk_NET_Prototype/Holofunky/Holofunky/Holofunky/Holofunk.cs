////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
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
    using HolofunkMachine = StateMachineInstance<LoopieEvent, HolofunkState>;

    /// <summary>
    /// The Holofunk, incarnate.
    /// </summary>
    /// <remarks>
    /// Implements all the main game logic, coordinates all major components, and basically
    /// gets the job done.
    /// </remarks>
    public class Holofunk : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager m_graphics;
        SpriteBatch m_spriteBatch;
        WiimoteLib m_wiimoteLib;
        HolofunkSceneGraph m_sceneGraph;
        HolofunKinect m_kinect;
        HolofunkContent m_holofunkContent;
        HolofunkBass m_holofunkBass;
        HolofunkMachine m_holofunkMachine;
        Clock m_clock;

        // increments with each new loopie, never repeats
        int m_loopieID;

        // one beat per second at first, nice and slow
        const int BPM = 60;

        public Holofunk()
        {
            m_graphics = new GraphicsDeviceManager(this);

            // 16x10 aspect ratio
            m_graphics.PreferredBackBufferWidth = 640;
            m_graphics.PreferredBackBufferHeight = 400;

            m_clock = new Clock(BPM, HolofunkBass.InputChannelCount);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();

            new Test(GraphicsDevice).RunAllTests();

            m_kinect = new HolofunKinect(GraphicsDevice, ViewportSize);

            m_wiimoteLib = new WiimoteLib();

            m_sceneGraph = new HolofunkSceneGraph(
                GraphicsDevice,
                ViewportSize,
                m_kinect.DepthTexture,
                m_holofunkContent);

            m_holofunkBass = new HolofunkBass(m_clock);
            m_holofunkBass.StartASIO();

            m_holofunkMachine = new HolofunkMachine(
                LoopieStateMachine.Instance, 
                new HolofunkState(m_sceneGraph, m_holofunkBass, m_kinect));

            // Listen to the Wiimote
            if (m_wiimoteLib != null) {
                WiimoteController c = m_wiimoteLib.Wiimotes[0];
                c.ButtonBChanged += new WiimoteController.ButtonEventHandler(ButtonBChanged);
                c.MinusChanged += new WiimoteController.ButtonEventHandler(MinusChanged);
                c.PlusChanged += new WiimoteController.ButtonEventHandler(PlusChanged);
                c.HomeChanged += new WiimoteController.ButtonEventHandler(HomeChanged);
            }            
        }

        Point ViewportSize { get { return new Point(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height); } }

        /// <summary>
        /// The B button (i.e. trigger) changed state; dispatch the right event to the state machine.
        /// </summary>
        /// <param name="state"></param>
        void ButtonBChanged(bool state)
        {
            DispatchEvent(state ? LoopieEvent.TriggerDown : LoopieEvent.TriggerUp);
        }

        void MinusChanged(bool state)
        {
            DispatchEvent(state ? LoopieEvent.MinusDown : LoopieEvent.MinusUp);
        }

        void PlusChanged(bool state)
        {
            DispatchEvent(state ? LoopieEvent.PlusDown : LoopieEvent.PlusUp);
        }

        void HomeChanged(bool state)
        {
            DispatchEvent(state ? LoopieEvent.HomeDown : LoopieEvent.HomeUp);
        }

        void DispatchEvent(LoopieEvent e)
        {
            m_holofunkMachine.OnNext(e);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content; note it is called from base.Initialize()!
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            m_spriteBatch = new SpriteBatch(GraphicsDevice);

            m_holofunkContent = new HolofunkContent(Content);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            m_holofunkBass.Dispose();
            m_holofunkBass = null;
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            m_sceneGraph.Update(m_kinect, m_holofunkBass, m_clock);

            Track<float> newTrackIfAny = m_holofunkBass.Update();
            // if there is a new track now, then start it playing!
            if (newTrackIfAny != null) {
                newTrackIfAny.StartPlaying();

                List<Loopie> loopies = m_holofunkMachine.ActionState.Loopies;
                loopies.Add(new Loopie(
                    m_loopieID++, 
                    newTrackIfAny, 
                    m_sceneGraph,
                    m_holofunkMachine.ActionState.NewLoopPosition));
            }

            // if we are over something, then effect it
            Loopie over = m_holofunkMachine.ActionState.GrabbedLoopie();
            if (over != null) {
                m_holofunkMachine.ActionState.LoopieEffect(over);
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            m_sceneGraph.Render(GraphicsDevice, m_spriteBatch);

            base.Draw(gameTime);
        }
    }
}
