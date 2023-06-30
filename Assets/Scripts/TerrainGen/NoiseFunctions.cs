using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

[BurstCompile]
public static class NoiseFunctions
{
	[BurstCompile]
	public static float PerlinNoise2D(float x, float y, float z, float frequency, float amplitude)
	{
		float height = noise.cnoise(new float2(x * frequency, z * frequency)) * amplitude;
		return y - height;
	}

	[BurstCompile]
	public static float SimplexNoise2D(float x, float y, float z, float frequency, float amplitude)
	{
		float height = noise.snoise(new float2(x * frequency, z * frequency)) * amplitude;
		return y - height;
	}
}
