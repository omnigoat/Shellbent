using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shellbent.Utilities
{
	internal class RequireStruct<T> where T : struct { }
	internal class RequireClass<T> where T : class { }

	static class ExtensionMethods
	{
		public static R? NullOr<R, T>(this T? self, Func<T, R?> f)
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

		public static R? NullOr<R, T>(this T? self, Func<T, R?> f, RequireStruct<R> _ = null)
			where T : struct
			where R : struct
		{
			if (self.HasValue)
			{
				return f.Invoke(self.Value).Value;
			}
			else
			{
				return null;
			}
		}

		public static R NullOr<R, T>(this T? self, Func<T, R> f, RequireClass<R> _ = null)
			where T : struct
			where R : class
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

		public static R NullOr<R, T>(this T self, Func<T, R> f, RequireClass<R> _ = null)
			where T : class
			where R : class
		{
			if (self != null)
			{
				return f.Invoke(self);
			}
			else
			{
				return null;
			}
		}


		public static bool RegexMatches(string str, string pattern, out Match match)
		{
			match = Regex.Match(str, pattern);
			return match.Success;
		}
	}
}
