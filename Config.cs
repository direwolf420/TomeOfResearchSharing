using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace TomeOfResearchSharing
{
	public class Config : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static Config Instance => ModContent.GetInstance<Config>();

		[DefaultValue(false)]
		public bool TransferUnloadedData;
	}
}
