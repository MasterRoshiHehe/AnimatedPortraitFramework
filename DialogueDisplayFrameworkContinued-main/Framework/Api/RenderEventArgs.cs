using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System;

namespace DialogueDisplayFramework.Api
{
    public class RenderEventArgs<T> : EventArgs, IRenderEventArgs<T>
    {
        public RenderEventArgs(SpriteBatch spriteBatch, DialogueBox dialogueBox, T data)
        {
            SpriteBatch = spriteBatch;
            DialogueBox = dialogueBox;
            Data = data;
        }

        public SpriteBatch SpriteBatch { get; }

        public DialogueBox DialogueBox { get; }

        public T Data { get; }
    }
}
