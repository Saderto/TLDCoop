using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace FoodPackByEMPEROR;

public static class Assets
{
	private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

	public static Texture2D? Texture(string relativePath, bool compress = true, bool mipmaps = true)
	{
		if (textures.TryGetValue(relativePath, out Texture2D value))
		{
			return value;
		}
		string text = Path.Combine(Mod.Root, relativePath);
		if (!File.Exists(text))
		{
			MelonLogger.Error("Missing texture: " + text);
			return null;
		}
		byte[] array = File.ReadAllBytes(text);
		bool flag = array.Length <= 25 || array[25] != 2;
		Texture2D texture2D = new Texture2D(2, 2, flag ? TextureFormat.RGBA32 : TextureFormat.RGB24, mipmaps);
		texture2D.LoadImage(array, markNonReadable: false);
		if (compress && !flag && texture2D.format != TextureFormat.RGB24)
		{
			Texture2D texture2D2 = new Texture2D(texture2D.width, texture2D.height, TextureFormat.RGB24, mipmaps);
			texture2D2.SetPixels32(texture2D.GetPixels32());
			texture2D2.Apply(mipmaps);
			UnityEngine.Object.Destroy(texture2D);
			texture2D = texture2D2;
		}
		texture2D.name = Path.GetFileNameWithoutExtension(text);
		texture2D.filterMode = ((!mipmaps) ? FilterMode.Bilinear : FilterMode.Trilinear);
		texture2D.anisoLevel = 8;
		texture2D.mipMapBias = -0.5f;
		texture2D.hideFlags = HideFlags.HideAndDontSave;
		if (compress)
		{
			try
			{
				texture2D.Compress(highQuality: true);
				texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			}
			catch (Exception e)
			{
				Safe.Log("Compressing " + relativePath, e);
			}
		}
		if (textures.Count == 0)
		{
			MelonLogger.Msg($"Texture {texture2D.name}: {texture2D.width}x{texture2D.height} {texture2D.format}, readable {texture2D.isReadable}");
		}
		return textures[relativePath] = texture2D;
	}

	public static Mesh? LoadMesh(string relativePath)
	{
		if (meshes.TryGetValue(relativePath, out Mesh value))
		{
			return value;
		}
		string text = Path.Combine(Mod.Root, relativePath);
		if (!File.Exists(text))
		{
			MelonLogger.Error("Missing mesh: " + text);
			return null;
		}
		using BinaryReader binaryReader = new BinaryReader(File.OpenRead(text));
		int num = binaryReader.ReadInt32();
		int num2 = binaryReader.ReadInt32();
		Il2CppStructArray<Vector3> il2CppStructArray = new Il2CppStructArray<Vector3>(num);
		Il2CppStructArray<Vector3> il2CppStructArray2 = new Il2CppStructArray<Vector3>(num);
		Il2CppStructArray<Vector2> il2CppStructArray3 = new Il2CppStructArray<Vector2>(num);
		for (int i = 0; i < num; i++)
		{
			il2CppStructArray[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int j = 0; j < num; j++)
		{
			il2CppStructArray2[j] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		for (int k = 0; k < num; k++)
		{
			il2CppStructArray3[k] = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
		}
		Il2CppStructArray<int> il2CppStructArray4 = new Il2CppStructArray<int>(num2);
		for (int l = 0; l < num2; l++)
		{
			il2CppStructArray4[l] = binaryReader.ReadInt32();
		}
		Mesh mesh = new Mesh
		{
			name = Path.GetFileNameWithoutExtension(text),
			indexFormat = IndexFormat.UInt32
		};
		mesh.vertices = il2CppStructArray;
		mesh.normals = il2CppStructArray2;
		mesh.uv = il2CppStructArray3;
		mesh.uv2 = il2CppStructArray3;
		Il2CppStructArray<Color32> il2CppStructArray5 = new Il2CppStructArray<Color32>(num);
		for (int m = 0; m < num; m++)
		{
			il2CppStructArray5[m] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
		}
		mesh.colors32 = il2CppStructArray5;
		mesh.triangles = il2CppStructArray4;
		mesh.RecalculateBounds();
		mesh.hideFlags = HideFlags.HideAndDontSave;
		return meshes[relativePath] = mesh;
	}
}
