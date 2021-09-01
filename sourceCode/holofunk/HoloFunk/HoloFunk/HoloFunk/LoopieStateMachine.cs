////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk;
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
    // Some states use the same model as their parents.  Other states change the model relative to
    // their parent states.
    // Either way, the State<TEvent, TModel, TBaseModel> class suffices, but is verbose.
    // These type synonyms make it much more readable to use either kind of state in the machine
    // definition below.

    using PlayerState = State<LoopieEvent, PlayerModel, PlayerModel>;
    using PlayerToPlayerMenuState = State<LoopieEvent, PlayerMenuModel<PlayerModel>, PlayerModel>;
    using PlayerToEffectState = State<LoopieEvent, PlayerEffectSpaceModel, PlayerModel>;
    using EffectState = State<LoopieEvent, PlayerEffectSpaceModel, PlayerEffectSpaceModel>;

    using PlayerAction = Action<LoopieEvent, PlayerModel>;
    using HoloTransition = Transition<LoopieEvent>;

    /// <summary>The loopie state machine for an individual player.</summary>
    /// <remarks>The actions here are actually Wiimote button presses.  This implies that the various
    /// action handlers are actually called from the WiimoteLib thread!
    /// 
    /// [WiimoteLibThread]
    /// 
    /// This becomes tricky, as the various transition handlers definitely do call into both
    /// ASIO and the scene graph, resulting in cross-thread interactions with the ASIO thread
    /// and XNA thread, respectively.  Yet we *want* this, especially for the ASIO thread, as
    /// any lost time here hurts our latency.
    /// 
    /// Currently the ASIO operations invoked here actually go via m_holofunkBass, which already
    /// handles cross-thread communication with the ASIO thread.  However, the scene graph
    /// operations are touchier; most of them pertain to updating color, etc., which is safe to
    /// do cross-thread, but there does seem room for collision between the code which adds a
    /// track that just finished recording (on the XNA thread), and the code which deletes a
    /// loopie (on the WiimoteLib thread).
    /// 
    /// Note that the scene graph currently has immutable parents; this actually helps
    /// significantly with avoiding truly breaking renderer-versus-state-machine-update state
    /// collisions.</remarks>
    class LoopieStateMachine : StateMachine<LoopieEvent>
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

        static void AddTransition(LoopieStateMachine ret, State<LoopieEvent> from, LoopieEvent evt, State<LoopieEvent> to)
        {
            ret.AddTransition(from, new HoloTransition(evt, to));
        }

        // Set up the state machine we want for our dear little Loopies.
        static LoopieStateMachine MakeLoopieStateMachine()
        {
            PlayerState root = new PlayerState("root", null, new PlayerAction[0], new PlayerAction[0]);

            #region Palette mode (initial)

            // Base state: just playing along.  ("Unselected" implies that something *can* be
            // selected, which will be true someday, but not yet.)
            var initial = new PlayerState(
                "initial",
                root,
                (evt, model) => {
                    model.PlayerSceneGraph.Body.WiiHandTexture = model.SceneGraph.Content.HollowCircle;
                    model.PlayerSceneGraph.Body.MikeHandColor = model.PlayerColor;
                    model.PlayerSceneGraph.Body.WiiHandColor = model.PlayerColor;
                });

            var ret = new LoopieStateMachine(initial, LoopieEventComparer.Instance);

            // Palette mode menu
            var paletteMenu = new PlayerToPlayerMenuState(
                "paletteMenu",
                root,
                (evt, model) => { },
                (evt, model) => model.RunSelectedMenuItem(),
                entryConversionFunc: playerModel => new PlayerMenuModel<PlayerModel>(
                    playerModel,
                    playerModel,
                    playerModel.PlayerSceneGraph,
                    playerModel.WiiHandPosition,
                    new MenuItem<PlayerModel>("Delete\nmy sounds", model => {
                        lock (model.LoopiesToRemove) {
                            foreach (Loopie loopie in model.Loopies) {
                                if (loopie.PlayerIndex == model.PlayerIndex) {
                                    model.LoopiesToRemove.Add(loopie);
                                }
                            }
                        }
                    }),
                    new MenuItem<PlayerModel>("Delete\n*ALL* sounds", model => {
                        lock (model.LoopiesToRemove) {
                            model.LoopiesToRemove.AddRange(model.Loopies);
                        }
                    }),
                    new MenuItem<PlayerModel>("Switch hands", model => model.RightHanded = !model.RightHanded),
                    new MenuItem<PlayerModel>("Switch\naudience\nview",
                        model => model.SecondaryView =
                            (model.SecondaryView == HolofunkView.Secondary
                                ? HolofunkView.Primary
                                : HolofunkView.Secondary)),
                    new MenuItem<PlayerModel>("Clear mike\neffects",
                        model => model.MicrophoneParameters.ShareAll(AllEffects.CreateParameterMap())),
                    new MenuItem<PlayerModel>("Clear loop\neffects",
                        model => {
                            model.ShareLoopParameters(AllEffects.CreateParameterMap());
                        }),
                    new MenuItem<PlayerModel>("+10 BPM", model => model.RequestedBPM += 10,
                        enabledFunc: model => model.Loopies.Count == 0),
                    new MenuItem<PlayerModel>("-10 BPM", model => model.RequestedBPM -= 10,
                        enabledFunc: model => model.Loopies.Count == 0)
                    ),
                exitConversionFunc: model => model.ExtractAndDetach()
                );

            AddTransition(ret, initial, LoopieEvent.HomeDown, paletteMenu);

            AddTransition(ret, paletteMenu, LoopieEvent.HomeUp, initial);

            #endregion

            #region Switch players with 2 button

            // Increase BPM state: we touched the right button in initial mode.
            // If there are no loopies, increase the requested BPM.
            PlayerState swapPlayers = new PlayerState(
                "swapPlayers",
                root,
                (evt, model) => model.SwapSkeletons());

            AddTransition(ret, initial, LoopieEvent.TwoDown, swapPlayers);

            AddTransition(ret, swapPlayers, LoopieEvent.TwoUp, initial);

            #endregion

            #region Effect mode

            var effectDragging = new PlayerToEffectState(
                "effectDraggingKnob",
                initial,
                (evt, model) => {
                    // if we got here via the 1 button, then the mike is selected initially
                    if (evt.Type == LoopieEventType.OneDown) {
                        model.MicrophoneSelected = true;
                    }
                    else {
                        model.MicrophoneSelected = false;
                    }

                    if (model.MicrophoneSelected) {
                        model.InitializeParametersFromMicrophone();
                        model.ShareMicrophoneParameters();
                    }
                    else {
                        model.InitializeParametersFromLoops();
                        model.ShareLoopParameters();
                    }

                    model.StartDragging();
                },
                (evt, model) => {
                    if (model.MicrophoneSelected) {
                        model.FlushMicrophoneParameters();
                    }
                    else {
                        model.FlushLoopParameters();
                    }

                    model.StopDragging();
                    model.DragStartLocation = Option<Vector2>.None;
                    model.CurrentKnobLocation = Option<Vector2>.None;

                    model.MicrophoneSelected = false;
                },
                entryConversionFunc: playerModel => new PlayerEffectSpaceModel(playerModel),
                exitConversionFunc: playerEffectModel => playerEffectModel.ExtractAndDispose()
                );

            AddTransition(ret, initial, LoopieEvent.ADown, effectDragging);

            AddTransition(ret, effectDragging, LoopieEvent.AUp, initial);

            AddTransition(ret, initial, LoopieEvent.OneDown, effectDragging);

            AddTransition(ret, effectDragging, LoopieEvent.OneUp, initial);

            #endregion

            #region D-pad effect preset changing

            var padUp = new PlayerState("padUp", initial, (evt, model) => model.SetEffectPresetIndex(0), (evt, model) => { });

            AddTransition(ret, initial, LoopieEvent.UpDown, padUp);
            AddTransition(ret, padUp, LoopieEvent.UpUp, initial);

            var padRight = new PlayerState("padRight", initial, (evt, model) => model.SetEffectPresetIndex(1), (evt, model) => { });

            AddTransition(ret, initial, LoopieEvent.RightDown, padRight);
            AddTransition(ret, padRight, LoopieEvent.RightUp, initial);

            var padDown = new PlayerState("padDown", initial, (evt, model) => model.SetEffectPresetIndex(2), (evt, model) => { });

            AddTransition(ret, initial, LoopieEvent.DownDown, padDown);
            AddTransition(ret, padDown, LoopieEvent.DownUp, initial);

            var padLeft = new PlayerState("padLeft", initial, (evt, model) => model.SetEffectPresetIndex(3), (evt, model) => { });

            AddTransition(ret, initial, LoopieEvent.LeftDown, padLeft);
            AddTransition(ret, padLeft, LoopieEvent.LeftUp, initial);

            #endregion

            #region Muting / unmuting

            // Mute everything we touch.
            PlayerState muting = new PlayerState(
                "muting",
                root,
                (evt, model) => {
                    model.PlayerSceneGraph.Body.WiiHandTexture = model.SceneGraph.Content.MuteCircle;

                    Option<Loopie> closest = model.LoopieModel.ClosestLoopie;

                    List<Loopie> touched = model.LoopieModel.TouchedLoopies;

                    if (closest.HasValue && closest.Value.Condition == LoopieCondition.Mute) {
                        // we don't delete if there are any UN-muted loopies in touching range
                        bool doDelete = true;
                        for (int i = 0; i < touched.Count; i++) {
                            if (touched[i].Condition != LoopieCondition.Mute) {
                                doDelete = false;
                                break;
                            }
                        }

                        // minus-down on a muted Loopie deletes it
                        if (doDelete) {
                            // we are on the Wiimote thread, so we need to synchronize vs. the game thread
                            lock (model.LoopiesToRemove) {
                                model.LoopiesToRemove.Add(closest.Value);
                            }

                            // and we DO NOT set the loopie effect to mute here;
                            // we just return
                            return;
                        }
                    }

                    // in all other cases, we start muting
                    model.LoopieModel.LoopieTouchEffect = loopie => loopie.SetCondition(LoopieCondition.Mute);
                },
                (evt, model) => {
                    model.LoopieModel.LoopieTouchEffect = loopie => { };
                    model.PlayerSceneGraph.Body.WiiHandColor = model.PlayerColor;
                });

            AddTransition(ret, initial, LoopieEvent.MinusDown, muting);

            AddTransition(ret, muting, LoopieEvent.MinusUp, initial);

            // Unmute everything we touch.
            PlayerState unmuting = new PlayerState(
                "unmuting",
                root,
                (evt, model) => {
                    model.LoopieModel.LoopieTouchEffect = loopie => loopie.SetCondition(LoopieCondition.Loop);
                    //model.SceneGraph.WiiHandColor = Color.Blue;
                    model.PlayerSceneGraph.Body.WiiHandTexture = model.SceneGraph.Content.UnmuteCircle;
                },
                (evt, model) => {
                    model.LoopieModel.LoopieTouchEffect = loopie => { };
                    model.PlayerSceneGraph.Body.WiiHandColor = model.PlayerColor;
                });

            AddTransition(ret, initial, LoopieEvent.PlusDown, unmuting);

            AddTransition(ret, unmuting, LoopieEvent.PlusUp, initial);

            #endregion

            #region Recording

            // We're holding down the trigger and recording.
            PlayerState recording = new PlayerState(
                "recording",
                root,
                (evt, model) => {
                    model.BassAudio.StartRecording(
                        model.AsioChannel, 
                        model.Loopies.Count + 1, 
                        model.MicrophoneParameters.Copy());

                    model.PlayerSceneGraph.Body.MikeHandColor = Color.Red;
                    model.PlayerSceneGraph.Body.WiiHandColor = Color.Red;
                    model.PlayerSceneGraph.Body.SetMikeSignalNode(model.PlayerSceneGraph.Body.WiiHandGroup);
                },
                (evt, model) => {
                    model.BassAudio.StopRecordingAtCurrentDuration(model.AsioChannel);

                    model.LoopieModel.NewLoopPosition = model.PlayerSceneGraph.Body.WiiHandGroup.LocalTransform;

                    model.PlayerSceneGraph.Body.NewTrackLocation.LocalTransform = model.LoopieModel.NewLoopPosition;

                    model.PlayerSceneGraph.Body.SetMikeSignalNode(model.PlayerSceneGraph.Body.NewTrackLocation);
                });

            AddTransition(ret, initial, LoopieEvent.TriggerDown, recording);

            AddTransition(ret, recording, LoopieEvent.TriggerUp, initial);

            #endregion

            return ret;
        }

        LoopieStateMachine(PlayerState initialState, IComparer<LoopieEvent> comparer)
            : base(initialState, comparer)
        {
        }
    }
}
