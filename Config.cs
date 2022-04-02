using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace TomeOfResearchSharing
{
	public class Config : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static Config Instance => ModContent.GetInstance<Config>();

		[Label("Transfer Unloaded Data")]
		[Tooltip("Toggle if researched items from mods that are currently unloaded should be transferred aswell")]
		[DefaultValue(false)]
		public bool TransferUnloadedData;
	}
}
