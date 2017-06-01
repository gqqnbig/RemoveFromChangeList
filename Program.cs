using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RemoveFromChangeList
{
	class Program
	{
		static readonly MD5 md5Tool = MD5.Create();
		static readonly string diffFolder = Path.Combine(Path.GetTempPath(), "RemoveFromChangeList");

		static void Main(string[] args)
		{

			string path;
			path = args[0];


			var modifiedFilesInChangeLists = GetModifiedFiles(path);

			var diffPathes = modifiedFilesInChangeLists.Select(GetDiffFilePath).ToList();
			foreach (string filePath in Directory.EnumerateFiles(diffFolder))
			{
				if (diffPathes.Contains(filePath) == false)	//the file is no longer in a change list, we should remove the diff file.
				{
					Console.WriteLine($"{filePath} is no longer in any change list. Delete it.");
					File.Delete(filePath);
				}
			}



			foreach (string filePath in modifiedFilesInChangeLists)
			{
				var startInfo = new ProcessStartInfo();
				startInfo.RedirectStandardOutput = true;
				startInfo.FileName = "svn";
				startInfo.Arguments = $"diff --internal-diff {filePath}";
				startInfo.UseShellExecute = false;

				var p = Process.Start(startInfo);
				//Ignore first 4 lines.
				p.StandardOutput.ReadLine();
				p.StandardOutput.ReadLine();
				p.StandardOutput.ReadLine();
				p.StandardOutput.ReadLine();




				StringBuilder newDiffBuilder = new StringBuilder();
				while (p.StandardOutput.EndOfStream == false)
				{
					var str = p.StandardOutput.ReadLine();
					if (str.StartsWith("@@"))
						continue;

					newDiffBuilder.AppendLine(str);
				}


				var historyDiff = GetDiffFilePath(filePath);
				Directory.CreateDirectory(Path.GetDirectoryName(historyDiff));
				if (File.Exists(historyDiff))
				{
					string oldDiff;
					using (var sr = new StreamReader(historyDiff))
					{
						oldDiff = sr.ReadToEnd();
					}

					if (oldDiff != newDiffBuilder.ToString())
					{
						Console.WriteLine($"{path} has changed. Move it from change list");
						Process.Start("svn", "changelist --remove " + path);

						File.Delete(historyDiff);
					}
				}
				else
				{
					using (var sw = new StreamWriter(historyDiff))
						sw.Write(newDiffBuilder.ToString());
				}

			}


			Console.ReadKey();
		}

		private static string GetDiffFilePath(string filePath)
		{
			var md5Hash = GetMd5Hash(md5Tool, filePath);
			md5Hash = md5Hash.Substring(0, 8);

			return Path.Combine(diffFolder, md5Hash + "-" + Path.GetFileName(filePath) + ".diff");
		}



		private static List<string> GetModifiedFiles(string path)
		{
			var startInfo = new ProcessStartInfo();
			startInfo.RedirectStandardOutput = true;
			startInfo.FileName = "svn";
			startInfo.Arguments = $"status --xml {path}";
			startInfo.UseShellExecute = false;

			var p = Process.Start(startInfo);


			XElement output = XElement.Load(p.StandardOutput);
			var modifiedFilesInChangeLists = from a in ((IEnumerable)output.XPathEvaluate(@"//changelist//wc-status[@item=""modified""]/../@path")).OfType<XAttribute>()
											 select a.Value;
			return modifiedFilesInChangeLists.ToList();
		}

		static string GetMd5Hash(MD5 md5Hash, string input)
		{

			// Convert the input string to a byte array and compute the hash.
			byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

			// Create a new Stringbuilder to collect the bytes
			// and create a string.
			StringBuilder sBuilder = new StringBuilder();

			// Loop through each byte of the hashed data 
			// and format each one as a hexadecimal string.
			for (int i = 0; i < data.Length; i++)
			{
				sBuilder.Append(data[i].ToString("x2"));
			}

			// Return the hexadecimal string.
			return sBuilder.ToString();
		}
	}
}
