using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TomeOfResearchSharing
{
	public class ResearchData : TagSerializable
	{
		public static readonly Func<TagCompound, ResearchData> DESERIALIZER = Load;

		private HashSet<int> itemIDs; //Contains all currently loaded IDs when used, otherwise vanilla IDs

		private Dictionary<string, List<NameCountPair>> unloadedIDs; //Contains current unloaded items

		private Dictionary<int, int> pendingResearchableModdedIDs; //Now loaded (previously unloaded) items that are checked for researchability separately

		public ResearchData(HashSet<int> itemIDs = null, Dictionary<string, List<NameCountPair>> unloadedIDs = null, Dictionary<int, int> pendingResearchableModdedIDs = null)
		{
			this.itemIDs = itemIDs ?? new HashSet<int>();
			this.unloadedIDs = unloadedIDs ?? new Dictionary<string, List<NameCountPair>>();
			this.pendingResearchableModdedIDs = pendingResearchableModdedIDs ?? new Dictionary<int, int>();
		}

		//Copy constructor
		public ResearchData(ResearchData other)
		{
			itemIDs = new HashSet<int>(other.itemIDs);

			unloadedIDs = new Dictionary<string, List<NameCountPair>>();
			foreach (var pair in other.unloadedIDs)
			{
				unloadedIDs[pair.Key] = new List<NameCountPair>(pair.Value);
			}

			pendingResearchableModdedIDs = new Dictionary<int, int>();
			foreach (var pair in other.pendingResearchableModdedIDs)
			{
				pendingResearchableModdedIDs[pair.Key] = pair.Value;
			}
		}

		public int ActiveCount => itemIDs.Count + pendingResearchableModdedIDs.Count;

		public IEnumerable<int> GetItemIDs()
		{
			foreach (var item in itemIDs)
			{
				yield return item;
			}
		}

		public IEnumerable<KeyValuePair<int, int>> GetPendingResearchableModdedIDs()
		{
			foreach (var item in pendingResearchableModdedIDs)
			{
				yield return item;
			}
		}

		public IEnumerable<KeyValuePair<string, List<NameCountPair>>> GetUnloadedIDs()
		{
			foreach (var item in unloadedIDs)
			{
				yield return item;
			}
		}

		public TagCompound SerializeData()
		{
			var tag = new TagCompound();

			tag.Add("vanillaIDs", itemIDs.Where(x => x < ItemID.Count).ToList());

			var tempModdedItemIDs = new Dictionary<string, List<NameCountPair>>();

			//Save unloaded modded IDs
			foreach (var pair in unloadedIDs)
			{
				tempModdedItemIDs.Add(pair.Key, pair.Value);
			}

			//Save modded IDs
			var tempTempModdedItemIDs = new Dictionary<int, int>();
			foreach (var type in itemIDs.Where(x => x >= ItemID.Count)) //Modded items during current session
			{
				tempTempModdedItemIDs.Add(type, TomeOfResearchSharing.FullyResearchedCount);
			}

			foreach (var pair in pendingResearchableModdedIDs) //Modded items carried over
			{
				tempTempModdedItemIDs.Add(pair.Key, pair.Value);
			}

			foreach (var intPair in tempTempModdedItemIDs)
			{
				ModItem modItem = ItemLoader.GetItem(intPair.Key);
				string modName = modItem.Mod.Name;
				string name = modItem.Name;
				if (!tempModdedItemIDs.ContainsKey(modName))
				{
					tempModdedItemIDs[modName] = new List<NameCountPair>();
				}

				List<NameCountPair> lists = tempModdedItemIDs[modName];
				var namePair = new NameCountPair(name, intPair.Value);
				if (!lists.Contains(namePair))
				{
					lists.Add(namePair);
				}
			}

			TagCompound moddedIDsTag = new TagCompound();
			foreach (var pair in tempModdedItemIDs)
			{
				moddedIDsTag.Add(pair.Key, pair.Value);
			}

			tag.Add("moddedIDs", moddedIDsTag);

			return tag;
		}

		public static ResearchData Load(TagCompound tag)
		{
			var vanillaItemIDs = tag.GetList<int>("vanillaIDs").ToHashSet();

			var moddedItemIDsTag = tag.Get<TagCompound>("moddedIDs");

			var pendingResearchableModdedIDs = new Dictionary<int, int>();

			var unloadedIDs = new Dictionary<string, List<NameCountPair>>();

			foreach (var modTag in moddedItemIDsTag)
			{
				string modName = modTag.Key;
				var list = moddedItemIDsTag.GetList<NameCountPair>(modName).ToList(); //'Value as' casting doensn't work here fsr

				if (!ModLoader.TryGetMod(modName, out _))
				{
					if (!unloadedIDs.ContainsKey(modName))
					{
						//If mod is not currently loaded, all its items are also not loaded, so save the entire list, and continue
						unloadedIDs[modName] = list.ToList();
					}
					continue;
				}

				foreach (var pair in list)
				{
					if (ModContent.TryFind(modName, pair.Name, out ModItem modItem))
					{
						pendingResearchableModdedIDs.Add(modItem.Type, pair.Count);
					}
					else
					{
						//If loaded item does not exist, add it as unloaded
						if (!unloadedIDs.ContainsKey(modName))
						{
							unloadedIDs[modName] = new List<NameCountPair>();
						}

						List<NameCountPair> lists = unloadedIDs[modName];
						if (!lists.Contains(pair))
						{
							lists.Add(pair);
						}
					}
				}
			}

			return new ResearchData(vanillaItemIDs, unloadedIDs, pendingResearchableModdedIDs);
		}

		public void NetSend(BinaryWriter writer)
		{
			//log4net.ILog logger = ModContent.GetInstance<TomeOfResearchSharing>().Logger;
			//long pos = writer.BaseStream.Position;

			try
			{
				//optimization: send the smaller of the two sets (50% < or > researched)
				int count = itemIDs.Count;
				bool reversed = count > CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Count / 2;
				bool empty = (itemIDs.Count + unloadedIDs.Count + pendingResearchableModdedIDs.Count) == 0;

				BitsByte header = 0;
				header[0] = reversed;
				header[1] = empty;
				writer.Write(header);

				if (empty)
				{
					return;
				}

				if (reversed)
				{
					var missingIDs = new HashSet<int>();
					foreach (var item in CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Keys)
					{
						if (!itemIDs.Contains(item))
						{
							missingIDs.Add(item);
						}
					}

					writer.Write7BitEncodedInt(missingIDs.Count);
					foreach (var type in missingIDs)
					{
						writer.Write7BitEncodedInt(type);
					}
				}
				else
				{
					writer.Write7BitEncodedInt(count);
					foreach (var type in itemIDs)
					{
						writer.Write7BitEncodedInt(type);
					}
				}

				//TODO optimization: split up ones with count = 0 into a list
				writer.Write7BitEncodedInt(unloadedIDs.Count);
				foreach (var pair in unloadedIDs)
				{
					writer.Write((string)pair.Key);

					writer.Write7BitEncodedInt(pair.Value.Count);
					foreach (var pair2 in pair.Value)
					{
						writer.Write((string)pair2.Name);
						writer.Write7BitEncodedInt(pair2.Count);
					}
				}

				//TODO optimization: split up ones with count = 0 into a list
				writer.Write7BitEncodedInt(pendingResearchableModdedIDs.Count);
				foreach (var pair in pendingResearchableModdedIDs)
				{
					writer.Write7BitEncodedInt(pair.Key);
					writer.Write7BitEncodedInt(pair.Value);
				}
			}
			finally
			{
				//long pos2 = writer.BaseStream.Position;
				//long diff = pos2 - pos;
				//logger.Info("packet send start: " + pos);
				//logger.Info("packet send len  : " + pos2);
				//logger.Info("packet send diff : " + diff);
			}

			//int asd = 0;
		}

		public void NetReceive(BinaryReader reader)
		{
			itemIDs = new HashSet<int>();
			unloadedIDs = new Dictionary<string, List<NameCountPair>>();
			pendingResearchableModdedIDs = new Dictionary<int, int>();

			//log4net.ILog logger = ModContent.GetInstance<TomeOfResearchSharing>().Logger;
			//long pos = reader.BaseStream.Position;
			try
			{
				BitsByte header = reader.ReadByte();
				bool reversed = header[0];
				bool empty = header[1];
				if (empty)
				{
					return;
				}

				int count;

				if (reversed)
				{
					count = reader.Read7BitEncodedInt();
					var missingIDs = new HashSet<int>();
					for (int i = 0; i < count; i++)
					{
						missingIDs.Add(reader.Read7BitEncodedInt());
					}

					foreach (var item in CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.Keys)
					{
						if (!missingIDs.Contains(item))
						{
							itemIDs.Add(item);
						}
					}
				}
				else
				{
					count = reader.Read7BitEncodedInt();
					for (int i = 0; i < count; i++)
					{
						itemIDs.Add(reader.Read7BitEncodedInt());
					}
				}

				count = reader.Read7BitEncodedInt();
				for (int i = 0; i < count; i++)
				{
					string modName = reader.ReadString();
					unloadedIDs[modName] = new List<NameCountPair>();

					int secondCount = reader.Read7BitEncodedInt();
					for (int j = 0; j < secondCount; j++)
					{
						string name = reader.ReadString();
						int countValue = reader.Read7BitEncodedInt();
						var pair = new NameCountPair(name, countValue);
						unloadedIDs[modName].Add(pair);
					}
				}

				count = reader.Read7BitEncodedInt();
				for (int i = 0; i < count; i++)
				{
					int key = reader.Read7BitEncodedInt();
					int value = reader.Read7BitEncodedInt();
					pendingResearchableModdedIDs.Add(key, value);
				}
			}
			finally
			{
				//long pos2 = reader.BaseStream.Position;
				//long diff = pos2 - pos;
				//logger.Info("packet recv start: " + pos);
				//logger.Info("packet recv len  : " + pos2);
				//logger.Info("packet recv diff : " + diff);
			}
		}
	}
}
