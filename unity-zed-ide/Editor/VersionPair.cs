#if !UNITY_MSBUILD_ENABLED
using System;

namespace Unifan.Zed.Editor
{
	internal struct VersionPair
	{
		public Version IdeVersion;
		public Version LanguageVersion;

		public VersionPair(int idemajor, int ideminor, int languageMajor, int languageMinor)
		{
			IdeVersion = new Version(idemajor, ideminor);
			LanguageVersion = new Version(languageMajor, languageMinor);
		}
	}
}
#endif
