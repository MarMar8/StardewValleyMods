﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MobilePhone
{
    public class MobilePhoneApp
    {
        private static IModHelper Helper;
        private static IMonitor Monitor;
        private static ModConfig Config;
        private static Texture2D appIcon;
        private static List<CallableNPC> callableList = new List<CallableNPC>();
        private static int topRow = 0;
        private static bool dragging = true;
        private static int yOffset;
        private static int lastMousePositionY;
        private static float listHeight;

        public static void Initialize(IModHelper helper, IMonitor monitor, ModConfig config)
        {
            Helper = helper;
            Monitor = monitor;
            Config = config;
            appIcon = Helper.Content.Load<Texture2D>(Path.Combine("assets","app_icon.png"));
            ModEntry.apps.Add("aedenthorn.MobilePhone", GetApp());
        }

        private static MobileApp GetApp()
        {
            return new MobileApp("Mobile Phone", OpenPhoneBook, appIcon);
        }

        private static void OpenPhoneBook()
        {
            topRow = 0;
            ModEntry.appRunning = true;
            ModEntry.phoneAppRunning = true;
            ModEntry.runningApp = Helper.ModRegistry.ModID;
            Game1.activeClickableMenu = new PhoneBookMenu();
            CreateCallableList();
            listHeight = Config.ContactMarginY + (int)Math.Ceiling(callableList.Count / (float)ModEntry.gridWidth) * (Config.ContactHeight + Config.ContactMarginY);
            Helper.Events.Display.RenderingActiveMenu += Display_RenderingActiveMenu;
            Helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            Helper.Events.Input.ButtonReleased += Input_ButtonReleased;
        }

        private static void Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            if (!ModEntry.appRunning)
                return;
            if (e.Button == SButton.MouseLeft)
            {
                if (!ModEntry.phoneRect.Contains(Game1.getMousePosition()))
                {
                    Helper.Input.Suppress(SButton.MouseLeft);
                    ModEntry.TogglePhone();
                    return;
                }

                lastMousePositionY = Game1.getMouseY();
                Monitor.Log($"y {lastMousePositionY}");
            }
        }

        private static void Input_ButtonReleased(object sender, StardewModdingAPI.Events.ButtonReleasedEventArgs e)
        {
            if (e.Button == SButton.MouseLeft)
            {
                Monitor.Log($"unclicked toprow {topRow} callables {callableList.Count} width {ModEntry.gridWidth} tiles {ModEntry.gridWidth * ModEntry.gridHeight}");
                if (dragging)
                {
                    Monitor.Log($"was dragging");
                    dragging = false;
                    return;
                }

                for (int i = 0; i < callableList.Count; i++)
                {
                    Vector2 pos = GetNPCPos(i);
                    Rectangle r = new Rectangle((int)pos.X, (int)pos.Y, Config.ContactWidth, Config.ContactHeight);
                    if (r.Contains(Game1.getMousePosition()))
                    {
                        Monitor.Log($"calling {callableList[i].npc.Name}");
                        CallNPC(callableList[i].npc);
                        Helper.Events.Input.ButtonPressed -= Input_ButtonPressed;
                        Helper.Events.Input.ButtonReleased -= Input_ButtonReleased;
                        dragging = true;
                        return;
                    }
                }
            }
        }

        private static void CallNPC(NPC npc)
        {
            if (npc.CurrentDialogue.Count >= 1 || npc.endOfRouteMessage.Value != null)
            {
                Monitor.Log($"{npc.Name} has dialogue");
                npc.grantConversationFriendship(Game1.player, 20);
                Game1.drawDialogue(npc);
            }
            else
            {
                Monitor.Log($"{npc.Name} has no dialogue");
                Game1.drawObjectDialogue(Helper.Translation.Get("no-answer"));
            }
        }

        private static void CreateCallableList()
        {
            callableList.Clear();
            foreach(KeyValuePair<string,Netcode.NetRef<Friendship>> kvp in Game1.player.friendshipData.FieldDict)
            {
                if(kvp.Value.Value.Points >= Config.MinPointsToCall)
                {
                    NPC npc = Game1.getCharacterFromName(kvp.Key);
                    Texture2D portrait = npc.Sprite.Texture;
                    Rectangle sourceRect = npc.getMugShotSourceRect();
                    string name = Config.UseRealNamesInPhoneBook && npc.displayName != null ? npc.displayName : npc.Name;
                    callableList.Add(new CallableNPC(name, npc, portrait, sourceRect));
                }
            }
            callableList = callableList.OrderBy(a => a.npc.Name).ToList();
        }

        private static void Display_RenderingActiveMenu(object sender, StardewModdingAPI.Events.RenderingActiveMenuEventArgs e)
        {

            if (Helper.Input.IsDown(SButton.MouseLeft) && ModEntry.screenRect.Contains(Game1.getMousePosition()))
            {
                int dy = Game1.getMouseY() - lastMousePositionY;
                if (Math.Abs(dy) > 0)
                {
                    dragging = true;
                }
                if (dragging)
                {
                    yOffset = (int)Math.Max(Math.Min(0, yOffset + dy), -1 * Math.Max(0, listHeight - ModEntry.GetScreenSize().Y));
                }
            }

            lastMousePositionY = Game1.getMouseY();

            if (!ModEntry.appRunning || !ModEntry.phoneOpen || !(Game1.activeClickableMenu is PhoneBookMenu))
            {
                ModEntry.appRunning = false;
                ModEntry.phoneAppRunning = false;
                Helper.Events.Display.RenderingActiveMenu -= Display_RenderingActiveMenu;
                Helper.Events.Input.ButtonPressed -= Input_ButtonPressed;
                Helper.Events.Input.ButtonReleased -= Input_ButtonReleased;
                return;
            }
            e.SpriteBatch.Draw(ModEntry.phoneBookTexture, ModEntry.screenPosition, Color.White);

            if(yOffset < 0)
            {
                e.SpriteBatch.Draw(ModEntry.upArrowTexture, ModEntry.upArrowPosition, Color.White);
            }
            if (yOffset > ModEntry.GetScreenSize().Y - listHeight)
            {
                e.SpriteBatch.Draw(ModEntry.downArrowTexture, ModEntry.downArrowPosition, Color.White);
            }

            int screenBottom = (int)(ModEntry.screenPosition.Y + ModEntry.GetScreenSize().Y);
            for (int i = 0; i < callableList.Count; i++)
            {
                Vector2 npcPos = GetNPCPos(i);
                Rectangle r = callableList[i].sourceRect;
                if (npcPos.Y < ModEntry.screenPosition.Y - r.Height * 2 || npcPos.Y >= screenBottom)
                {
                    continue;
                }
                Rectangle sourceRect = r;
                int cutTop = 0;
                int cutBottom = 0;
                if(npcPos.Y < ModEntry.screenPosition.Y)
                {
                    cutTop = (int)Math.Round((ModEntry.screenPosition.Y - (int)npcPos.Y) / 2f);
                    sourceRect = new Rectangle(r.X, r.Y + cutTop, r.Width, r.Height - cutTop);
                    npcPos = new Vector2(npcPos.X, ModEntry.screenPosition.Y);
                }
                else if(npcPos.Y > screenBottom - r.Height * 2)
                {
                    cutBottom = (int)Math.Round((screenBottom - r.Height * 2 - (int)npcPos.Y) / 2f);
                    sourceRect = new Rectangle(r.X, r.Y, r.Width, r.Height + cutBottom);
                }

                e.SpriteBatch.Draw(callableList[i].portrait, npcPos + new Vector2((Config.ContactWidth - 32) / 2,0), sourceRect, Color.White, 0, Vector2.Zero, 2, SpriteEffects.None, 0.86f);
                if(Config.ShowNamesInPhoneBook && npcPos.Y < screenBottom - Config.ContactHeight - callableList[i].nameSize.Y * 0.4f + 6)
                    e.SpriteBatch.DrawString(Game1.dialogueFont, callableList[i].name, GetNPCPos(i) + new Vector2(Config.ContactWidth / 2 - callableList[i].nameSize.X * 0.2f, Config.ContactHeight - 6 ), Color.Black, 0, Vector2.Zero, 0.4f, SpriteEffects.None, 0.86f);
            }
        }

        private static Vector2 GetNPCPos(int i)
        {
            i -= topRow * ModEntry.gridWidth;
            float x = ModEntry.screenPosition.X + Config.ContactMarginX + ((i % ModEntry.gridWidth) * (Config.ContactWidth + Config.ContactMarginX));
            float y = ModEntry.screenPosition.Y + Config.ContactMarginY + ((i / ModEntry.gridWidth) * (Config.ContactHeight + Config.ContactMarginY));

            return new Vector2(x, y + yOffset);
        }

        public static Texture2D MakeBackground()
        {
            Vector2 screenSize = ModEntry.GetScreenSize();
            Texture2D phoneBook = new Texture2D(Game1.graphics.GraphicsDevice, (int)screenSize.X, (int)screenSize.Y);
            Color[] data = new Color[phoneBook.Width * phoneBook.Height];
            for (int pixel = 0; pixel < data.Length; pixel++)
            {
                data[pixel] = Config.PhoneBookBackgroundColor;
            }
            phoneBook.SetData(data);
            return phoneBook;
        }
    }
}