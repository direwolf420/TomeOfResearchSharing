using System;
using Terraria.ModLoader.IO;

namespace TomeOfResearchSharing
{
	public class NameCountPair : TagSerializable
	{
		public static readonly Func<TagCompound, NameCountPair> DESERIALIZER = Load;

		public string Name { get; private set; }

		public int Count { get; private set; }

		public NameCountPair(string name, int count)
		{
			Name = name;
			Count = count;
		}

		public TagCompound SerializeData()
		{
			var tag = new TagCompound
			{
				{ "n", Name },
				{ "c", Count }
			};

			return tag;
		}

		public override string ToString()
		{
			return $"{Name} {Count}";
		}

		public static NameCountPair Load(TagCompound tag)
		{
			return new NameCountPair(tag.GetString("n"), tag.GetInt("c"));
		}
	}
}
