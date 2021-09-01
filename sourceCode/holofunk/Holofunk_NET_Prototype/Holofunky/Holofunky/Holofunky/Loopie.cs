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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    using HoloState = State<LoopieEvent, HolofunkState>;
    using HoloAction = Action<HolofunkState>;
    using HoloTransition = Transition<LoopieEvent, HolofunkState>;

    /// <summary>
    /// The varieties of events in our Loopie machine.
    /// </summary>
    /// <remarks>
    /// The comments following are the payload of that variety of event.
    /// </remarks>
    enum LoopieEventType
    {
        None,       // uninitialized value; to catch inadvertent defaults
        TriggerDown,// trigger pressed
        TriggerUp,  // trigger released
        MinusDown,  // "-" button down
        MinusUp,    // "-" button up
        PlusDown,   // "+" button down
        PlusUp,     // "+" button up
        HomeDown,   // home button down
        HomeUp,     // home button up
    }

    /// <summary>
    /// An event in a Loopie machine.
    /// </summary>
    /// <remarks>
    /// This deliberately contains the union of all event type payloads.
    /// We may wind up using a "ref struct" idiom to reduce copy cost here.
    /// </remarks>
    struct LoopieEvent
    {
        readonly LoopieEventType m_type;

        /// <summary>
        /// The type of event.
        /// </summary>
        internal LoopieEventType Type { get { return m_type; } }

        internal LoopieEvent(LoopieEventType type) { m_type = type; }

        internal static LoopieEvent TriggerDown { get { return new LoopieEvent(LoopieEventType.TriggerDown); } }
        internal static LoopieEvent TriggerUp { get { return new LoopieEvent(LoopieEventType.TriggerDown); } }
        internal static LoopieEvent MinusDown { get { return new LoopieEvent(LoopieEventType.MinusDown); } }
        internal static LoopieEvent MinusUp { get { return new LoopieEvent(LoopieEventType.MinusUp); } }
        internal static LoopieEvent PlusDown { get { return new LoopieEvent(LoopieEventType.PlusDown); } }
        internal static LoopieEvent PlusUp { get { return new LoopieEvent(LoopieEventType.PlusUp); } }
        internal static LoopieEvent HomeDown { get { return new LoopieEvent(LoopieEventType.HomeDown); } }
        internal static LoopieEvent HomeUp { get { return new LoopieEvent(LoopieEventType.HomeUp); } }
    }

    class LoopieEventComparer : IComparer<LoopieEvent>
    {
        internal static readonly LoopieEventComparer Instance = new LoopieEventComparer();

        public int Compare(LoopieEvent x, LoopieEvent y)
        {
            // we disregard whether two events are targeted at the same loopie,
            // as this does not affect state machine event dispatching
            int delta = (int)x.Type - (int)y.Type;
            return delta;
        }
    }

    /// <summary>
    /// The status of a given Loopie.
    /// </summary>
    [Flags]
    enum LoopieCondition
    {
        Loop = 0x2,
        Mute = 0x4,
    }

    /// <summary>
    /// The state of Holofunk as a whole, viewed from the LoopieStateMachine.
    /// </summary>
    class HolofunkState
    {
        // basic component access
        readonly HolofunkSceneGraph m_sceneGraph;
        readonly List<Loopie> m_loopies = new List<Loopie>();
        readonly HolofunkBass m_bass;
        readonly HolofunKinect m_kinect;

        // Where should the newly placed loop go?
        // If we released the trigger before the end of the current beat, then we might wiggle around
        // after releasing the trigger but before the beat starts to play.  But you want to "drop" the
        // loopie at the point where you *released* the trigger.  This field allows us to save where
        // exactly that was.
        Transform m_newLoopPosition;

        // What effect are we applying to loopies we approach?
        // Default: nada.
        // This must be idempotent since right now we apply it like mad on every update!
        Action<Loopie> m_effect = loopie => { };

        public HolofunkState(HolofunkSceneGraph sceneGraph, HolofunkBass bass, HolofunKinect kinect)
        {
            m_sceneGraph = sceneGraph;
            m_bass = bass;
            m_kinect = kinect;
        }

        public HolofunkSceneGraph SceneGraph { get { return m_sceneGraph; } }
        public List<Loopie> Loopies { get { return m_loopies; } }
        public HolofunkBass BassAudio { get { return m_bass; } }
        public HolofunKinect Kinect { get { return m_kinect; } }
        public Transform NewLoopPosition { get { return m_newLoopPosition; } set { m_newLoopPosition = value; } }
        public Action<Loopie> LoopieEffect { get { return m_effect; } set { m_effect = value; } }

        // Get the Loopie that is closest to the Wii hand and within grabbing distance.
        // Return null if none exists.
        internal Loopie GrabbedLoopie()
        {
            Transform handPosition = m_kinect.GetJointViewportPosition(m_kinect.WiiHand);

            Loopie closest = null;
            double minDistSquared = double.MaxValue;
            double handDiameter = m_sceneGraph.HandDiameter / 2;
            double handDiameterSquared = m_sceneGraph.HandDiameter * m_sceneGraph.HandDiameter;

            foreach (Loopie loopie in Loopies) {
                Transform loopiePosition = loopie.Position;
                double xDist = loopiePosition.Translation.X - handPosition.Translation.X;
                double yDist = loopiePosition.Translation.Y - handPosition.Translation.Y;
                double distSquared = xDist * xDist + yDist * yDist;
                if (distSquared < handDiameterSquared && distSquared < minDistSquared) {
                    closest = loopie;
                    minDistSquared = distSquared;
                }
            }

            return closest;
        }
    }

    class LoopieStateMachine : StateMachine<LoopieEvent, HolofunkState>
    {
        static LoopieStateMachine s_instance;

        internal static LoopieStateMachine Instance 
        { 
            get 
            {
                // on-demand initialization ensures no weirdness about static initializer ordering
                if (s_instance == null) {
                    s_instance = MakeLoopieStateMachine();
                }
                return s_instance; 
            } 
        }

        // Set up the state machine we want for our dear little Loopies.
        static LoopieStateMachine MakeLoopieStateMachine()
        {
            HoloState root = new HoloState(null, new HoloAction[0], new HoloAction[0]);

            // Base state: just playing along.  ("Unselected" implies that something *can* be
            // selected, which will be true someday, but not yet.)
            HoloState unselected = new HoloState(
                root,
                state => {
                    state.SceneGraph.SetMikeSignalNode(state.SceneGraph.MikeHandNode);
                    state.SceneGraph.WiiHandNode.Color = Color.White;
                });

            // Mute everything we touch.
            HoloState muting = new HoloState(
                root,
                state => {
                    Loopie grabbed = state.GrabbedLoopie();
                    if (grabbed != null && grabbed.Condition == LoopieCondition.Mute) {
                        // minus-down on a muted Loopie deletes it
                        grabbed.Dispose();
                        state.Loopies.Remove(grabbed);
                    }

                    state.LoopieEffect = loopie => loopie.SetCondition(LoopieCondition.Mute);
                    state.SceneGraph.WiiHandNode.Color = HolofunkSceneGraph.MuteColor;
                },
                state => {
                    state.LoopieEffect = loopie => { };
                    state.SceneGraph.WiiHandNode.Color = Color.White;
                });

            // Unmute everything we touch.
            HoloState unmuting = new HoloState(
                root,
                state => {
                    state.LoopieEffect = loopie => loopie.SetCondition(LoopieCondition.Loop);
                    state.SceneGraph.WiiHandNode.Color = Color.Blue;
                },
                state => {
                    state.LoopieEffect = loopie => { };
                    state.SceneGraph.WiiHandNode.Color = Color.White;
                });

            // Wipe the whole thing
            HoloState wipe = new HoloState(
                root,
                state => {
                    foreach (Loopie loopie in state.Loopies) {
                        loopie.Dispose();
                    }
                    state.Loopies.Clear();
                },
                state => { });

            // We're holding down the trigger and recording.
            HoloState recording = new HoloState(
                root,
                state => {
                    state.SceneGraph.WiiHandNode.Color = Color.Red;
                    state.BassAudio.StartRecording(state.Loopies.Count + 1);
                    state.SceneGraph.SetMikeSignalNode(state.SceneGraph.WiiHandNode);
                },
                state => {
                    state.BassAudio.StopRecordingAtNextBeat();
                    state.NewLoopPosition = state.Kinect.GetJointViewportPosition(state.Kinect.WiiHand);
                    state.SceneGraph.SetMikeSignalNode(state.SceneGraph.MikeHandNode);
                });

            var ret = new LoopieStateMachine(unselected, LoopieEventComparer.Instance);

            ret.AddTransition(unselected,
                new HoloTransition(LoopieEvent.TriggerDown, recording));

            ret.AddTransition(recording,
                new HoloTransition(LoopieEvent.TriggerUp, unselected));

            ret.AddTransition(unselected,
                new HoloTransition(LoopieEvent.MinusDown, muting));

            ret.AddTransition(muting,
                new HoloTransition(LoopieEvent.MinusUp, unselected));

            ret.AddTransition(unselected,
                new HoloTransition(LoopieEvent.PlusDown, unmuting));

            ret.AddTransition(unmuting,
                new HoloTransition(LoopieEvent.PlusUp, unselected));

            ret.AddTransition(unselected,
                new HoloTransition(LoopieEvent.HomeDown, wipe));

            ret.AddTransition(wipe,
                new HoloTransition(LoopieEvent.HomeUp, unselected));

            return ret;
        }

        LoopieStateMachine(HoloState initialState, IComparer<LoopieEvent> comparer)
            : base(initialState, comparer)
        {
        }
    }

    /// <summary>
    /// An abstract "widget" that allows control of a Loop.
    /// </summary>
    /// <remarks>
    /// Contains the user interaction state machine for creating and controlling loops.
    /// 
    /// For now, also contains the controls and rendering logic; eventually will want to
    /// break this out to allow disjoint UIs while preserving the same Loopie internal logic.
    /// </remarks>
    class Loopie : IDisposable
    {
        readonly int m_id;
        LoopieCondition m_condition;
        Track<float> m_track;
        Transform m_position;
        HolofunkSceneGraph m_sceneGraph;
        SpriteNode m_spriteNode;

        readonly static Color[] s_colors = new[] {
            Color.Blue,
            Color.Purple,
            Color.SeaGreen,
            Color.Honeydew,
            Color.DarkOrchid,
            Color.Aqua,
            Color.Magenta,
            Color.SteelBlue,
            Color.Tomato
        };

        // Loop m_loop;

        internal Loopie(int id, Track<float> track, HolofunkSceneGraph sceneGraph, Transform position)
        {
            m_id = id;
            m_track = track;
            m_position = position;

            SetCondition(LoopieCondition.Loop);

            m_sceneGraph = sceneGraph;
            m_spriteNode = sceneGraph.CreateScaledSprite(
                position, 
                // set the scale proportionately to the maximum level (on both channels)
                () => Math.Max(m_track.InputLevelL, m_track.InputLevelR),
                // set the color: gray if muted, otherwise based on our unique ID
                () => m_condition == LoopieCondition.Mute 
                    ? HolofunkSceneGraph.MuteColor
                    : s_colors[m_id % s_colors.Length]);
        }

        internal int Id { get { return m_id; } }
        internal Transform Position { get { return m_position; } }
        internal LoopieCondition Condition { get { return m_condition; } }

        internal void SetCondition(LoopieCondition condition)
        {
            m_condition = condition;

            if (m_condition == LoopieCondition.Mute) {
                m_track.SetVolume(0);
            }
            else {
                m_track.SetVolume(1.0f);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            m_track.SetVolume(0);
            m_track.Dispose();
            
            m_sceneGraph.RemoveScaledSprite(m_spriteNode);
        }

        #endregion
    }
}
