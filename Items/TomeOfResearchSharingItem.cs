using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace TomeOfResearchSharing.Items
{
	public class TomeOfResearchSharingItem : ModItem
	{
		private ResearchData data = new ResearchData();
		private string playerName = string.Empty;

		private bool Empty => playerName == string.Empty;

		public override ModItem Clone(Item item)
		{
			var clone = (TomeOfResearchSharingItem)base.Clone(item);
			clone.data = new ResearchData(this.data); //Decouple reference
			return clone;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["data"] = data;
			tag["playerName"] = playerName;
		}

		public override void LoadData(TagCompound tag)
		{
			data = tag.Get<ResearchData>("data");
			playerName = tag.GetString("playerName");
		}

		public override void NetSend(BinaryWriter writer)
		{
			data.NetSend(writer);
			writer.Write(playerName);
		}

		public override void NetReceive(BinaryReader reader)
		{
			data = new ResearchData();
			data.NetReceive(reader);
			playerName = reader.ReadString();
		}

		public override LocalizedText Tooltip => LocalizedText.Empty;

		public static LocalizedText TransferText { get; private set; }
		public static LocalizedText ApplyEmptyText { get; private set; }
		public static LocalizedText ApplyPopulatedText { get; private set; }
		public static LocalizedText FromPlayerText { get; private set; }
		public static LocalizedText ResearchProgressText { get; private set; }
		public static LocalizedText UnloadedProgressText { get; private set; }

		public static LocalizedText NotJourneyModeText { get; private set; }
		public static LocalizedText StoredText { get; private set; }
		public static LocalizedText AppliedText { get; private set; }

		public override void SetStaticDefaults()
		{
			TransferText = this.GetLocalization("Tooltips.Transfer");
			ApplyEmptyText = this.GetLocalization("Tooltips.ApplyEmpty");
			ApplyPopulatedText = this.GetLocalization("Tooltips.ApplyPopulated");
			FromPlayerText = this.GetLocalization("Tooltips.FromPlayer");
			ResearchProgressText = this.GetLocalization("Tooltips.ResearchProgress");
			UnloadedProgressText = this.GetLocalization("Tooltips.UnloadedProgress");

			NotJourneyModeText = this.GetLocalization("Status.NotJourneyMode");
			StoredText = this.GetLocalization("Status.Stored");
			AppliedText = this.GetLocalization("Status.Applied");
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.value = Item.sellPrice(silver: 3);
			Item.rare = ItemRarityID.Pink;
			Item.useAnimation = 40;
			Item.useTime = 40;
			Item.useStyle = ItemUseStyleID.RaiseLamp;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			if (Empty)
			{
				tooltips.Add(new TooltipLine(Mod, "Transfer", TransferText.ToString()));
				tooltips.Add(new TooltipLine(Mod, "Apply", ApplyEmptyText.ToString()));
			}

			if (!Empty)
			{
				tooltips.Add(new TooltipLine(Mod, "Apply", ApplyPopulatedText.ToString()));

				tooltips.Add(new TooltipLine(Mod, "PlayerName", FromPlayerText.Format(Color.Orange.Hex3(), playerName)));
				tooltips.Add(new TooltipLine(Mod, "ResearchProgress", ResearchProgressText.Format(data.ActiveCount, CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Count - TomeOfResearchSharing.vanillaDeprecated.Count)));

				var unloadedItems = data.GetUnloadedIDs().ToList();

				if (unloadedItems.Count > 0)
				{
					//Count each list separately
					int count = 0;
					foreach (var kvp in unloadedItems)
					{
						count += kvp.Value.Count;
					}

					float pulse = Main.mouseTextColor / 255f;
					Color color = Color.LightGray * pulse;
					//color.A = Main.mouseTextColor;
					tooltips.Add(new TooltipLine(Mod, "UnloadedProgress", UnloadedProgressText.Format(count))
					{
						OverrideColor = color
					});
				}
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe(1)
				.AddIngredient(ItemID.Book)
				.AddTile(TileID.Bookcases)
				.AddCondition(Condition.InJourneyMode)
				.Register();
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
			{
				return true;
			}

			if (Main.GameMode != GameModeID.Creative)
			{
				Main.NewText(NotJourneyModeText.ToString());
				return true;
			}

			if (Empty)
			{
				playerName = player.name;

				//Set data
				Main.NewText(StoredText.ToString());
				SoundEngine.PlaySound(SoundID.Research);

				List<int> researchedItems = new List<int>();
				Main.LocalPlayerCreativeTracker.ItemSacrifices.FillListOfItemsThatCanBeObtainedInfinitely(researchedItems);
				var itemIDs = researchedItems.ToHashSet();

				//Get unloaded items, reflection
				if (!TryGetUnloadedResearch(player, out IList<TagCompound> unloadedResearch))
				{
					//Do not carry over unloaded research
					data = new ResearchData(itemIDs);
					return true;
				}

				var unloadedIDs = new Dictionary<string, List<NameCountPair>>();
				foreach (var tag in unloadedResearch)
				{
					string modName = tag.GetString("mod");
					string modItemName = tag.GetString("name");
					int sacrificeCount = tag.GetInt("sacrificeCount"); //Need to check that in loaddata recover

					if (!unloadedIDs.ContainsKey(modName))
					{
						unloadedIDs[modName] = new List<NameCountPair>();
					}

					List<NameCountPair> lists = unloadedIDs[modName];
					var pair = new NameCountPair(modItemName, sacrificeCount);
					if (!lists.Contains(pair))
					{
						lists.Add(pair);
					}
				}

				data = new ResearchData(itemIDs, unloadedIDs);
			}
			else
			{
				Main.NewText(AppliedText.ToString());
				SoundEngine.PlaySound(SoundID.ResearchComplete);

				//var researchedItems = new HashSet<int>();
				//Vanilla (+ this instance of item use modded items): Guaranteed researchability, research limits didn't change
				foreach (var type in data.GetItemIDs())
				{
					//researchedItems.Add(type);
					CreativeUI.ResearchItem(type);
				}

				//Modded (check if pending stack + current stack would research item, then research it)
				foreach (var pair in data.GetPendingResearchableModdedIDs())
				{
					int type = pair.Key;
					//researchedItems.Add(type);
					int count = pair.Value;

					int? remaining = CreativeUI.GetSacrificesRemaining(type);

					if (remaining.HasValue && (count == TomeOfResearchSharing.FullyResearchedCount || remaining.Value <= count))
					{
						CreativeUI.ResearchItem(type);
					}
				}

				//var missingItems = new HashSet<int>();
				//HashSet<int> allItems = CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Keys.ToHashSet();
				//allItems.ExceptWith(researchedItems);

				//foreach (var item in allItems)
				//{
				//	if (ItemLoader.GetItem(item) is ModItem modItem)
				//	{
				//		int a = modItem.Type;
				//	}
				//}

				//Add stored unloaded research so that tml auto-restores it
				if (!TryGetUnloadedResearch(player, out IList<TagCompound> unloadedResearch))
				{
					return true;
				}

				foreach (var pair in data.GetUnloadedIDs())
				{
					string modName = pair.Key;
					foreach (var pair2 in pair.Value)
					{
						string name = pair2.Name;
						int count = pair2.Count;
						if (count == TomeOfResearchSharing.FullyResearchedCount)
						{
							//For outward interactions, fall back on vanilla variant
							count = ItemsSacrificedUnlocksTracker.POSITIVE_SACRIFICE_COUNT_CAP;
						}

						TagCompound tagCompound = unloadedResearch.FirstOrDefault((TagCompound tag) => tag.GetString("mod") == modName && tag.GetString("name") == name);
						if (tagCompound == null)
						{
							//Make new entry
							unloadedResearch.Add(new TagCompound
							{
								{"mod", modName },
								{"name", name },
								{"sacrificeCount", count },
							});
						}
						else
						{
							//Overwrite new entry with higher count
							if (tagCompound.GetInt("sacrificeCount") < count)
							{
								tagCompound["sacrificeCount"] = count;
							}
						}
					}
				}
			}

			return true;
		}

		private bool TryGetUnloadedResearch(Player player, out IList<TagCompound> unloadedResearch)
		{
			unloadedResearch = null;
			if (!Config.Instance.TransferUnloadedData)
			{
				return false;
			}

			//get unloaded items, reflection
			var unloadedPlayer = player.GetModPlayer<UnloadedPlayer>();

			//public class UnloadedPlayer : ModPlayer -> internal IList<TagCompound> unloadedResearch;
			FieldInfo info = typeof(UnloadedPlayer).GetField("unloadedResearch", BindingFlags.Instance | BindingFlags.NonPublic);

			object unloadedResearchObj = info.GetValue(unloadedPlayer);
			if (unloadedResearchObj is IList<TagCompound> dummy)
			{
				unloadedResearch = dummy;
			}

			return unloadedResearch != null;
		}
	}
}
