////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    public enum TextSpriteNodeState
    {
        Default, // plain old unhlighlighted
        Disabled, // can't be highlighted
        Highlighted, // is highlighted
    }

    /// <summary>A node class that displays white text over a semi-transparent black circle, all centered.</summary>
    public class TextSpriteNode : GroupNode
    {
        TextNode m_textNode;
        SpriteNode m_spriteNode;
        SpriteNode m_highlightSpriteNode;

        public TextSpriteNode(
            AParentSceneNode parent,
            Transform localTransform,
            string label,
            Texture2D background,
            Texture2D highlight)
            : base(parent, localTransform, label)
        {
            m_spriteNode = new SpriteNode(this, label + "_sprite", background);
            m_spriteNode.Origin = new Vector2(0.5f);

            m_textNode = new TextNode(this, label + "_text");
            m_textNode.Alignment = Alignment.Centered;
            m_textNode.Text.Append(label);
            m_textNode.LocalTransform = new Transform(Vector2.Zero, new Vector2(0.7f));

            m_highlightSpriteNode = new SpriteNode(this, label + "_highlight", highlight);
            m_highlightSpriteNode.Origin = new Vector2(0.5f);

            SetState(TextSpriteNodeState.Default);
        }

        public void SetState(TextSpriteNodeState state)
        {
            switch (state) {
                case TextSpriteNodeState.Default:
                    m_textNode.Color = Color.White;
                    m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                    m_highlightSpriteNode.Color = new Color(0);
                    break;

                case TextSpriteNodeState.Disabled:
                    // dim text, no highlight
                    m_textNode.Color = new Color((byte)0x40, (byte)0x40, (byte)0x40, (byte)0x40);
                    m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                    m_highlightSpriteNode.Color = new Color(0);
                    break;

                case TextSpriteNodeState.Highlighted:
                    // dim text, no highlight
                    m_textNode.Color = Color.White;
                    m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                    m_highlightSpriteNode.Color = Color.White;
                    break;
            }

        }  
    }
}

