using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kea.CommonFiles
{
	class ToonHelpers
	{
		//Helpers that require toon structures.
		public static string GetToonSavePath(Structures.ToonListEntryInfo toonInfo)
		{
			//Must return without any slashes at the end
			string sanitizedTitleName = Helpers.SanitizeStringForFilePath(toonInfo.toonTitleName);
			return $"{sanitizedTitleName}[{toonInfo.titleNo.ToString("D6")}]";
		}
		
		public static string GetToonEpisodeSavePath(int i,Structures.EpisodeListEntry episodeInfo,string suffix)
		{
			//Must return without any slashes at the end
			//string BasePath = GetToonSavePath(episodeInfo.toonInfo); // commented for Refactor.
			return $"({i + 1}) {episodeInfo.episodeTitle}{suffix}";
		}
		
	}
}
