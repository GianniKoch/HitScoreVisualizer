﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HitScoreVisualizer.Models;
using HitScoreVisualizer.Settings;
using Newtonsoft.Json;
using Zenject;
using Version = SemVer.Version;

namespace HitScoreVisualizer.Services
{
	internal class ConfigProvider : IInitializable
	{
		private readonly HSVConfig _hsvConfig;

		private readonly string _hsvConfigsFolderPath;
		private readonly JsonSerializerSettings _jsonSerializerSettings;

		private readonly Dictionary<Version, Func<Configuration, bool>> _migrationActions;

		internal Version MinimumMigratableVersion { get; }
		internal Version MaximumMigrationNeededVersion { get; }

		internal string? CurrentConfigPath => _hsvConfig.ConfigFilePath;
		internal static Configuration? CurrentConfig { get; set; }

		public ConfigProvider(HSVConfig hsvConfig)
		{
			_hsvConfig = hsvConfig;

			_jsonSerializerSettings = new JsonSerializerSettings {DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate, Formatting = Formatting.Indented};
			_hsvConfigsFolderPath = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, nameof(HitScoreVisualizer));

			_migrationActions = new Dictionary<Version, Func<Configuration, bool>>
			{
				{new Version(2, 0, 0), RunMigration2_0_0},
				{new Version(2, 1, 0), RunMigration2_1_0},
				{new Version(2, 2, 3), RunMigration2_2_3}
			};

			MinimumMigratableVersion = _migrationActions.Keys.Min();
			MaximumMigrationNeededVersion = _migrationActions.Keys.Max();

			Plugin.LoggerInstance.Debug("Creating configs folder");
			if (!Directory.Exists(_hsvConfigsFolderPath))
			{
				Directory.CreateDirectory(_hsvConfigsFolderPath);
			}
		}

		public async void Initialize()
		{
			if (_hsvConfig.ConfigFilePath == null)
			{
				return;
			}

			if (!File.Exists(_hsvConfig.ConfigFilePath))
			{
				_hsvConfig.ConfigFilePath = null;
				return;
			}

			var userConfig = await LoadConfig(_hsvConfig.ConfigFilePath).ConfigureAwait(false);
			if (userConfig == null)
			{
				return;
			}

			var configFileInfo = new ConfigFileInfo(Path.GetFileNameWithoutExtension(_hsvConfig.ConfigFilePath), _hsvConfig.ConfigFilePath)
			{
				Configuration = userConfig, State = GetConfigState(userConfig)
			};

			SelectUserConfig(configFileInfo);
		}

		internal async Task<IEnumerable<ConfigFileInfo>> ListAvailableConfigs()
		{
			var configFileInfoList = Directory
				.GetFiles(_hsvConfigsFolderPath)
				.Select(x => new ConfigFileInfo(Path.GetFileNameWithoutExtension(x), x))
				.ToList();

			foreach (var configInfo in configFileInfoList)
			{
				configInfo.Configuration = await LoadConfig(configInfo.ConfigPath);
				configInfo.State = GetConfigState(configInfo.Configuration);
			}

			return configFileInfoList;
		}

		internal bool ConfigSelectable(ConfigState? state)
		{
			switch (state)
			{
				case ConfigState.Compatible:
				case ConfigState.NeedsMigration:
					return true;
				default:
					return false;
			}
		}

		internal void SelectUserConfig(ConfigFileInfo? configFileInfo)
		{
			// safe-guarding just to be sure
			if (!ConfigSelectable(configFileInfo?.State))
			{
				return;
			}

			if (configFileInfo!.State == ConfigState.NeedsMigration)
			{
				RunMigration(configFileInfo.Configuration!);
			}

			CurrentConfig = configFileInfo.Configuration;
			_hsvConfig.ConfigFilePath = configFileInfo.ConfigPath;
		}

		private async Task<Configuration?> LoadConfig(string path)
		{
			try
			{
				using var fileStream = File.OpenRead(path);
				using var streamReader = new StreamReader(fileStream);
				var content = await streamReader.ReadToEndAsync().ConfigureAwait(false);
				return JsonConvert.DeserializeObject<Configuration>(content, _jsonSerializerSettings);
			}
			catch
			{
				// Expected behaviour when file isn't an actual hsv config file...
				return null!;
			}
		}

		private ConfigState GetConfigState(Configuration? configuration)
		{
			if (configuration?.Version == null)
			{
				return ConfigState.Broken;
			}

			if (configuration.Version > Plugin.Version)
			{
				return ConfigState.NewerVersion;
			}

			if (configuration.Version < MinimumMigratableVersion)
			{
				return ConfigState.Incompatible;
			}

			if (!Validate(configuration))
			{
				return ConfigState.ValidationFailed;
			}

			return configuration.Version <= MaximumMigrationNeededVersion ? ConfigState.NeedsMigration : ConfigState.Compatible;
		}

		private static bool Validate(Configuration? configuration)
		{
			if (configuration == null || (!configuration.Judgments?.Any() ?? true))
			{
				return false;
			}

			return configuration.Judgments!.All(ValidateJudgment);
		}

		private static bool ValidateJudgment(Judgment judgment)
		{
			if (judgment.Color.Count != 4)
			{
				Plugin.LoggerInstance.Warn($"Judgment \"{judgment.Text}\" with threshold {judgment.Threshold} has invalid color!");
				Plugin.LoggerInstance.Warn("Make sure to include exactly 4 numbers for each judgment's color!");
				return false;
			}

			return true;
		}

		private void RunMigration(Configuration userConfig)
		{
			var userConfigVersion = userConfig.Version;
			foreach (var requiredMigration in _migrationActions.Keys.Where(migrationVersion => migrationVersion >= userConfigVersion))
			{
				_migrationActions[requiredMigration](userConfig);
			}

			userConfig.Version = Plugin.Version;
		}

		private static bool RunMigration2_0_0(Configuration configuration)
		{
			configuration.BeforeCutAngleJudgments = new List<JudgmentSegment> {JudgmentSegment.Default};
			configuration.AccuracyJudgments = new List<JudgmentSegment> {JudgmentSegment.Default};
			configuration.AfterCutAngleJudgments = new List<JudgmentSegment> {JudgmentSegment.Default};

			return true;
		}

		private static bool RunMigration2_1_0(Configuration configuration)
		{
			if (configuration.Judgments != null)
			{
				foreach (var j in configuration.Judgments.Where(j => j.Threshold == 110))
				{
					j.Threshold = 115;
				}
			}

			if (configuration.AccuracyJudgments != null)
			{
				foreach (var aj in configuration.AccuracyJudgments.Where(aj => aj.Threshold == 10))
				{
					aj.Threshold = 15;
				}
			}

			return true;
		}

		private static bool RunMigration2_2_3(Configuration configuration)
		{
			configuration.DoIntermediateUpdates = true;

			return true;
		}
	}
}