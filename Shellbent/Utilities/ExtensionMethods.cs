using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shellbent.Utilities
{
	static class ExtensionMethods
	{
		public static R? NullOr<T, R>(this T? self, System.Func<T, R?> f)
			where T : struct
			where R : struct
		{
			if (self.HasValue)
			{
				return f.Invoke(self.Value);
			}
			else
			{
				return null;
			}
		}

		public static R? NullOr<T, R>(this T? self, System.Func<T, R> f)
			where T : struct
			where R : struct
		{
			if (self.HasValue)
			{
				return (R?)f.Invoke(self.Value);
			}
			else
			{
				return null;
			}
		}
	}
}
