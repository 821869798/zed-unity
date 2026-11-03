/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Unity Technologies.
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Generic;
using System.IO;

namespace Unifan.Zed.Editor
{
	internal static class Discovery
	{
		public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
		{
			foreach (var installation in ZedInstallation.GetVisualStudioInstallations())
				yield return installation;
		}

		public static bool TryDiscoverInstallation(string editorPath, out IVisualStudioInstallation installation)
		{
			try
			{
				if (ZedInstallation.TryDiscoverInstallation(editorPath, out installation))
					return true;
			}
			catch (IOException)
			{
				installation = null;
			}

			return false;
		}

		public static void Initialize()
		{
			ZedInstallation.Initialize();
		}
	}
}
