////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Vst;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A particular variety of sound effect that can apply to a Track.</summary>
    /// <remarks>Effects are instantiated per-Track (since some Effects have BASS-related state associated
    /// with them).</remarks>
    public abstract class HolofunkEffect
    {
        /// <summary>Apply this effect's parameters at the given moment to the given track.</summary>
        public abstract void Apply(ParameterMap parameters, Moment now);
    }

    /// <summary>Effects that are implemented by Bass.</summary>
    public abstract class BassEffect : HolofunkEffect
    {
        // The BASS stream handle to which the effect applies.
        readonly StreamHandle m_streamHandle;

        protected BassEffect(StreamHandle streamHandle)
        {
            m_streamHandle = streamHandle;
        }

        protected StreamHandle StreamHandle { get { return m_streamHandle; } }
    }

    public abstract class SimpleBassEffect : BassEffect
    {
        protected SimpleBassEffect(StreamHandle streamHandle)
            : base(streamHandle) 
        {
        }

        protected abstract BASSAttribute Attribute { get; }

        protected abstract ParameterDescription Parameter { get; }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            float f = parameters[Parameter].GetInterpolatedValue(now);
            Bass.BASS_ChannelSetAttribute((int)StreamHandle, Attribute, f);
        }
    }

    public class PanEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(PanEffect), "balance", -1, 0, 0, 1, absolute: true);

        public static ParameterDescription Pan { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_PAN; } }

        PanEffect(StreamHandle streamHandle) : base(streamHandle) { }

        static PanEffect()
        {
            AllEffects.Register(
                typeof(PanEffect),
                (streamHandle, form) => new PanEffect(streamHandle),
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public class VolumeEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(VolumeEffect), "volume", 0, 0.5f, 0.7f, 1, absolute: true);

        public static ParameterDescription Volume { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_VOL; } }

        VolumeEffect(StreamHandle streamHandle) : base(streamHandle) { }

        static VolumeEffect()
        {
            AllEffects.Register(
                typeof(VolumeEffect),
                (streamHandle, form) => new VolumeEffect(streamHandle), 
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public abstract class BassDX8Effect<TEffectArgs> : BassEffect
        where TEffectArgs : class
    {
        TEffectArgs m_fxArgs;
        FxHandle m_fxHandle;

        protected BassDX8Effect(StreamHandle streamHandle, BASSFXType fxType, TEffectArgs effectArgs)
            : base(streamHandle)
        {
            m_fxArgs = effectArgs;

            m_fxHandle = (FxHandle)Bass.BASS_ChannelSetFX((int)streamHandle, fxType, 0);
            BASSError error = Bass.BASS_ErrorGetCode();
            HoloDebug.Assert(m_fxHandle != 0);

            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected void Apply()
        {
            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected TEffectArgs EffectArgs { get { return m_fxArgs; } }
    }

    public class EchoEffect : BassDX8Effect<BASS_DX8_ECHO>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(EchoEffect), "echo wet/dry", 0, 0, 0, 100);
        public static ParameterDescription Feedback = new ParameterDescription(typeof(EchoEffect), "echo feedback", 0, 0, 50, 100);

        EchoEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_ECHO, new BASS_DX8_ECHO(0f, 50f, 333f, 333f, false))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDryMix = (int)parameters[WetDry].GetInterpolatedValue(now);
            EffectArgs.fFeedback = parameters[Feedback].GetInterpolatedValue(now);
            Apply();
        }

        static EchoEffect()
        {
            AllEffects.Register(
                typeof(EchoEffect),
                (streamHandle, form) => new EchoEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry, Feedback }));
        }
    }

    public class ReverbEffect : BassDX8Effect<BASS_DX8_REVERB>
    {
        public static ParameterDescription Time = new ParameterDescription(typeof(ReverbEffect), "reverb time", 0.001f, 0.001f, 500f, 3000f);
        public static ParameterDescription Mix = new ParameterDescription(typeof(ReverbEffect), "reverb mix", -96f, -96f, -96f, 0f);

        ReverbEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_REVERB, new BASS_DX8_REVERB(0f, -96f, 500f, 0.001f))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fReverbTime = parameters[Time].GetInterpolatedValue(now);
            EffectArgs.fReverbMix = (int)parameters[Mix].GetInterpolatedValue(now);
            Apply();
        }

        static ReverbEffect()
        {
            AllEffects.Register(
                typeof(ReverbEffect),
                (streamHandle, form) => new ReverbEffect(streamHandle),
                new List<ParameterDescription>(new[] { Time, Mix }));
        }
    }

    public class FlangerEffect : BassDX8Effect<BASS_DX8_FLANGER>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(FlangerEffect), "flanger wet/dry", 0, 0, 0, 100);
        public static ParameterDescription Depth = new ParameterDescription(typeof(ReverbEffect), "flanger depth", 0, 0, 25, 100);

        FlangerEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_FLANGER, new BASS_DX8_FLANGER(0f, 25f, 80f, 8f, 1, 30f, BASSFXPhase.BASS_FX_PHASE_NEG_90))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDryMix = parameters[WetDry].GetInterpolatedValue(now);
            EffectArgs.fDepth = (int)parameters[Depth].GetInterpolatedValue(now);
            Apply();
        }

        static FlangerEffect()
        {
            AllEffects.Register(
                typeof(FlangerEffect),
                (streamHandle, form) => new FlangerEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry, Depth }));
        }
    }

    public class ChorusEffect : BassDX8Effect<BASS_DX8_CHORUS>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(ChorusEffect), "chorus wet/dry", 0, 0, 0, 100);
        public static ParameterDescription Feedback = new ParameterDescription(typeof(ChorusEffect), "feedback", -99, 0, 0, 99);

        ChorusEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_CHORUS, new BASS_DX8_CHORUS(0f, 25f, 0f, 0f, 1, 0, BASSFXPhase.BASS_FX_PHASE_ZERO))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDryMix = parameters[WetDry].GetInterpolatedValue(now);
            EffectArgs.fFeedback = parameters[Feedback].GetInterpolatedValue(now);
            Apply();
        }

        static ChorusEffect()
        {
            AllEffects.Register(
                typeof(ChorusEffect),
                (streamHandle, form) => new ChorusEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry, Feedback }));
        }
    }

    public class TurnadoAAA1Effect : BassEffect
    {
        public static ParameterDescription AutoFreeze = new ParameterDescription(typeof(TurnadoAAA1Effect), "AutoFreeze", 0, 0, 0, 1);
        public static ParameterDescription Kompressor = new ParameterDescription(typeof(TurnadoAAA1Effect), "Kompressor", 0, 0, 0, 1);
        public static ParameterDescription DadaismFlanger = new ParameterDescription(typeof(TurnadoAAA1Effect), "DadaismFlanger", 0, 0, 0, 1);
        public static ParameterDescription StrangeTone = new ParameterDescription(typeof(TurnadoAAA1Effect), "StrangeTone", 0, 0, 0, 1);
        public static ParameterDescription VowelFilter = new ParameterDescription(typeof(TurnadoAAA1Effect), "VowelFilter", 0, 0, 0, 1);
        public static ParameterDescription SliceWarz = new ParameterDescription(typeof(TurnadoAAA1Effect), "SliceWarz", 0, 0, 0, 1);
        public static ParameterDescription RingModulator = new ParameterDescription(typeof(TurnadoAAA1Effect), "RingModulator", 0, 0, 0, 1);
        public static ParameterDescription Backgroundbreak = new ParameterDescription(typeof(TurnadoAAA1Effect), "Backgroundbreak", 0, 0, 0, 1);

        readonly StreamHandle m_vstStream;

        public TurnadoAAA1Effect(StreamHandle streamHandle, Form baseForm)
            : base(streamHandle)
        {
            m_vstStream = (StreamHandle)BassVst.BASS_VST_ChannelSetDSP(
                (int)streamHandle,
                Path.Combine(System.Environment.CurrentDirectory.ToString(), "Turnado.dll"),
                BASSVSTDsp.BASS_VST_DEFAULT,
                0);

            const int ourProgramIndex = 1;
            string ourPresetName = "AAA2";

            string[] programNames = BassVst.BASS_VST_GetProgramNames((int)m_vstStream);
            HoloDebug.Assert(programNames[ourProgramIndex] == ourPresetName);

            int currentProgramIndex = BassVst.BASS_VST_GetProgram((int)m_vstStream);
            HoloDebug.Assert(currentProgramIndex == 0);

            // we always pick the first program in the  C:\Users\[you]\Documents\Sugar Bytes\Turnado\Global Presets\Factory 1 folder.
            // we name ours to come first.  simplifies things greatly
            bool ok = BassVst.BASS_VST_SetProgram((int)m_vstStream, ourProgramIndex);
            BASSError error = Bass.BASS_ErrorGetCode();
            HoloDebug.Assert(ok);
            HoloDebug.Assert(error == BASSError.BASS_OK);

            int loadedProgramIndex = BassVst.BASS_VST_GetProgram((int)m_vstStream);
            // HoloDebug.Assert(loadedProgramIndex == ourProgramIndex);

            string name = BassVst.BASS_VST_GetProgramName((int)m_vstStream, 0);
            // HoloDebug.Assert(name == ourPresetName);

            /* INSANELY USEFUL, put in some kind of affordance to pop this manually:
            Action a = () => {
                BASS_VST_INFO vstInfo = new BASS_VST_INFO();
                if (BassVst.BASS_VST_GetInfo((int)m_vstStream, vstInfo) && vstInfo.hasEditor) {
                    // create a new System.Windows.Forms.Form
                    Form f = new Form();
                    f.Width = vstInfo.editorWidth + 4;
                    f.Height = vstInfo.editorHeight + 34;
                    f.Closing += (sender, e) => f_Closing(sender, e, m_vstStream);
                    f.Text = vstInfo.effectName;
                    f.Show();
                    BassVst.BASS_VST_EmbedEditor((int)m_vstStream, f.Handle);
                }
            };

            if (baseForm != null) {
                baseForm.BeginInvoke(a);
            }
             */
        }

        void f_Closing(object sender, System.ComponentModel.CancelEventArgs e, StreamHandle vstStream)
        {
            // unembed the VST editor
            BassVst.BASS_VST_EmbedEditor((int)vstStream, IntPtr.Zero);
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            SetParam(parameters, now, AutoFreeze, 0);
            SetParam(parameters, now, Kompressor, 1);
            SetParam(parameters, now, DadaismFlanger, 2);
            SetParam(parameters, now, StrangeTone, 3);
            SetParam(parameters, now, VowelFilter, 4);
            SetParam(parameters, now, SliceWarz, 5);
            SetParam(parameters, now, RingModulator, 6);
            SetParam(parameters, now, Backgroundbreak, 7);
        }

        void SetParam(ParameterMap parameters, Moment now, ParameterDescription desc, int paramIndex)
        {
            float initialValue = BassVst.BASS_VST_GetParam((int)m_vstStream, paramIndex);

            float value = parameters[desc].GetInterpolatedValue(now);
            if (Math.Abs(initialValue - value) > 0.001f) {
                bool ok = BassVst.BASS_VST_SetParam((int)m_vstStream, paramIndex, value);
                HoloDebug.Assert(ok);
                BASSError error = Bass.BASS_ErrorGetCode();
                HoloDebug.Assert(error == BASSError.BASS_OK);

                float rereadValue = BassVst.BASS_VST_GetParam((int)m_vstStream, paramIndex);
                HoloDebug.Assert(Math.Abs(rereadValue - value) < 0.001f);
            }
        }

        static TurnadoAAA1Effect()
        {
            AllEffects.Register(
                typeof(TurnadoAAA1Effect),
                (streamHandle, form) => new TurnadoAAA1Effect(streamHandle, form),
                new List<ParameterDescription>(new[] { AutoFreeze, Kompressor, DadaismFlanger, StrangeTone, VowelFilter, SliceWarz, RingModulator, Backgroundbreak }));
        }
    }
}
