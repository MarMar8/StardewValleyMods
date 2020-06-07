﻿using Harmony;
using static Harmony.AccessTools;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Reflection;
using xTile.Dimensions;
using System.IO;
using StardewValley.BellsAndWhistles;
using xTile.Tiles;
using System.Linq;
using xTile;

namespace MultipleSpouses
{
	public static class LocationPatches
	{
		private static IMonitor Monitor;

		// call this method from your Entry class
		public static void Initialize(IMonitor monitor)
		{
			Monitor = monitor;
		}


		public static bool GameLocation_updateMap_Prefix(ref GameLocation __instance, string ___loadedMapPath)
		{
			try
			{
				if (__instance is FarmHouse)
				{
					FarmHouse farmHouse = __instance as FarmHouse;
					if (farmHouse.owner == null)
						return true;
					bool showSpouse = ModEntry.spouses.Count > 0 || farmHouse.owner.spouse != null;
					__instance.mapPath.Value = "Maps\\" + __instance.Name + ((farmHouse.upgradeLevel == 0) ? "" : ((farmHouse.upgradeLevel == 3) ? "2" : string.Concat(farmHouse.upgradeLevel))) + (showSpouse ? "_marriage" : "");

					if (!object.Equals(__instance.mapPath.Value, ___loadedMapPath))
					{
						__instance.reloadMap();
					}
					return false;
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(GameLocation_updateMap_Prefix)}:\n{ex}", LogLevel.Error);
			}
			return true;
		}

