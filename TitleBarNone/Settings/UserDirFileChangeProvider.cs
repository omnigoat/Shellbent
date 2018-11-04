﻿using System;
using System.IO;

namespace Atma.TitleBarNone.Settings
{
	class UserDirFileChangeProvider : FileChangeProvider
	{
		public UserDirFileChangeProvider()
			: base(GetUserDirFile())
		{
		}

		private static string GetUserDirFile()
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Defaults.ConfgFileName);
		}
	}
}
