﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using BugSplatUnity.Runtime.Client;
using UnityEditor.iOS.Xcode;

public class BuildPostprocessors
{
	static string _platform;

	/// <summary>
	/// Upload Asset/Plugin symbol files to BugSplat. 
	/// We don't upload Unity symbol files because the build output only contains public symbol information.
	/// BugSplat is configured to use the Unity symbol server which has private symbols containing file, function, and line information.
	/// </summary>
	[PostProcessBuild(1)]
	public static async Task OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		var options = GetBugSplatOptions();

		if (options == null)
		{
			Debug.LogWarning(
				"No BugSplatOptions ScriptableObject found! Skipping build post-process tasks...");
			return;
		}

		if (target == BuildTarget.iOS)
			PostProcessIos(pathToBuiltProject, options);

		await UploadSymbolFiles(target, options);
	}

	private static async Task UploadSymbolFiles(BuildTarget target, BugSplatOptions options)
	{
		switch (target)
		{
			case BuildTarget.StandaloneWindows64:
				_platform = "x86_64";
				break;
			case BuildTarget.StandaloneWindows:
				_platform = "x86";
				break;
			default: return;
		}

		var projectDir = Path.GetDirectoryName(Application.dataPath);
		if (projectDir == null)
		{
			Debug.LogWarning($"Could not find data path directory {Application.dataPath}, skipping symbol uploads...");
			return;
		}

		var pluginsDir = Path.Combine(Path.Combine(projectDir, "Assets", "Plugins"), _platform);

		if (!Directory.Exists(pluginsDir))
		{
			Debug.LogWarning("Plugins directory doesn't exist, skipping symbol uploads...");
			return;
		}

		var database = options.Database;
		var application = string.IsNullOrEmpty(options.Application) ? Application.productName : options.Application;
		var version = string.IsNullOrEmpty(options.Version) ? Application.version : options.Version;
		var clientId = options.SymbolUploadClientId;
		var clientSecret = options.SymbolUploadClientSecret;

		if (string.IsNullOrEmpty(database))
		{
			Debug.LogWarning("BugSplatOptions Database was not set! Skipping symbol uploads...");
			return;
		}

		if (string.IsNullOrEmpty(clientId))
		{
			Debug.LogWarning("BugSplatOptions ClientID was not set! Skipping symbol uploads...");
			return;
		}

		if (string.IsNullOrEmpty(clientSecret))
		{
			Debug.LogWarning("BugSplatOptions ClientSecret was not set! Skipping symbol uploads...");
			return;
		}

		Debug.Log($"BugSplat Database: {database}");
		Debug.Log($"BugSplat Application: ${application}");
		Debug.Log($"BugSplat Version: ${version}");

		var fileExtensions = new List<string>()
		{
			".dll",
			".pdb"
		};
		var symbolFiles = Directory.GetFiles(pluginsDir, "*", SearchOption.AllDirectories)
			.Select(file => new FileInfo(file))
			.Where(fileInfo => fileExtensions.Any(ext => ext.Equals(fileInfo.Extension)))
			.ToList();

		foreach (var symbolFile in symbolFiles)
		{
			Debug.Log($"BugSplat found symbol file: {symbolFile.FullName}");
		}

		Debug.Log("About to upload symbol files to BugSplat...");

		try
		{
			using (var symbolUploader = SymbolUploader.CreateOAuth2SymbolUploader(clientId, clientSecret))
			{
				var responseMessages = await symbolUploader.UploadSymbolFiles(
					database,
					application,
					version,
					symbolFiles
				);

				if (responseMessages[0].IsSuccessStatusCode)
					Debug.Log("BugSplat symbol upload completed successfully!");
				else
					Debug.LogError("BugSplat symbol upload failed. " + responseMessages[0]);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError(ex);
		}
	}

	private static BugSplatOptions GetBugSplatOptions()
	{
		var guids = AssetDatabase.FindAssets("t:BugSplatOptions");

		if (guids.Length == 0)
		{
			return null;
		}

		var path = AssetDatabase.GUIDToAssetPath(guids[0]);
		return AssetDatabase.LoadAssetAtPath<BugSplatOptions>(path);
	}

	private static void PostProcessIos(string pathToBuiltProject, BugSplatOptions options)
	{
		var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);

		var project = new PBXProject();
		project.ReadFromString(File.ReadAllText(projectPath));

#if UNITY_2019_3_OR_NEWER
		var targetGuid = project.GetUnityFrameworkTargetGuid();
#else
		var targetName = PBXProject.GetUnityTargetName();
		var targetGuid = project.TargetGuidByName(targetName);
#endif

		project.AddBuildProperty(targetGuid, "OTHER_LDFLAGS", "-ObjC");
		project.AddBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");

		project.SetBuildProperty(targetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		var mainTargetGuid = project.GetUnityMainTargetGuid();
		project.AddBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
		project.SetBuildProperty(mainTargetGuid, "DEBUG_INFORMATION_FORMAT", "dwarf-with-dsym");

		ModifyPlist(pathToBuiltProject, options);
		AddBundle(pathToBuiltProject, project, targetGuid);
		AddBuildPhase(mainTargetGuid, project);

		File.WriteAllText(projectPath, project.WriteToString());
	}

	private static void ModifyPlist(string projectPath, BugSplatOptions options)
	{
		var plistInfoFile = new PlistDocument();

		var infoPlistPath = Path.Combine(projectPath, "Info.plist");
		plistInfoFile.ReadFromString(File.ReadAllText(infoPlistPath));

		const string bugSplatServerURLKey = "BugsplatServerURL";
		plistInfoFile.root.AsDict().SetString(bugSplatServerURLKey, $"https://{options.Database}.bugsplat.com/");

		File.WriteAllText(infoPlistPath, plistInfoFile.WriteToString());
	}

	private static void AddBundle(string pathToBuiltProject, PBXProject project, string targetGuid)
	{
		const string frameworksFolderPath = "Frameworks";
		const string bundleName = "HockeySDKResources.bundle";
		var files = Directory.GetDirectories(Path.Combine(pathToBuiltProject,
			frameworksFolderPath), bundleName, SearchOption.AllDirectories);

		if (!files.Any())
		{
			Debug.LogWarning("Could not find the .bundle file.");
			return;
		}

		var linkedResourcePathAbsolute = files.First();
		var substringIndex = linkedResourcePathAbsolute.IndexOf(frameworksFolderPath, StringComparison.Ordinal);
		var relativePath = linkedResourcePathAbsolute.Substring(substringIndex);

		var addFolderReference = project.AddFolderReference(relativePath, bundleName);
		project.AddFileToBuild(targetGuid, addFolderReference);
	}

	private static void AddBuildPhase(string targetGuid, PBXProject project)
	{
		const string shellPath = "/bin/sh";
		const int index = 999;
		const string name = "Upload dSYM files to BugSplat";
		const string shellScript =
			"if [ ! -f \"${HOME}/.bugsplat.conf\" ]\nthen\n    echo \"Missing bugsplat config file: ~/.bugsplat.conf\"\n    exit\nfi\n\nsource \"${HOME}/.bugsplat.conf\"\n\nif [ -z \"${BUGSPLAT_USER}\" ]\nthen\n    echo \"BUGSPLAT_USER must be set in ~/.bugsplat.conf\"\n    exit\nfi\n\nif [ -z \"${BUGSPLAT_PASS}\" ]\nthen\n    echo \"BUGSPLAT_PASS must be set in ~/.bugsplat.conf\"\n    exit\nfi\n\necho \"Product dir: ${BUILT_PRODUCTS_DIR}\"\n\nWORK_DIR=\"$PWD\"\nAPP=$(find $BUILT_PRODUCTS_DIR -name *.app -type d -maxdepth 1 -print | head -n1)\n\necho \"App: ${APP}\"\n\nFILE=\"${WORK_DIR}/Archive.zip\"\n\ncd $BUILT_PRODUCTS_DIR\nzip -r \"${FILE}\" ./* -x \"UnityFramework.framework/*\"\ncd -\n\n# Change Info.plist path\nAPP_MARKETING_VERSION=$(/usr/libexec/PlistBuddy -c \"Print CFBundleShortVersionString\" \"${APP}/Info.plist\")\nAPP_BUNDLE_VERSION=$(/usr/libexec/PlistBuddy -c \"Print CFBundleVersion\" \"${APP}/Info.plist\")\n\nif [ -z \"${APP_MARKETING_VERSION}\" ]\nthen\n\techo \"CFBundleShortVersionString not found in app Info.plist\"\n    exit\nfi\n\necho \"App marketing version: ${APP_MARKETING_VERSION}\"\necho \"App bundle version: ${APP_BUNDLE_VERSION}\"\n\nAPP_VERSION=\"${APP_MARKETING_VERSION}\"\n\nif [ -n \"${APP_BUNDLE_VERSION}\" ]\nthen\n    APP_VERSION=\"${APP_VERSION} (${APP_BUNDLE_VERSION})\"\nfi\n\n# Changed CFBundleName to CFBundleExecutable and Info.plist path\nPRODUCT_NAME=$(/usr/libexec/PlistBuddy -c \"Print CFBundleExecutable\" \"${APP}/Info.plist\")\n\nBUGSPLAT_SERVER_URL=$(/usr/libexec/PlistBuddy -c \"Print BugsplatServerURL\" \"${APP}/Info.plist\")\nBUGSPLAT_SERVER_URL=${BUGSPLAT_SERVER_URL%/}\n\nUPLOAD_URL=\"${BUGSPLAT_SERVER_URL}/post/plCrashReporter/symbol/\"\n\necho \"App version: ${APP_VERSION}\"\n\nUUID_CMD_OUT=$(xcrun dwarfdump --uuid \"${APP}/${PRODUCT_NAME}\")\nUUID_CMD_OUT=$([[ \"${UUID_CMD_OUT}\" =~ ^(UUID: )([0-9a-zA-Z\\-]+) ]] && echo ${BASH_REMATCH[2]})\necho \"UUID found: ${UUID_CMD_OUT}\"\n\necho \"Signing into bugsplat and storing session cookie for use in upload\"\n\nCOOKIEPATH=\"/tmp/bugsplat-cookie.txt\"\nLOGIN_URL=\"${BUGSPLAT_SERVER_URL}/browse/login.php\"\necho \"Login URL: ${LOGIN_URL}\"\nrm \"${COOKIEPATH}\"\ncurl -b \"${COOKIEPATH}\" -c \"${COOKIEPATH}\" --data-urlencode \"currusername=${BUGSPLAT_USER}\" --data-urlencode \"currpasswd=${BUGSPLAT_PASS}\" \"${LOGIN_URL}\"\n\necho \"Uploading ${FILE} to ${UPLOAD_URL}\"\n\ncurl -i -b \"${COOKIEPATH}\" -c \"${COOKIEPATH}\" -F filedata=@\"${FILE}\" -F appName=\"${PRODUCT_NAME}\" -F appVer=\"${APP_VERSION}\" -F buildId=\"${UUID_CMD_OUT}\" $UPLOAD_URL";

		if (string.IsNullOrEmpty(project.GetShellScriptBuildPhaseForTarget(targetGuid, name, shellPath, shellScript)))
			project.InsertShellScriptBuildPhase(index, targetGuid, name, shellPath, shellScript);
	}
}