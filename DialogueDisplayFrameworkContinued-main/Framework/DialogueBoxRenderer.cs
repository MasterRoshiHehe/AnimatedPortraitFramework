using DialogueDisplayFramework.Data;
using DialogueDisplayFramework.Api;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Menus;
using System;

namespace DialogueDisplayFramework.Framework
{
    public class DialogueBoxRenderer
    {
        public static void DrawDialogueBox(SpriteBatch b, DialogueBox dialogueBox, DialogueDisplayData data)
        {
            ApiConsumerManager.RaiseRenderingDialogueBox(b, dialogueBox, data);

            if (data.Dividers != null)
                foreach (var divider in data.Dividers)
                    DrawDivider(b, dialogueBox, divider);

            if (data.Images != null)
                foreach (var image in data.Images)
                    DrawImage(b, dialogueBox, image);

            DrawPortrait(b, dialogueBox, data.Portrait);
            DrawName(b, dialogueBox, data.Name);

            if (data.Texts != null)
                foreach (var textData in data.Texts)
                    DrawText(b, dialogueBox, textData);

            if (Game1.player.friendshipData.TryGetValue(dialogueBox.characterDialogue.speaker.Name, out Friendship friendship))
            {
                DrawHearts(b, dialogueBox, data.Hearts, friendship);
                DrawGifts(b, dialogueBox, data.Gifts, friendship);
                DrawJewel(b, dialogueBox, data.Jewel, friendship);
            }

            var dialogue = (data.Dialogue != null && !data.Dialogue.Disabled) ? data.Dialogue : DataHelpers.DefaultValues.Dialogue;
            DrawDialogueString(b, dialogueBox, dialogue);

            if (dialogueBox.dialogueIcon != null)
                DrawButton(b, dialogueBox, data.Button);

            ApiConsumerManager.RaiseRenderedDialogueBox(b, dialogueBox, data.GetAdapter());
        }

        public static void DrawDivider(SpriteBatch b, DialogueBox dialogueBox, DividerData divider)
        {
            ApiConsumerManager.RaiseRenderingDivider(b, dialogueBox, divider);

            if (divider?.Disabled == false)
            {
                var pos = GetDataVector(dialogueBox, divider);
                var color = Utility.StringToColor(divider.Color) ?? Color.White;

                if (divider.Horizontal)
                {
                    Texture2D texture = (divider.Color is null) ? Game1.menuTexture : Game1.uncoloredMenuTexture;
                    b.Draw(texture, new Rectangle((int)pos.X, (int)pos.Y, divider.Width, 64), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 6, -1, -1)), color);
                }
                else
                {
                    var divHeight = (divider.Height < 1) ? dialogueBox.height + 4 : divider.Height;

                    b.Draw(Game1.mouseCursors, new Rectangle((int)pos.X, (int)pos.Y, 36, divHeight), new Rectangle?(new Rectangle(278, 324, 9, 1)), color);

                    if (divider.TopConnector == true)
                        b.Draw(Game1.mouseCursors, new Vector2(pos.X, pos.Y - 20), new Rectangle?(new Rectangle(278, 313, 10, 7)), color, 0f, Vector2.Zero, divider.Scale, SpriteEffects.None, divider.LayerDepth);

                    if (divider.BottomConnector == true)
                        b.Draw(Game1.mouseCursors, new Vector2(pos.X, pos.Y + divHeight - 4), new Rectangle?(new Rectangle(278, 328, 10, 8)), color, 0f, Vector2.Zero, divider.Scale, SpriteEffects.None, divider.LayerDepth);
                }
            }

