using System;
using System.ComponentModel;
using System.Reflection;
using System.Drawing.Design;
using System.Windows.Forms;
using System.IO;

namespace Shellbent.Editors
{
	public class FilePickerEditor : UITypeEditor
	{
		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			object page = context.Instance;

			PropertyInfo fi = page.GetType().GetProperty(context.PropertyDescriptor.Name);
			string path = fi.GetValue(page, new object[] { }) as string;
			FilePickerAttribute fpa = null;

			var attr = fi.GetCustomAttributes(typeof(FilePickerAttribute), false);
			if (attr != null && attr.Length > 0)
			{
				fpa = ((FilePickerAttribute)attr[0]);
			}

			OpenFileDialog ofd = new OpenFileDialog
			{
				CheckPathExists = true,
				CheckFileExists = false,
				Filter = fpa?.Filter,
				FilterIndex = fpa?.FilterIndex ?? 1
			};

			if (string.IsNullOrEmpty(path))
			{
				ofd.FileName = fpa?.DefaultFileName ?? string.Empty;
			}
			else if (fpa?.StoreFolder == true)
			{
				ofd.InitialDirectory = path;
				ofd.FileName = fpa?.DefaultFileName ?? string.Empty;
				ofd.CheckFileExists = false;
			}
			else
			{
				ofd.FileName = Path.GetFileName(path);
				ofd.InitialDirectory = Path.GetDirectoryName(path);
			}

			if (ofd.ShowDialog() == DialogResult.OK)
			{
				if (fpa?.StoreFolder == true)
					return Path.GetDirectoryName(ofd.FileName);
				return ofd.FileName;
			}
			return path;
		}
	}

	public class FilePickerAttribute : Attribute
	{
		public readonly bool StoreFolder;
		public readonly string DefaultFileName;
		public readonly string Filter;
		public readonly int FilterIndex;

		public FilePickerAttribute(bool StoreFolder = false, string DefaultFileName = null, string Filter = null, int FilterIndex = 1)
		{
			this.StoreFolder = StoreFolder;
			this.DefaultFileName = DefaultFileName;
			this.Filter = Filter;
			this.FilterIndex = FilterIndex;
		}
	}

}
