using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using System.Windows.Forms;
using System.Drawing;

namespace Shellbent.Editors
{
	public class PreviewRequiresAttribute : Attribute
	{
		public enum Requirement
		{
			None,
			Solution,
			Document,
		}

		public PreviewRequiresAttribute(Requirement requires)
			: base()
		{
			Require = requires;
		}

		public readonly Requirement Require;
	}

	public class PatternEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.DropDown;
		}

		public override void PaintValue(PaintValueEventArgs e)
		{
			using (Pen p = Pens.Black)
			{
				e.Graphics.DrawRectangle(p, e.Bounds.Left, e.Bounds.Top, Math.Min(e.Bounds.Width, 40), e.Bounds.Height);

				// draw regular stuff in the leftover space
				base.PaintValue(new PaintValueEventArgs(e.Context, e.Value, e.Graphics,
					new Rectangle(e.Bounds.Left + Math.Min(e.Bounds.Width, 40), e.Bounds.Top, e.Bounds.Width, e.Bounds.Height)));
			}
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			Controls.PatternControl ctl = new Controls.PatternControl();
			

			object page = context.Instance;

			PropertyInfo fi = page.GetType().GetProperty(context.PropertyDescriptor.Name);
			string ep = fi.GetValue(page, new object[] { }) as string; // get the attribute value
			string defVal = null;
			{
				var attr = fi.GetCustomAttributes(typeof(DefaultValueAttribute), false);
				if (attr != null && attr.Length > 0)
				{
					defVal = ((DefaultValueAttribute)attr[0]).Value.ToString();
				}
			}

			return "lmao no";
		}
	}
}