		public static void GameLocation_resetLocalState_Postfix(GameLocation __instance)
		{
			try
			{

				if (__instance is Beach && ModEntry.config.BuyPendantsAnytime)
				{
					ModEntry.PHelper.Reflection.GetField<NPC>(__instance, "oldMariner").SetValue(new NPC(new AnimatedSprite("Characters\\Mariner", 0, 16, 32), new Vector2(80f, 5f) * 64f, 2, "Old Mariner", null));
					return;
				}

				if (!(__instance is FarmHouse) || __instance != Utility.getHomeOfFarmer(Game1.player))
				{
					return;
				}
				ModEntry.PMonitor.Log("reset farm state");

				FarmHouse farmHouse = __instance as FarmHouse;

				Farmer f = farmHouse.owner;
				ModEntry.ResetSpouses(f);

				if (!ModEntry.config.BuildAllSpousesRooms)
				{
					return;
				}


				if (ModEntry.spouses.ContainsKey("Emily") && f.spouse != "Emily" && Game1.player.eventsSeen.Contains(463391))
				{
					int offset = (ModEntry.spouses.Keys.ToList().IndexOf("Emily") + 1) * 7 * 64;
					Vector2 parrotSpot = new Vector2(2064f + offset, 160f);
					int upgradeLevel = farmHouse.upgradeLevel;
					if (upgradeLevel - 2 <= 1)
					{
						parrotSpot = new Vector2(2448f + offset, 736f);
					}
					farmHouse.temporarySprites.Add(new EmilysParrot(parrotSpot));
				}

				List<NPC> mySpouses = new List<NPC>();

				foreach (NPC spouse in ModEntry.spouses.Values)
				{
					string name = spouse.Name;
					if (name == "Victor" || name == "Olivia" || name == "Sophia")
					{
						if (ModEntry.PHelper.Content.Load<Map>($"../[TMX] Stardew Valley Expanded/assets/{name}sRoom.tmx", ContentSource.ModFolder) == null && ModEntry.PHelper.Content.Load<Map>($"../../[TMX] Stardew Valley Expanded/assets/{name}sRoom.tmx", ContentSource.ModFolder) == null)
						{
							ModEntry.PMonitor.Log($"Couldn't load spouse room for SVE spouse {name}. Check and make sure it is located at ../[TMX] Stardew Valley Expanded/assets/{name}sRoom.tmx or ../../[TMX] Stardew Valley Expanded/assets/{name}sRoom.tmx", LogLevel.Error);
							continue;
						}
					}
					mySpouses.Add(spouse);
				}

				if (farmHouse.upgradeLevel > 3 || mySpouses.Count == 0)
				{
					return;
				}

				int untitled = 0;
				for (int i = 0; i < farmHouse.map.TileSheets.Count; i++)
				{
					if (farmHouse.map.TileSheets[i].Id == "untitled tile sheet")
						untitled = i;
				}

				int ox = 0;
				int oy = 0;
				if (farmHouse.upgradeLevel > 1)
				{
					ox = 6;
					oy = 9;
				}

				for (int i = 0; i < 7; i++)
				{
					farmHouse.setMapTileIndex(ox + 29 + i, oy + 11, 0, "Buildings", 0);
					farmHouse.removeTile(ox + 29 + i, oy + 9, "Front");
					farmHouse.removeTile(ox + 29 + i, oy + 10, "Buildings");
					farmHouse.setMapTileIndex(ox + 28 + i, oy + 10, 165, "Front", 0);
					farmHouse.removeTile(ox + 29 + i, oy + 10, "Back");
				}
				for (int i = 0; i < 8; i++)
				{
					farmHouse.setMapTileIndex(ox + 28 + i, oy + 10, 165, "Front", 0);
				}
				for (int i = 0; i < 10; i++)
				{
					farmHouse.removeTile(ox + 35, oy + 0 + i, "Buildings");
					farmHouse.removeTile(ox + 35, oy + 0 + i, "Front");
				}
				for (int i = 0; i < 3; i++)
				{
					farmHouse.setMapTileIndex(ox + 29 + (i * 2 + 1), oy + 10, ModEntry.config.HallTileOdd, "Back", 0);
					farmHouse.setMapTileIndex(ox + 29 + (i * 2), oy + 10, ModEntry.config.HallTileEven, "Back", 0);
				}
				farmHouse.setMapTileIndex(ox + 35, oy + 10, ModEntry.config.HallTileOdd, "Back", 0);

				farmHouse.removeTile(ox + 28, oy + 9, "Front");
				farmHouse.removeTile(ox + 28, oy + 10, "Buildings");
				farmHouse.setMapTileIndex(ox + 28, oy + 10, 163, "Front", 0);
				farmHouse.removeTile(ox + 35, oy + 0, "Front");
				farmHouse.removeTile(ox + 35, oy + 0, "Buildings");

				for (int i = 0; i < 6; i++)
				{
					farmHouse.setMapTileIndex(ox + 28, oy + 1 + i, 99, "Buildings", untitled);
				}

				farmHouse.setMapTileIndex(ox + 28, oy + 7, 111, "Buildings", untitled);
				farmHouse.setMapTileIndex(ox + 28, oy + 8, 123, "Buildings", untitled);
				farmHouse.setMapTileIndex(ox + 28, oy + 9, 135, "Buildings", untitled);
				farmHouse.setMapTileIndex(ox + 28, oy + 9, 54, "Back", untitled);

				farmHouse.removeTile(ox + 28, oy + 10, "Back");
				farmHouse.setMapTileIndex(ox + 28, oy + 10, ModEntry.config.HallTileOdd, "Back", 0);


				int count = 0;

				if (f.isMarried() && f.spouse == "Victor" || f.spouse == "Olivia" || f.spouse == "Sophia")
				{
					ModEntry.BuildSpouseRoom(farmHouse, f.spouse, -1);
				}


				for (int j = 0; j < mySpouses.Count; j++)
				{
					farmHouse.removeTile(ox + 35 + (7 * count), oy + 0, "Buildings");
					for (int i = 0; i < 10; i++)
					{
						farmHouse.removeTile(ox + 35 + (7 * count), oy + 1 + i, "Buildings");
					}
					ModEntry.BuildSpouseRoom(farmHouse, mySpouses[j].Name, count++);
				}


				farmHouse.setMapTileIndex(ox + 35 + (7 * count), oy + 0, 11, "Buildings", 0);
				for (int i = 0; i < 10; i++)
				{
					farmHouse.setMapTileIndex(ox + 35 + (7 * count), oy + 1 + i, 68, "Buildings", 0);
				}
				farmHouse.setMapTileIndex(ox + 35 + (7 * count), oy + 10, 130, "Front", 0);
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(GameLocation_resetLocalState_Postfix)}:\n{ex}", LogLevel.Error);
			}

		}

