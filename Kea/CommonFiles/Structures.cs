using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kea.CommonFiles
{
	public class Structures
	{
		public struct downloadedToonChapterFileInfo
        {
            public string filePath;
            public string filePathInArchive;
        }

		public struct EpisodeListEntry
		{
			public string episodeSequence;
			public int episodeNo;
			public string episodeTitle;
			public string url;
		}

		public struct ToonListEntryInfo
		{
			public int titleNo;
			public string toonTitleName;
			
			public string startDownloadAtEpisode;
			public string stopDownloadAtEpisode;
		}

		public struct ToonListEntry
		{
			public ToonListEntryInfo toonInfo;
			public EpisodeListEntry[] episodeList;
		}
	}
}
