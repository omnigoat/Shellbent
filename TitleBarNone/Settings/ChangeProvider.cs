﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;

namespace Atma.TitleBarNone.Settings
{
	abstract class ChangeProvider : IDisposable
	{
		public delegate void ChangedEvent();

		public abstract event ChangedEvent Changed;

		public abstract List<SettingsTriplet> Triplets { get; }

		public void Dispose()
		{
			DisposeImpl();
		}

		protected abstract void DisposeImpl();
	}
}