		public static void Farm_addSpouseOutdoorArea_Prefix(ref string spouseName)
		{
			try
			{
				ModEntry.PMonitor.Log($"Checking for outdoor spouse to change area");
				if (ModEntry.outdoorSpouse != null && spouseName != "")
				{
					spouseName = ModEntry.outdoorSpouse;
					ModEntry.PMonitor.Log($"Setting outdoor spouse area for {spouseName}");
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(Farm_addSpouseOutdoorArea_Prefix)}:\n{ex}", LogLevel.Error);
			}

		}


		public static bool Beach_checkAction_Prefix(Beach __instance, Location tileLocation, xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result, NPC ___oldMariner)
		{
			try
			{
				if (___oldMariner != null && ___oldMariner.getTileX() == tileLocation.X && ___oldMariner.getTileY() == tileLocation.Y)
				{
					string playerTerm = Game1.content.LoadString("Strings\\Locations:Beach_Mariner_Player_" + (who.IsMale ? "Male" : "Female"));
					if (who.specialItems.Contains(460) && !Utility.doesItemWithThisIndexExistAnywhere(460, false))
					{
						for (int i = who.specialItems.Count - 1; i >= 0; i--)
						{
							if (who.specialItems[i] == 460)
							{
								who.specialItems.RemoveAt(i);
							}
						}
					}
					if (who.specialItems.Contains(460))
					{
						Game1.drawObjectDialogue(Game1.parseText(Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerHasItem", playerTerm)));
					}
					else if (who.hasAFriendWithHeartLevel(10, true) && who.houseUpgradeLevel == 0)
					{
						Game1.drawObjectDialogue(Game1.parseText(Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerNotUpgradedHouse", playerTerm)));
					}
					else if (who.hasAFriendWithHeartLevel(10, true))
					{
						Response[] answers = new Response[]
						{
					new Response("Buy", Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerBuyItem_AnswerYes")),
					new Response("Not", Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerBuyItem_AnswerNo"))
						};
						__instance.createQuestionDialogue(Game1.parseText(Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerBuyItem_Question", playerTerm)), answers, "mariner");
					}
					else
					{
						Game1.drawObjectDialogue(Game1.parseText(Game1.content.LoadString("Strings\\Locations:Beach_Mariner_PlayerNoRelationship", playerTerm)));
					}
					__result = true;
					return false;
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(Beach_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
			}
			return true;
		}
		public static bool ManorHouse_performAction_Prefix(ManorHouse __instance, string action, Farmer who, ref bool __result)
		{
			try
			{
				ModEntry.ResetSpouses(who);
				if (action != null && who.IsLocalPlayer && (Game1.player.isMarried() || ModEntry.spouses.Count > 0))
				{
					string a = action.Split(new char[]
					{
					' '
					})[0];
					if (a == "DivorceBook")
					{
						string s2 = Game1.content.LoadStringReturnNullIfNotFound("Strings\\Locations:ManorHouse_DivorceBook_Question_" + Game1.player.spouse);
						if (s2 == null)
						{
							s2 = Game1.content.LoadStringReturnNullIfNotFound("Strings\\Locations:ManorHouse_DivorceBook_Question");
						}
						List<Response> responses = new List<Response>();
						if(who.spouse != null)
							responses.Add(new Response(who.spouse, who.spouse));
						foreach (string spouse in ModEntry.spouses.Keys)
						{
							responses.Add(new Response(spouse, spouse));
						}
						responses.Add(new Response("No", Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_No")));
						__instance.createQuestionDialogue(s2, responses.ToArray(), "divorce");
					}
					__result = true;
					return false;
				}
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed in {nameof(ManorHouse_performAction_Prefix)}:\n{ex}", LogLevel.Error);
			}
			return true;
		}
	}
}