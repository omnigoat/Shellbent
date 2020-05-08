using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;

namespace Shellbent.Settings
{
	abstract class ChangeProvider : IDisposable
	{
		public delegate void ChangedEvent();

		public abstract event ChangedEvent Changed;

		public abstract List<TitleBarSetting> Settings { get; }

		public void Dispose()
		{
			DisposeImpl();
		}

		protected abstract void DisposeImpl();
	}
}
