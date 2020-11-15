// Copyright © 2020 Alex Kukhtin. All rights reserved.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace A2v10.BuildSql
{
	public class Build : Task
	{
		public String ProjectDir { get; set; }

		public override Boolean Execute()
		{
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			Log.LogMessage(MessageImportance.Normal, $"A2v10.BuildSql version:{ver}");

			if (ProjectDir == null)
				return false;

			Log.LogMessage(MessageImportance.Normal, $"Project dir: {ProjectDir}");
			var appPath = Path.GetFullPath(Path.Combine(ProjectDir, "App_application"));
			if (!Directory.Exists(appPath))
				return true; // success

			var sb = new SqlFileBuilder(appPath, Log);
			sb.Process();
			foreach (var dir in Directory.GetDirectories(appPath))
			{
				var fileName = Path.GetFileName(dir);
				if (fileName.StartsWith("@") || fileName == "sql")
					continue;
				sb = new SqlFileBuilder(dir, Log);
				sb.Process();
			}
			return true;
		}
	}
}
