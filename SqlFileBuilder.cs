﻿// Copyright © 2020 Alex Kukhtin. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace A2v10.BuildSql
{
	public class SqlFileBuilder
	{
		private readonly String _path;
		private readonly TaskLoggingHelper _log;

		public SqlFileBuilder(String path, TaskLoggingHelper log)
		{
			_path = path;
			_log = log;
		}

		public void Process()
		{
			String jsonPath = Path.Combine(_path, "sql.json");
			if (!File.Exists(jsonPath))
			{
				return;
			}

			_log.LogMessage(MessageImportance.High, $"Processing {jsonPath}");

			String jsonText = File.ReadAllText(jsonPath);
			List<ConfigItem> list = JsonConvert.DeserializeObject<List<ConfigItem>>(jsonText);

			foreach (var item in list)
			{
				ProcessOneItem(item);
			}
		}

		void ProcessOneItem(ConfigItem item)
		{
			String outFilePath = Path.Combine(_path, item.outputFile);

			String dirName = Path.GetDirectoryName(Path.GetFullPath(outFilePath));
			if (!Directory.Exists(dirName))
				Directory.CreateDirectory(dirName);

			File.Delete(outFilePath);
			var nl = Environment.NewLine;
			FileStream fw = null;
			try
			{
				fw = File.Open(outFilePath, FileMode.CreateNew, FileAccess.Write);
				_log.LogMessage(MessageImportance.High, $"Writing {item.outputFile}");
				using (var sw = new StreamWriter(fw, new UTF8Encoding(true)))
				{
					fw = null;
					WriteVersion(item, sw);
					sw.Write($"{nl}{nl}/* {item.outputFile} */{nl}{nl}");
					foreach (var f in item.inputFiles)
					{
						var inputPath = Path.Combine(_path, f);
						_log.LogMessage(MessageImportance.High, $"\t{f}");
						var inputText = new StringBuilder(File.ReadAllText(inputPath));
						ProcessText(inputText, item);
						sw.Write(inputText.ToString());
						sw.WriteLine();
					}
				}
			}
			finally
			{
				if (fw != null)
					fw.Close();
			}
		}

		void ProcessText(StringBuilder sb, ConfigItem item)
		{
			if (item.replaceSessionContext)
			{
				// TODO:
				sb.Replace("default(cast(session_context(N'TenantId') as int))", "default(1)");
			}
			if (String.IsNullOrEmpty(item.remove))
				return;
			while (true)
			{
				var sx = sb.ToString();
				Int32 fPos = sx.IndexOf($"/*{item.remove.ToUpperInvariant()}=FALSE*/");
				if (fPos == -1)
					return;
				Int32 lPos = sx.IndexOf("go", fPos);
				sb.Remove(fPos, lPos - fPos + 2);
			}
		}

		void WriteVersion(ConfigItem item, StreamWriter writer)
		{
			if (String.IsNullOrEmpty(item.version))
				return;
			// write version
			var nl = Environment.NewLine;
			_log.LogMessage(MessageImportance.High, $"\tVersion: {item.version}");
			String msg = $"/*{nl}version: {item.version}{nl}generated: {DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")}{nl}*/";
			writer.WriteLine(msg);

			if (!String.IsNullOrEmpty(item.name))
			{
				var numVersion = item.NumVersion;
				var moduleName = $"script:{item.name}";
				var moduleTitle = "null";
				if (!String.IsNullOrEmpty(item.title))
					moduleTitle = $"N'{item.title}'";

				String updateVersion = $@"
set nocount on;
if not exists(select * from INFORMATION_SCHEMA.SCHEMATA where SCHEMA_NAME=N'a2sys')
	exec sp_executesql N'create schema a2sys';
go
-----------------------------------------------
if not exists(select * from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA=N'a2sys' and TABLE_NAME=N'Versions')
	create table a2sys.Versions
	(
		Module sysname not null constraint PK_Versions primary key,
		[Version] int null,
		[Title] nvarchar(255),
		[File] nvarchar(255)
	);
go
----------------------------------------------
if exists(select * from a2sys.Versions where [Module]=N'{moduleName}')
	update a2sys.Versions set [Version]={numVersion}, [File]=N'{item.outputFile}', Title={moduleTitle} where [Module]=N'{moduleName}';
else
	insert into a2sys.Versions([Module], [Version], [File], Title) values (N'{moduleName}', {numVersion}, N'{item.outputFile}', {moduleTitle});
go
";
				writer.WriteLine(updateVersion);
			}
		}
	}
}
