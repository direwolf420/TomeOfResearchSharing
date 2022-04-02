using System.Collections.Generic;
using Terraria.GameContent.Creative;
using Terraria.ID;
using Terraria.ModLoader;

namespace TomeOfResearchSharing
{
	public class TomeOfResearchSharing : Mod
	{
		public static HashSet<int> vanillaDeprecated;

		//MAGIC NUMBER: Item is already researched: write as 0 (0 normally is "nothing" but in this case it wouldn't even be stored)
		public static readonly int FullyResearchedCount = 0;

		public override void Load()
		{
			var vanillaDeprecatedTemp = new int[] { ItemID.LesserRestorationPotion, ItemID.FirstFractal };

			vanillaDeprecated = new HashSet<int>();
			foreach (var item in vanillaDeprecatedTemp)
			{
				if (CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId.ContainsKey(item))
				{
					//Safeguard against possible future terraria updates or mods that mess with that
					vanillaDeprecated.Add(item);
				}
			}

		}

		public override void Unload()
		{
			vanillaDeprecated = null;
		}
	}
}
