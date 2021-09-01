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
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Holofunk
{
    // sort out our WinForms vs. XNA Name Battle
    using SysColor = System.Drawing.Color;
    using HolofunkMachine = StateMachineInstance<LoopieEvent>;

    /// <summary>
    /// Centralize all compile-time numeric tuning knobs.
    /// </summary>
    static class MagicNumbers
    {
        // Ratio by which to multiply the Holofunkinect.VIDEO_{WIDTH,HEIGHT}
        internal const int ScreenRatio = 2;

        // Kinect angle?
        internal const int KinectAngle = 10;

        // what tempo do we start at?
        internal const float InitialBpm = 80f;//130.612f; for Turnado sync at 120BPM... WHATEVER

        // adjust the position of skeleton sprites by this much in screen space
        internal static Vector2 ScreenHandAdjustment = new Vector2(0, -50);

        // Length of slider nodes in starfish mode.
        internal const int SliderLength = 120;

        // How big is the bounding circle, in multiples of the base circle texture width?
        internal const float EffectSpaceBoundingCircleMultiple = 2.3f;

        // How much smaller is the circle than its texture width?
        internal const float EffectSpaceBoundingCircleSize = 0.8f;

        // How much smaller is the effect knob, in multiples of the base circle texture width?
        internal const float EffectSpaceKnobMultiple = 0.2f;

        /// <summary>How many timepoints back do we go when recording a new track?  (Latency compensation, basically.)</summary>
        /// <remarks>We don't need this, thankfully, with the Wiimote; but we may need it with Kinect 2....</remarks>
        internal const int EarlierDurationInTimepoints = 0;

        /// <summary>Fade effect labels over this amount of time</summary>
        internal const int InitialEffectLabelTimepoints = Clock.TimepointRateHz * 3;

        /// <summary>update status text every 10 frames, to conserve on garbage</summary>
        internal const int StatusTextUpdateInterval = 20;

        /// <summary>scale factor to apply to track nodes and hand cursors</summary>
        internal const float LoopieScale = 0.8f;
    }

    /// <summary>The Holofunk, incarnate.</summary>
    /// <remarks>Implements all the main game logic, coordinates all major components, and basically
    /// gets the job done.</remarks>
    public class Holofunk : Game
    {
        readonly Clock m_clock;

        readonly GraphicsDeviceManager m_graphicsDeviceManager;

        struct EventEntry
        {
            public readonly LoopieEvent Event;
            public readonly HolofunkMachine Machine;
            public EventEntry(LoopieEvent evt, HolofunkMachine machine)
            {
                HoloDebug.Assert(machine != null);
                Event = evt;
                Machine = machine;
            }
            public bool IsInitialized { get { return Machine != null; } }
        }

        readonly Queue<EventEntry> m_eventQueue;

        WiimoteLib m_wiimoteLib;
        HolofunKinect m_kinect;
        HolofunkBass m_holofunkBass;
        HolofunkModel m_model;

        ISpriteBatch m_spriteBatch;

        // two HolofunkMachines, one per player
        HolofunkMachine m_holofunkMachine0, m_holofunkMachine1;

        // increments with each new loopie, never repeats
        int m_loopieID;

        // WHAT IS OUR TEMPO, COMMANDER
        const float InitialBpm = MagicNumbers.InitialBpm;

        // 4/4 time (actually, 4/_ time, we don't care about the note duration)
        const int BeatsPerMeasure = 4;

        // time at previous tick
        long m_timeAtPreviousTick;

        // how large is our viewport
        Vector2 m_viewportSize;

        public Holofunk() 
        {
            m_clock = new Clock(InitialBpm, BeatsPerMeasure, HolofunkBassAsio.InputChannelCount);

            // Creates a graphics manager. This is mandatory.

            m_viewportSize = new Vector2(HolofunKinect.VIDEO_WIDTH, HolofunKinect.VIDEO_HEIGHT);

            m_graphicsDeviceManager = new GraphicsDeviceManager(this);
            m_graphicsDeviceManager.PreferredBackBufferWidth = (int)m_viewportSize.X * MagicNumbers.ScreenRatio;
            m_graphicsDeviceManager.PreferredBackBufferHeight = (int)m_viewportSize.Y * MagicNumbers.ScreenRatio;
            m_graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "TextureContent";

            m_eventQueue = new Queue<EventEntry>();
        }

        internal Moment Now { get { return m_clock.Now; } }

        internal Vector2 ViewportSize { get { return m_viewportSize; } }

        internal HolofunkView SecondaryView { get { return m_model.SecondaryView; } }

        internal void EnqueueEvent(LoopieEvent evt, HolofunkMachine machine)
        {
            lock (m_eventQueue) {
                m_eventQueue.Enqueue(new EventEntry(evt, machine));
            }
        }

        /// <summary>Allows the game to perform any initialization it needs to before starting to run.</summary>
        /// <remarks>This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.</remarks>
        protected override void Initialize()
        {
            m_wiimoteLib = new WiimoteLib();

            // HORRIBLE HACK: just ensure the statics are initialized
            string s = PlayerEffectSpaceModel.EffectSettings[0].LeftLabel;

            m_holofunkBass = new HolofunkBass(m_clock, MagicNumbers.EarlierDurationInTimepoints);
            m_holofunkBass.StartASIO();

            m_kinect = new HolofunKinect(GraphicsDevice, m_viewportSize, MagicNumbers.KinectAngle);

            base.Initialize();

            List<IGameSystem> list = GameSystems.ToList();
            HolofunkRenderer renderer = (HolofunkRenderer)list[0];

            m_holofunkBass.SetBaseForm((Form)renderer.Window.NativeWindow);
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            new Test(GraphicsDevice).RunAllTests();

            m_spriteBatch = new SpriteBatchWrapper(new SpriteBatch(GraphicsDevice), ViewportSize, MagicNumbers.ScreenRatio);

            var holofunkContent = new HolofunkTextureContent(Content);

            m_model = new HolofunkModel(
                GraphicsDevice,
                m_clock,
                m_holofunkBass,
                m_kinect,
                holofunkContent,
                m_viewportSize,
                m_clock.BPM);                

            m_holofunkMachine0 = new HolofunkMachine(new LoopieEvent(), LoopieStateMachine.Instance, m_model.Player0);
            m_holofunkMachine1 = new HolofunkMachine(new LoopieEvent(), LoopieStateMachine.Instance, m_model.Player1);

            // Listen to the Wiimote
            if (m_wiimoteLib != null) {
                WiimoteController c = m_wiimoteLib.Wiimotes[0];

                new WiimoteHandler(this, m_holofunkMachine0, c);

                m_model.SetWiimote(0, c);

                if (m_wiimoteLib.Wiimotes.Count > 1) {
                    c = m_wiimoteLib.Wiimotes[1];

                    new WiimoteHandler(this, m_holofunkMachine1, c);

                    m_model.SetWiimote(1, c);
                }
            }
        }

        /// <summary>Dispose this and all its state.</summary>
        /// <remarks>This seems to be called twice... so making it robust to that.</remarks>
        protected override void Dispose(bool disposeManagedResources)
        {
            if (m_kinect != null) {
                m_kinect.Dispose();
                m_kinect = null;
            }

            if (m_holofunkBass != null) {
                m_holofunkBass.Dispose();
                m_holofunkBass = null;
            }

            base.Dispose(disposeManagedResources);
        }

        // [MainThread]
        protected override void Update(GameTime gameTime)
        {
            m_model.UpdateCount++;

            long ticks = DateTime.Now.Ticks;

            if (m_timeAtPreviousTick > 0) {
                m_model.TotalTickCount += ticks - m_timeAtPreviousTick;
            }
            m_timeAtPreviousTick = ticks;

            UpdateWorld();
        }

        int ChannelToPlayerIndex(int channel)
        {
            // trivial now, but may change someday, who knows
            return channel;
        }

        /// <summary>Allows the form to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.</summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        void UpdateWorld()
        {
            if (m_kinect == null) {
                // we've been disposed, do nothing
                return;
            }

            // update the tempo.  This ensures clock consistency from the point of view
            // of the scene graph (which is updated and rendered from the XNA thread).
            // We don't yet handle updating existing tracks, so don't change BPM if there are any.
            // TODO: add tempo shifting that works perfectly throughout the whole system.... EEECH
            if (m_model.RequestedBPM != m_clock.BPM && m_model.Loopies.Count == 0) {
                m_clock.BPM = m_model.RequestedBPM;
            }

            // Process all available responses from m_holofunkBass
            int channel;
            Track<float> newTrackIfAny;
            while (m_holofunkBass.TryUpdate(out channel, out newTrackIfAny)) {
                int playerIndex = ChannelToPlayerIndex(channel);
                PlayerModel playerModel = m_model.GetPlayerModel(playerIndex);

                if (newTrackIfAny != null) {
                    // NOW we can put the mike signal back on the mike itself.
                    playerModel.PlayerSceneGraph.Body.SetMikeSignalNode(playerModel.PlayerSceneGraph.Body.MikeHandGroup);

                    List<Loopie> loopies = m_model.Loopies;
                    loopies.Add(new Loopie(
                        m_loopieID++,
                        newTrackIfAny,
                        m_model.SceneGraph,
                        m_model.Content,
                        new Transform(
                            playerModel.LoopieModel.NewLoopPosition.Translation,
                            new Vector2(MagicNumbers.LoopieScale)),
                        playerIndex));
                }
            }

            Moment now = m_clock.Now;

            // and handle any requested removals
            lock (m_model.LoopiesToRemove) {
                while (m_model.LoopiesToRemove.Count > 0) {
                    Loopie toRemove = m_model.LoopiesToRemove[m_model.LoopiesToRemove.Count - 1];
                    toRemove.Dispose(now);
                    m_model.LoopiesToRemove.RemoveAt(m_model.LoopiesToRemove.Count - 1);
                    m_model.Loopies.Remove(toRemove);
                }

                m_model.LoopiesToRemove.Clear();
            }

            CheckBeatEvent(now, m_holofunkMachine0);
            CheckBeatEvent(now, m_holofunkMachine1);

            // First, pre-update the top-level Holofunk state.
            // (This mainly means, set all loopies to un-Touched.)
            foreach (Loopie loopie in m_model.Loopies) {
                loopie.Touched = false;
            }

            // now update the Holofunk model
            m_model.Update(now);

            // Invoke the current state update function.
            lock (m_holofunkMachine0) {
                m_holofunkMachine0.Update(now);
            }
            lock (m_holofunkMachine1) {
                m_holofunkMachine1.Update(now);
            }

            // Now have the loopies update to the current time.
            foreach (Loopie loopie in m_model.Loopies) {
                loopie.Update(now);
            }
        }

        void CheckBeatEvent(Moment now, HolofunkMachine machine)
        {
            lock (machine) {
                long timepointsSinceLastTransition = now.TimepointCount - machine.LastTransitionMoment.TimepointCount;
                if (timepointsSinceLastTransition > m_clock.TimepointsPerBeat) {
                    machine.OnNext(LoopieEvent.Beat, now);
                    machine.LastTransitionMoment = machine.LastTransitionMoment.PlusBeats(1);
                }
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            Render(GraphicsDevice, m_spriteBatch, gameTime, HolofunkView.Primary, SharpDX.Color.DarkGreen);
        }

        internal void Render(GraphicsDevice graphicsDevice, ISpriteBatch spriteBatch, GameTime gameTime, HolofunkView view, SharpDX.Color backgroundColor)
        {
            graphicsDevice.Clear(backgroundColor);

            m_model.SceneGraph.Render(graphicsDevice, spriteBatch, m_model.Content, view);
        }

        class WiimoteHandler
        {
            readonly Holofunk m_holofunk;
            readonly HolofunkMachine m_holofunkMachine;

            internal WiimoteHandler(Holofunk holofunk, HolofunkMachine holofunkMachine, WiimoteController c)
            {
                m_holofunk = holofunk;
                m_holofunkMachine = holofunkMachine;

                // NOTE that all these methods are actually called on a dedicated WiimoteLib thread!
                c.ButtonBChanged += new WiimoteController.ButtonEventHandler(ButtonBChanged);
                c.ButtonAChanged += new WiimoteController.ButtonEventHandler(ButtonAChanged);
                c.MinusChanged += new WiimoteController.ButtonEventHandler(MinusChanged);
                c.PlusChanged += new WiimoteController.ButtonEventHandler(PlusChanged);
                c.HomeChanged += new WiimoteController.ButtonEventHandler(HomeChanged);
                c.LeftChanged += new WiimoteController.ButtonEventHandler(LeftChanged);
                c.RightChanged += new WiimoteController.ButtonEventHandler(RightChanged);
                c.UpChanged += new WiimoteController.ButtonEventHandler(UpChanged);
                c.DownChanged += new WiimoteController.ButtonEventHandler(DownChanged);
                c.OneChanged += new WiimoteController.ButtonEventHandler(OneChanged);
                c.TwoChanged += new WiimoteController.ButtonEventHandler(TwoChanged);
            }

            void ButtonBChanged(bool state) { DispatchEvent(state ? LoopieEvent.TriggerDown : LoopieEvent.TriggerUp); }
            void ButtonAChanged(bool state) { DispatchEvent(state ? LoopieEvent.ADown : LoopieEvent.AUp); }
            void MinusChanged(bool state) { DispatchEvent(state ? LoopieEvent.MinusDown : LoopieEvent.MinusUp); }
            void PlusChanged(bool state) { DispatchEvent(state ? LoopieEvent.PlusDown : LoopieEvent.PlusUp); }
            void HomeChanged(bool state) { DispatchEvent(state ? LoopieEvent.HomeDown : LoopieEvent.HomeUp); }
            void LeftChanged(bool state) { DispatchEvent(state ? LoopieEvent.LeftDown : LoopieEvent.LeftUp); }
            void RightChanged(bool state) { DispatchEvent(state ? LoopieEvent.RightDown : LoopieEvent.RightUp); }
            void UpChanged(bool state) { DispatchEvent(state ? LoopieEvent.UpDown : LoopieEvent.UpUp); }
            void DownChanged(bool state) { DispatchEvent(state ? LoopieEvent.DownDown : LoopieEvent.DownUp); }
            void OneChanged(bool state) { DispatchEvent(state ? LoopieEvent.OneDown : LoopieEvent.OneUp); }
            void TwoChanged(bool state) { DispatchEvent(state ? LoopieEvent.TwoDown : LoopieEvent.TwoUp); }

            void DispatchEvent(LoopieEvent e)
            {
                lock (m_holofunkMachine) {
                    // event processing had better be very fast
                    m_holofunkMachine.OnNext(e, m_holofunk.m_clock.Now);
                }
            }
        }

    }
}
