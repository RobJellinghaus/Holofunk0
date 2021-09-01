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
    /// <summary>The parameters for each axis in effect space.</summary>
    /// <remarks>Clockwise from Up.</remarks>
    class EffectSettings
    {
        internal readonly ParameterMap Up, Right, Down, Left;
        internal readonly string UpLabel, RightLabel, DownLabel, LeftLabel;

        internal EffectSettings(string[] labels, ParameterMap[] parameterMaps)
        {
            UpLabel = labels[0];
            RightLabel = labels[1];
            DownLabel = labels[2];
            LeftLabel = labels[3];

            Up = parameterMaps[0];
            Right = parameterMaps[1];
            Down = parameterMaps[2];
            Left = parameterMaps[3];
        }
    }

    /// <summary>The effect space state.</summary>
    /// <remarks>This is written to support being in effect space without dragging, though we don't
    /// currently use it that way.</remarks>
    class PlayerEffectSpaceModel : Model
    {
        static ParameterMap Map(ParameterDescription description, float value)
        {
            return new ParameterMap().Add(new ConstantParameter(description, value, false));
        }

        static ParameterMap Map2(ParameterDescription description1, float value1, ParameterDescription description2, float value2)
        {
            return new ParameterMap().Add(new ConstantParameter(description1, value1, false))
                .Add(new ConstantParameter(description2, value2, false));
        }

        public static EffectSettings[] EffectSettings = new[] {
            new EffectSettings(
                new[] { "Loud", "Pan R", "Soft", "Pan L" },
                new[] { 
                    Map(VolumeEffect.Volume, 1),
                    Map(PanEffect.Pan, 1),
                    Map(VolumeEffect.Volume, 0),
                    Map(PanEffect.Pan, 0)
                }),
            new EffectSettings(
                new[] { "VowelFilter", "RingMod", "Kompress", "DadaFlang" },
                new[] { 
                    Map(TurnadoAAA1Effect.VowelFilter, 1),
                    Map(TurnadoAAA1Effect.RingModulator, 1),
                    Map(TurnadoAAA1Effect.Kompressor, 1),
                    Map(TurnadoAAA1Effect.DadaismFlanger, 1)  
                }),
            new EffectSettings(
                new[] { "Autofreeze", "Backbreak", "BEWARE", "StrangeTone" },
                new[] { 
                    Map(TurnadoAAA1Effect.AutoFreeze, 1),
                    Map(TurnadoAAA1Effect.Backgroundbreak, 1),
                    Map(TurnadoAAA1Effect.SliceWarz, 1),
                    Map(TurnadoAAA1Effect.StrangeTone, 1)
                }),
            new EffectSettings(
                new[] { "DX8 Flanger", "DX8 Echo", "DX8 Reverb", "DX8 Chorus" },
                new[] { 
                    Map2(FlangerEffect.WetDry, 1, FlangerEffect.Depth, 1),
                    Map2(EchoEffect.WetDry, 1, EchoEffect.Feedback, 1),
                    Map2(ReverbEffect.Mix, 1, ReverbEffect.Time, 1),
                    Map2(ChorusEffect.WetDry, 1, ChorusEffect.Feedback, 1)
                })
        };

        readonly PlayerModel m_playerModel;

        readonly PlayerEffectSpaceSceneGraph m_sceneGraph;

        /// <summary>The base parameter values when we began dragging.</summary>
        ParameterMap m_baseParameters;

        /// <summary>The parameter map mutated by this interface while dragging.</summary>
        ParameterMap m_parameters;

        /// <summary>The effect settings currently in effect.</summary>
        EffectSettings m_effectSettings;

        /// <summary>If we are dragging, this is where we started.</summary>
        Option<Vector2> m_dragStartLocation;

        /// <summary>If we are dragging, this is where the effect knob is currently located.</summary>
        Option<Vector2> m_currentKnobLocation;

        internal PlayerEffectSpaceModel(PlayerModel playerModel)
        {
            m_playerModel = playerModel;
            m_effectSettings = EffectSettings[playerModel.EffectPresetIndex];

            m_sceneGraph = new PlayerEffectSpaceSceneGraph(m_playerModel.PlayerSceneGraph, this);

            m_playerModel.PlayerSceneGraph.Body.WiiHandTexture = m_playerModel.PlayerSceneGraph.Content.EffectCircle;

            m_playerModel.PlayerSceneGraph.Body.SetEffectLabelNode(m_sceneGraph.BoundingCircleNode);
        }

        internal Option<Vector2> DragStartLocation
        {
            get { return m_dragStartLocation; }
            set { m_dragStartLocation = value; }
        }

        internal Option<Vector2> CurrentKnobLocation
        {
            get { return m_currentKnobLocation; }
            set { m_currentKnobLocation = value; }
        }

        internal bool MicrophoneSelected
        {
            get { return m_playerModel.MicrophoneSelected; }
            set { m_playerModel.MicrophoneSelected = value; }
        }

        internal Vector2 WiiHandPosition
        {
            get { return m_playerModel.WiiHandPosition; }
        }

        internal PlayerModel ExtractAndDispose()
        {
            m_playerModel.PlayerSceneGraph.Body.WiiHandTexture = m_playerModel.PlayerSceneGraph.Content.HollowCircle;
            m_playerModel.PlayerSceneGraph.Body.SetEffectLabelNode(m_playerModel.PlayerSceneGraph.Body.WiiHandGroup);
            m_sceneGraph.RootNode.Detach();
            return m_playerModel;
        }

        public void InitializeParametersFromLoops()
        {
            m_parameters = AllEffects.CreateParameterMap();
            m_playerModel.UpdateParameterMapFromTouchedLoopieValues(m_parameters);
            m_baseParameters = m_parameters.Copy();
        }

        public void InitializeParametersFromMicrophone()
        {
            m_parameters = m_playerModel.MicrophoneParameters.Copy(forceMutable: true);
            m_baseParameters = m_parameters.Copy();
        }

        public void ShareLoopParameters()
        {
            m_playerModel.ShareLoopParameters(m_parameters);
        }

        internal void FlushLoopParameters()
        {
            m_playerModel.ShareLoopParameters(m_parameters.Copy());
        }

        internal void ShareMicrophoneParameters()
        {
            m_playerModel.MicrophoneParameters.ShareAll(m_parameters);
        }

        internal void FlushMicrophoneParameters()
        {
            m_playerModel.MicrophoneParameters.ShareAll(m_parameters.Copy());
        }

        public override void Update(Moment now)
        {
            if (!m_dragStartLocation.HasValue) {
                HoloDebug.Assert(!m_currentKnobLocation.HasValue);

                // when not dragging, we handle all selection, etc. as usual; 
                // e.g. we delegate to the usual player code
                m_playerModel.Update(now);
                m_playerModel.PlayerSceneGraph.Update(m_playerModel, m_playerModel.Kinect, now);
            }
            else {
                if (m_playerModel.MicrophoneSelected) {
                    // when the mike is being dragged, we don't have any touched loopies
                    m_playerModel.LoopieModel.TouchedLoopies.Clear();
                }

                HoloDebug.Assert(m_currentKnobLocation.HasValue);

                Vector2 knobDelta = GetKnobDelta(m_playerModel.WiiHandPosition);
                m_currentKnobLocation = m_dragStartLocation.Value + knobDelta;

                RecalculateDragEffect(now, knobDelta);

                m_playerModel.UpdateFromChildState(now);
            }

            m_sceneGraph.Update(m_playerModel, m_playerModel.Kinect, now);
        }

        /// <summary>Get the vector from the drag start location to the knob, given the current hand position.</summary>
        Vector2 GetKnobDelta(Vector2 currentDragLocation)
        {
            Vector2 delta = currentDragLocation - m_dragStartLocation.Value;

            // Now what we want is to keep the angle of the delta, but clamp its length.
            if (delta.Length() > BoundingCircleRadius) {
                // we want to normalize delta and then clamp it at boundingCircleRadius
                delta.Normalize();
                delta *= BoundingCircleRadius;
            }

            return delta;
        }

        float BoundingCircleRadius
        {
            get
            {
                float boundingCircleRadius = m_playerModel.PlayerSceneGraph.Content.HollowCircle.Width
                    * MagicNumbers.EffectSpaceBoundingCircleMultiple
                    / 2;
                return boundingCircleRadius;
            }
        }

        internal void StartDragging()
        {
            Rectangle boundingRect = new Rectangle(
                (int)BoundingCircleRadius,
                (int)BoundingCircleRadius,
                (int)(m_playerModel.PlayerSceneGraph.ViewportSize.X - BoundingCircleRadius),
                (int)(m_playerModel.PlayerSceneGraph.ViewportSize.Y - BoundingCircleRadius));

            Vector2 withinBounds = boundingRect.Clamp(m_playerModel.WiiHandPosition);

            DragStartLocation = withinBounds;
            CurrentKnobLocation = withinBounds;
        }

        internal void StopDragging()
        {
            DragStartLocation = Option<Vector2>.None;
            CurrentKnobLocation = Option<Vector2>.None;
        }

        /// <summary>The user's dragged the knob; update the effects appropriately.</summary>
        void RecalculateDragEffect(Moment now, Vector2 knobDelta)
        {
            // First, we want to map knobDelta -- that is effectively a vector to the bounding circle --
            // to be a vector to the unit square.
            Vector2 normalizedDelta = knobDelta;
            normalizedDelta.Normalize();

            // Now we want to find the longest dimension of normalizedDelta, and increase it to 1.
            float longestDimension = Math.Max(Math.Abs(normalizedDelta.X), Math.Abs(normalizedDelta.Y));

            if (longestDimension < 0.0001f) {
                // might as well just be zero, so leave normalizedDelta alone
            }
            else {
                float longestDimensionMultiplier = 1 / longestDimension;

                // Scaling a vector does not change its angle.
                normalizedDelta *= longestDimensionMultiplier;
            }

            // Now normalizedDelta is effectively the vector to the unit square!  Leave a little epsilon at the limit...
            HoloDebug.Assert(Math.Abs(normalizedDelta.X) <= 1.0001f);
            HoloDebug.Assert(Math.Abs(normalizedDelta.Y) <= 1.0001f);

            // Finally, the vector we really want is normalizedDelta multiplied by 
            // knobDelta's length divided by the circle's radius.
            float lengthFraction = knobDelta.Length() / BoundingCircleRadius;
            Vector2 actualDelta = normalizedDelta * lengthFraction;

            Spam.Model.WriteLine("normalizedDelta: (" + normalizedDelta.X + ", " + normalizedDelta.Y + "); lengthFraction " + lengthFraction + "; actualDelta: (" + actualDelta.X + ", " + actualDelta.Y + ")");

            // OK, now we have our X and Y values.  Figure out which we are going to apply.
            ParameterMap vertical = normalizedDelta.Y < 0 ? m_effectSettings.Up : m_effectSettings.Down;
            ParameterMap horizontal = normalizedDelta.X < 0 ? m_effectSettings.Left : m_effectSettings.Right;

            foreach (Parameter p in vertical) {
                DragParameter(now, horizontal, p, Math.Abs(actualDelta.Y));
            }

            foreach (Parameter p in horizontal) {
                DragParameter(now, null, p, Math.Abs(actualDelta.X));
            }
        }

        void DragParameter(Moment now, ParameterMap other, Parameter p, float value)
        {
            // We have a tiny gutter around 0, since some effects have a sharp step from 0 to anything else and
            // this sounds more jarring if it happens with no tolerance.
            if (value < 0.05f) {
                value = 0;
            }
            // We deliberately use SSA-like structure here to be able to see all the intermediate values in the debugger.
            // First, get the endpoint value of p
            float pValue = p[now];
            // Next, get the base value of p, in normalized terms
            ParameterDescription pDesc = p.Description;
            float pBaseValueNormalized = (pDesc.Base - pDesc.Min) / (pDesc.Max - pDesc.Min);

            // Now we want to move from pBaseValueNormalized towards pValue, by an amount proportional to dragValue
            float newDestValue = pBaseValueNormalized + ((pValue - pBaseValueNormalized) * value);

            Parameter dest = m_parameters[p.Description];
            float baseValue = m_baseParameters[p.Description][now];

            float averagedDestValue = newDestValue;
            if (other != null && other.Contains(p.Description)) {
                averagedDestValue = (averagedDestValue + other[p.Description][now]) / 2;
            }

            float adjustedDestValue = averagedDestValue;
            if (!p.Description.Absolute) {
                // only grow upwards from the base value, proportionately to how big the base value already is
                adjustedDestValue = baseValue + (averagedDestValue * (1 - baseValue));
            }

            Spam.Model.WriteLine("parameter " + p.Description.Name + ": pValue " + pValue + ", pBVN " + pBaseValueNormalized + ", baseValue " + baseValue + ", adjustedDestValue " + adjustedDestValue);

            // clamp to [0, 1] because we may overshoot a bit with this algorithm
            dest[now] = Math.Max(0, Math.Min(1f, adjustedDestValue));
        }
    }
}