            ApiConsumerManager.RaiseRenderedDivider(b, dialogueBox, divider);
        }

        public static void DrawImage(SpriteBatch b, DialogueBox dialogueBox, ImageData image)
        {
            ApiConsumerManager.RaiseRenderingImage(b, dialogueBox, image);

            if (image?.Disabled == false)
            {
                var texture = ModEntry.ImageDict[image.TexturePath];
                var pos = GetDataVector(dialogueBox, image);

                b.Draw(texture, pos, new Rectangle(image.X, image.Y, image.W, image.H), Color.White * image.Alpha, 0, Vector2.Zero, image.Scale, SpriteEffects.None, image.LayerDepth);
            }

            ApiConsumerManager.RaiseRenderedImage(b, dialogueBox, image);
        }

        public static void DrawPortrait(SpriteBatch b, DialogueBox dialogueBox, PortraitData portrait)
        {
            ApiConsumerManager.RaiseRenderingPortrait(b, dialogueBox, portrait);

            if (portrait?.Disabled == false)
            {
                Texture2D portraitTexture = dialogueBox.characterDialogue.overridePortrait ?? dialogueBox.characterDialogue.speaker.Portrait;
                Rectangle portraitSource;

                if (portrait.TexturePath != null && !ModEntry.ImageDict[portrait.TexturePath].IsDisposed)
                {
                    portraitTexture = ModEntry.ImageDict[portrait.TexturePath];
                }

                if (portrait.X >= 0 && portrait.Y >= 0)
                {
                    portraitSource = new Rectangle(portrait.X, portrait.Y, portrait.W, portrait.H);
                }
                else
                {
                    portraitSource = Game1.getSourceRectForStandardTileSheet(portraitTexture, dialogueBox.characterDialogue.getPortraitIndex(), portrait.W, portrait.H);
                }

                if (!portraitTexture.Bounds.Contains(portraitSource))
                {
                    portraitSource.X = 0;
                    portraitSource.Y = 0;
                }

                var offset = new Vector2(DialogueBoxInterface.shouldPortraitShake.Invoke<bool>(dialogueBox.characterDialogue) ? Game1.random.Next(-1, 2) : 0, 0);

                if (!portraitTexture.IsDisposed)
                {
                    b.Draw(portraitTexture, GetDataVector(dialogueBox, portrait) + offset, new Rectangle?(portraitSource), Color.White * portrait.Alpha, 0f, Vector2.Zero, portrait.Scale, SpriteEffects.None, portrait.LayerDepth);
                }
            }

            ApiConsumerManager.RaiseRenderedPortrait(b, dialogueBox, portrait);
        }

        public static void DrawName(SpriteBatch b, DialogueBox dialogueBox, TextData name)
        {
            if (name?.Disabled == false)
            {
                name.Text = dialogueBox.characterDialogue.speaker.getName();
                name.MarkAsSpeakerDisplayName();
                DrawText(b, dialogueBox, name);
            }
        }

        public static void DrawText(SpriteBatch b, DialogueBox dialogueBox, TextData data)
        {
            ApiConsumerManager.RaiseRenderingText(b, dialogueBox, data);

            if (data?.Disabled == false)
            {
                var pos = GetDataVector(dialogueBox, data);

                if (data.Centered || data.Alignment == SpriteText.ScrollTextAlignment.Center)
                    pos.X -= SpriteText.getWidthOfString(data.PlaceholderText ?? data.Text) / 2;
                else if (data.Alignment == SpriteText.ScrollTextAlignment.Right)
                    pos.X -= SpriteText.getWidthOfString(data.PlaceholderText ?? data.Text);

                SpriteText.drawString(b, data.Text, (int)pos.X, (int)pos.Y, 999999, data.Width, 999999, data.Alpha, data.LayerDepth, data.Junimo, data.ScrollType, data.PlaceholderText ?? "", Utility.StringToColor(data.Color), data.Alignment);
            }

            ApiConsumerManager.RaiseRenderedText(b, dialogueBox, data);
        }

        public static void DrawHearts(SpriteBatch b, DialogueBox dialogueBox, HeartsData hearts, Friendship friendship)
        {
            ApiConsumerManager.RaiseRenderingHearts(b, dialogueBox, hearts);

            if (hearts?.Disabled == false)
            {
                var speaker = dialogueBox.characterDialogue.speaker;
                int friendshipLevel = Game1.player.getFriendshipLevelForNPC(speaker.Name);
                bool isRomanceLocked = speaker.datable.Value && !friendship.IsDating() && !friendship.IsMarried() && !ModEntry.SHelper.ModRegistry.IsLoaded("Cherry.PlatonicRelationships");
                int heartLevel = friendshipLevel / 250;
                int maxHearts = Utility.GetMaximumHeartsForCharacter(speaker);
                int heartsToDisplay = hearts.ShowEmptyHearts ? maxHearts + (isRomanceLocked ? 2 : 0) : heartLevel + (hearts.ShowPartialhearts && heartLevel < maxHearts ? 1 : 0);

                var heartsWidth = 7;
                var heartsHeight = 6;
                var heartsXSpacing = 1;
                var heartsYSpacing = 1;

                var pos = GetDataVector(dialogueBox, hearts);

                if (hearts.Centered)
                {
                    pos.X -= (Math.Min(heartsToDisplay, hearts.HeartsPerRow) * (heartsWidth + heartsXSpacing) - heartsXSpacing) / 2 * hearts.Scale;
                }

                var drawingPos = new Vector2(pos.X, pos.Y);
                var drawingColIndex = 0;
                var drawingRowIndex = 0;
                var remainingFriendshipLevel = friendshipLevel;

                for (int i = 0; i < heartsToDisplay; i++)
                {
                    int xSource = ((i < heartLevel) || (isRomanceLocked && i >= 8)) ? 211 : 218;
                    var color = (isRomanceLocked && i >= 8) ? (Color.Black * 0.35f) : Color.White;

                    b.Draw(Game1.mouseCursors, drawingPos, new Rectangle(xSource, 428, 7, 6), color, 0, Vector2.Zero, hearts.Scale, SpriteEffects.None, hearts.LayerDepth);

                    if (hearts.ShowPartialhearts && remainingFriendshipLevel < 250)
                    {
                        float heartFullness = remainingFriendshipLevel / 250f;
                        b.Draw(Game1.mouseCursors, drawingPos, new Rectangle(211, 428, (int)(7 * heartFullness), 6), Color.White, 0, Vector2.Zero, hearts.Scale, SpriteEffects.None, hearts.LayerDepth);
                    }

                    if (++drawingColIndex < hearts.HeartsPerRow)
                    {
                        drawingPos.X += (heartsWidth + heartsXSpacing) * hearts.Scale;
                    }
                    else
                    {
                        drawingColIndex = 0;
                        drawingRowIndex++;

                        drawingPos.X = pos.X;
                        drawingPos.Y += (heartsHeight + heartsYSpacing) * hearts.Scale;

                        if (hearts.Centered && i > (heartsToDisplay - hearts.HeartsPerRow) && heartsToDisplay % hearts.HeartsPerRow > 0)
                        {
                            drawingPos.X += ((hearts.HeartsPerRow - heartsToDisplay % hearts.HeartsPerRow) * (heartsWidth + heartsXSpacing)) / 2 * hearts.Scale;
                        }
                    }

                    remainingFriendshipLevel -= 250;
                }
            }

            ApiConsumerManager.RaiseRenderedHearts(b, dialogueBox, hearts);
        }

        public static void DrawGifts(SpriteBatch b, DialogueBox dialogueBox, GiftsData gifts, Friendship friendship)
        {
            ApiConsumerManager.RaiseRenderingGifts(b, dialogueBox, gifts);

            var speaker = dialogueBox.characterDialogue.speaker;

            if (gifts?.Disabled == false && !friendship.IsMarried() && speaker is not Child)
            {
                var pos = GetDataVector(dialogueBox, gifts);

                var giftsThisWeek = Game1.player.friendshipData[speaker.Name].GiftsThisWeek;
                var iconRealScale = gifts.IconScale * gifts.Scale;

                var iconRect = new Rectangle(166, 174, 14, 12);
                var emptyBoxRect = new Rectangle(227, 425, 9, 9);
                var checkmarkRect = new Rectangle(236, 425, 9, 9);

                if (gifts.IconScale > 0)
                {
                    var offset = Vector2.Zero;
                    if (gifts.Inline)
                        offset.Y = (iconRect.Height - iconRect.Height * gifts.IconScale) / 2f * gifts.Scale;
                    else
                        offset.X = (checkmarkRect.Width * 2 - iconRect.Width * gifts.IconScale - 1) / 2f * gifts.Scale;

                    Utility.drawWithShadow(b, Game1.mouseCursors2, pos + offset, iconRect, Color.White, 0f, Vector2.Zero, iconRealScale, false, 0.88f, 0, -1, 0.2f);
                    pos += (gifts.Inline ? new Vector2(iconRect.Width, 0) : new Vector2(0, iconRect.Height)) * iconRealScale;
                }

                // TODO: add padding option?
                pos += new Vector2(gifts.Inline ? 1 : 0, 2) * gifts.Scale;

                for (var i = 1; i >= 0; i--) // First checkmark is placed to the right
                {
                    b.Draw(Game1.mouseCursors, pos, giftsThisWeek > i ? checkmarkRect : emptyBoxRect, Color.White, 0f, Vector2.Zero, gifts.Scale, SpriteEffects.None, 0.88f);
                    pos.X += (checkmarkRect.Width - 1) * gifts.Scale;
                }
            }

            ApiConsumerManager.RaiseRenderedGifts(b, dialogueBox, gifts);
        }

        public static void DrawJewel(SpriteBatch b, DialogueBox dialogueBox, BaseData jewel, Friendship friendship)
        {
            ApiConsumerManager.RaiseRenderingJewel(b, dialogueBox, jewel);

            if (ShouldDrawFriendshipJewel(dialogueBox) && jewel?.Disabled == false)
            {
                var pos = GetDataVector(dialogueBox, jewel);
                var friendshipHeartLevel = friendship.Points / 250;
                Rectangle sourceRect;

                if (friendshipHeartLevel >= 10)
                {
                    sourceRect = new Rectangle(269, 495, 11, 11);
                }
                else
                {
                    var animationFrame = (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 1000.0 / 250.0);
                    sourceRect = new Rectangle(140 + animationFrame * 11, 532 + friendshipHeartLevel / 2 * 11, 11, 11);
                }

                b.Draw(Game1.mouseCursors, pos, sourceRect, Color.White * jewel.Alpha, 0f, Vector2.Zero, jewel.Scale, SpriteEffects.None, jewel.LayerDepth);

                dialogueBox.friendshipJewel = new Rectangle((int)pos.X, (int)pos.Y, (int)(11 * jewel.Scale), (int)(11 * jewel.Scale));
            } else
            {
                dialogueBox.friendshipJewel = Rectangle.Empty;
            }

            ApiConsumerManager.RaiseRenderedJewel(b, dialogueBox, jewel);
        }

        public static void DrawDialogueString(SpriteBatch b, DialogueBox dialogueBox, DialogueStringData dialogue)
        {
            ApiConsumerManager.RaiseRenderingDialogueString(b, dialogueBox, dialogue);

            var dialoguePos = GetDataVector(dialogueBox, dialogue);
            var width = (dialogue.Width >= 0 ? dialogue.Width : dialogueBox.width - 8) + ModEntry.Config.DialogueWidthOffset;

            DialogueBoxInterface.preventGetCurrentString = false;

            SpriteText.drawString(b, dialogueBox.getCurrentString(), (int)dialoguePos.X, (int)dialoguePos.Y, dialogueBox.characterIndexInDialogue, width, 999999, dialogue.Alpha, dialogue.LayerDepth, false, -1, "", Utility.StringToColor(dialogue.Color), dialogue.Alignment);

            DialogueBoxInterface.preventGetCurrentString = true;

            ApiConsumerManager.RaiseRenderedDialogueString(b, dialogueBox, dialogue);
        }

        public static void DrawButton(SpriteBatch b, DialogueBox dialogueBox, BaseData button)
        {
            ApiConsumerManager.RaiseRenderingButton(b, dialogueBox, button);

            if (button?.Disabled == false)
            {
                dialogueBox.dialogueIcon.position = GetDataVector(dialogueBox, button);
            }

            ApiConsumerManager.RaiseRenderedButton(b, dialogueBox, button);
        }
        public static Vector2 GetDataVector(DialogueBox box, BaseData data)
        {
            return new Vector2(box.x + (data.Right ? box.width : 0) + data.XOffset, box.y + (data.Bottom ? box.height : 0) + data.YOffset);
        }

        // Extracted from StardewValley.Menus.DialogueBox.shouldDrawFriendshipJewel v1.6.15
        // Support for SMAPI for Android
        public static bool ShouldDrawFriendshipJewel(DialogueBox dlg)
        {
            return (dlg.width >= 642 && !Game1.eventUp && !dlg.isQuestion && dlg.isPortraitBox() && !dlg.friendshipJewel.Equals(Rectangle.Empty) && dlg.characterDialogue?.speaker != null && Game1.player.friendshipData.ContainsKey(dlg.characterDialogue.speaker.Name) && dlg.characterDialogue.speaker.Name != "Henchman");
        }
    }
}
