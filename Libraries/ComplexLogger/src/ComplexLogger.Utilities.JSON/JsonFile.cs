using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ComplexLogger.Utilities.Exceptions;

namespace ComplexLogger.Utilities.JSON;

public class JsonFile
{
	public static JsonSerializerOptions GetDefaultOptions()
	{
		return new JsonSerializerOptions
		{
			WriteIndented = true,
			IncludeFields = true
		};
	}

	public static void Save<T>(string configFileName, T Tinput, JsonSerializerOptions? options = null)
	{
		try
		{
			if (options == null)
			{
				options = GetDefaultOptions();
			}
			using FileStream fileStream = File.Open(configFileName, FileMode.Create, FileAccess.Write, FileShare.None);
			JsonSerializer.Serialize(fileStream, Tinput, options);
			fileStream.Dispose();
		}
		catch (Exception inner)
		{
			throw new BadMemeException("Attempting to save " + configFileName + " failed", inner);
		}
	}

	public static T? Load<T>(string? configFileName, bool createFile = false, JsonSerializerOptions? options = null)
	{
		if (string.IsNullOrWhiteSpace(configFileName))
		{
			throw new BadMemeException("configFileName is null, empty or whitespace");
		}
		if (!File.Exists(configFileName))
		{
			if (!createFile)
			{
				throw new BadMemeException($"JsonFile.Load({configFileName}, {createFile})::Requested JSON file does not exist");
			}
			Save(configFileName, default(T), options);
		}
		try
		{
			if (options == null)
			{
				options = GetDefaultOptions();
			}
			using FileStream fileStream = File.Open(configFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			T? result = JsonSerializer.Deserialize<T>(fileStream, options);
			fileStream.Dispose();
			return result;
		}
		catch (Exception inner)
		{
			throw new BadMemeException("Attempting to load the config file failed, file: " + configFileName, inner);
		}
	}

	public static async Task<T?> LoadAsync<T>(string configFileName, bool createFile = false, JsonSerializerOptions? options = null)
	{
		string configFileName2 = configFileName;
		JsonSerializerOptions options2 = options;
		return await Task.Run(() => Load<T>(configFileName2, createFile, options2));
	}

	public static async Task SaveAsync<T>(string configFileName, T Tinput, JsonSerializerOptions? options = null)
	{
		string configFileName2 = configFileName;
		T Tinput2 = Tinput;
		JsonSerializerOptions options2 = options;
		await Task.Run(delegate
		{
			Save(configFileName2, Tinput2, options2);
		});
	}
}
