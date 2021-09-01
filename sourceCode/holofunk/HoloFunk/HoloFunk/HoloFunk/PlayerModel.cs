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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{    
    /// <summary>The state of a single Holofunk player.</summary>
    class PlayerModel : Model
    {
        /// <summary>Our player index.</summary>
        readonly int m_playerIndex;

        /// <summary>The ASIO channel for this player.</summary>
        /// <remarks>In practice this may be equal to the player index, but we don't wish to require this.</remarks>
        readonly int m_asioChannel;

        /// <summary>The parent state of which we are a component.</summary>
        readonly HolofunkModel m_holofunkModel;

        readonly PlayerLoopieModel m_loopieModel;

        /// <summary>Index of the current effect preset.</summary>
        int m_effectPresetIndex;

        /// <summary>Is this player right-handed?</summary>
        bool m_rightHanded = true;

        /// <summary>The sound effect parameters currently defined for the microphone.</summary>
        ParameterMap m_microphoneParameters;

        /// <summary>Is the microphone selected in parameter mode?</summary>
        bool m_microphoneSelected;

        /// <summary>This player's scene graph.</summary>
        PlayerSceneGraph m_playerSceneGraph;

        /// <summary>
        /// true iff the effect preset index was updated since the last Update()
        /// </summary>
        bool m_effectPresetIndexUpdated;

        internal PlayerModel(
            int playerIndex,
            int asioChannel,
            HolofunkModel holofunkModel)
        {
            m_playerIndex = playerIndex;
            m_asioChannel = asioChannel;

            m_holofunkModel = holofunkModel;

            m_loopieModel = new PlayerLoopieModel();

            // the microphone has only per-loop parameters
            m_microphoneParameters = AllEffects.CreateParameterMap();

            m_playerSceneGraph = new PlayerSceneGraph(holofunkModel.SceneGraph, playerIndex, asioChannel);
        }

        internal int PlayerIndex { get { return m_playerIndex; } }
        internal int AsioChannel { get { return m_asioChannel; } }
        internal List<Loopie> Loopies { get { return m_holofunkModel.Loopies; } }
        internal List<Loopie> LoopiesToRemove { get { return m_holofunkModel.LoopiesToRemove; } }
        internal HolofunkSceneGraph SceneGraph { get { return m_holofunkModel.SceneGraph; } }
        internal HolofunKinect Kinect { get { return m_holofunkModel.Kinect; } }
        internal HolofunkBass BassAudio { get { return m_holofunkModel.BassAudio; } }
        internal Clock Clock { get { return m_holofunkModel.Clock; } }
        internal bool RightHanded { get { return m_rightHanded; } set { m_rightHanded = value; } }

        internal int EffectPresetIndex { get { return m_effectPresetIndex; } }

        internal void SetEffectPresetIndex(int value)
        {
            m_effectPresetIndex = value;
            m_effectPresetIndexUpdated = true;
        }

        // The requested BPM.
        internal float RequestedBPM { get { return m_holofunkModel.RequestedBPM; } set { m_holofunkModel.RequestedBPM = value; } }

        internal PlayerSceneGraph PlayerSceneGraph { get { return m_playerSceneGraph; } }

        internal Color PlayerColor { get { return PlayerIndex == 0 ? Color.LightBlue : Color.LightGreen; } }

        internal PlayerLoopieModel LoopieModel { get { return m_loopieModel; } }

        // The sound effect parameters currently being applied to the microphone.
        internal ParameterMap MicrophoneParameters { get { return m_microphoneParameters; } }

        internal Vector2 WiiHandPosition { get { return m_playerSceneGraph.WiiHandPosition; } }

        /// <summary>Is the microphone selected?</summary>
        internal bool MicrophoneSelected { get { return m_microphoneSelected; } set { m_microphoneSelected = value; } }

        internal HolofunkView SecondaryView { get { return m_holofunkModel.SecondaryView; } set { m_holofunkModel.SecondaryView = value; } }

        internal void SwapSkeletons()
        {
            Kinect.SwapSkeletons();
        }

        /// <summary>Update the state as appropriate for "loopie mode" (the default, in which you are
        /// selecting and recording loopies).</summary>
        public override void Update(Moment now)
        {
            // Push the current microphone parameters to BASS.
            BassAudio.UpdateMicrophoneParameters(m_asioChannel, m_microphoneParameters, now);

            // Apply the current effect to all loopies within reach
            LoopieModel.InvalidateTouchedLoopies();

            // recalculate the touched-loopie set
            LoopieModel.CalculateTouchedLoopies(
                Loopies,
                PlayerSceneGraph.WiiHandPosition,
                PlayerSceneGraph.Body.HandDiameter,
                PlayerColor,
                Kinect.ViewportSize);

            List<Loopie> touched = LoopieModel.TouchedLoopies;
            foreach (Loopie loopie in touched) {
                LoopieModel.LoopieTouchEffect(loopie);
            }

            if (m_effectPresetIndexUpdated) {
                m_playerSceneGraph.Body.ShowEffectLabels(PlayerEffectSpaceModel.EffectSettings[EffectPresetIndex], now);
                m_effectPresetIndexUpdated = false;
            }

            PlayerSceneGraph.Update(this, m_holofunkModel.Kinect, now);
        }

        internal void UpdateFromChildState(Moment now)
        {
            PlayerSceneGraph.Update(this, m_holofunkModel.Kinect, now);

            if (MicrophoneSelected) {
                // Push the current microphone parameters to BASS.
                BassAudio.UpdateMicrophoneParameters(AsioChannel, MicrophoneParameters, now);
            }

            foreach (Loopie loopie in LoopieModel.TouchedLoopies) {
                loopie.Track.UpdateEffects(now);
                loopie.Touched = true;
            }
        }

        /// <summary>Initialize the destination map with the average of the parameter values of all touched
        /// loopies (and the microphone if applicable); then have all touched loopies (and the microphone if
        /// applicable) share those newly initialized parameters.</summary>
        internal void UpdateParameterMapFromTouchedLoopieValues(ParameterMap dest)
        {
            // initialize the parameters from the average of the values in the loopies & mike
            int count = LoopieModel.TouchedLoopies.Count + 1;

            IEnumerable<ParameterMap> touchedParameterMaps =
                LoopieModel.TouchedLoopies.Select(loopie => loopie.Track.Parameters);
            if (MicrophoneSelected) {
                touchedParameterMaps = touchedParameterMaps.Concat(new ParameterMap[] { MicrophoneParameters });
            }

            dest.SetFromAverage(Moment.Start, touchedParameterMaps);
        }

        internal void ShareLoopParameters(ParameterMap parameters)
        {
            foreach (Loopie loopie in LoopieModel.TouchedLoopies) {
                loopie.Track.Parameters.ShareAll(parameters);
                // TODO: figure out how to handle these moments outside of time... Moment.Start is terrible
                loopie.Track.UpdateEffects(Moment.Start);
            }
        }
    }
}
