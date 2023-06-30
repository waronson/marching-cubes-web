using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;


[BurstCompile]
public struct PerlinNoiseJob : IJobParallelFor
{
	[WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> _values;
	public float _frequency;
	public float _amplitude;
	public int _size;
	public Vector3Int _offset;

	public void Execute(int index)
	{
		int x, y, z;
		Shared.CoordFromIndex(index, _size, out x, out y, out z);
		x += _offset.x;
		y += _offset.y;
		z += _offset.z;

		float height = noise.cnoise(new float2(x * _frequency, z * _frequency)) * _amplitude;
		_values[index] = y - height;
	}
}

[BurstCompile]
public struct SimplexNoiseJob : IJobParallelFor
{
	[WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> _values;
	public float _frequency;
	public float _amplitude;
	public int _size;
	public Vector3Int _offset;

	public void Execute(int index)
	{
		int x, y, z;
		Shared.CoordFromIndex(index, _size, out x, out y, out z);
		x += _offset.x;
		y += _offset.y;
		z += _offset.z;

		float height = noise.snoise(new float2(x * _frequency, z * _frequency)) * _amplitude;
		_values[index] = y - height;
	}
}

[BurstCompile]
public struct IQNoiseJob : IJobParallelFor
{
	[WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float> _values;
	public float _frequency;
	public float _amplitude;
	public int _size;
	public Vector3Int _offset;

	public void Execute(int index)
	{
		int x, y, z;
		Shared.CoordFromIndex(index, _size, out x, out y, out z);
		x += _offset.x;
		y += _offset.y;
		z += _offset.z;

		float height = Height(new float2(x * _frequency, z * _frequency), 9) * _amplitude;
		_values[index] = y - height;
	}

	private float Height(float2 p, int octaves)
	{
		float e = fbm(p, octaves);
		return e;
	}

	private float3 noised(float2 x)
	{
		float2 p = math.floor(x);
		float2 w = math.frac(x);

		float2 u = w * w * w * (w * (w * 6f - 15f) + 10f);
		float2 du = 30f * w * w * (w * (w - 2f) + 1f);

		float a = math.hash(p + new float2(0, 0));
		float b = math.hash(p + new float2(1, 0));
		float c = math.hash(p + new float2(0, 1));
		float d = math.hash(p + new float2(1, 1));

		float k0 = a;
		float k1 = b - a;
		float k2 = c - a;
		float k4 = a - b - c + d;

		return new float3(-1.0f + 2.0f * (k0 + k1 * u.x + k2 * u.y + k4 * u.x * u.y),
							2.0f * du * new float2(k1 + k4 * u.y,
							 k2 + k4 * u.x));
	}

	private float noise(float2 x)
	{
		float2 p = math.floor(x);
		float2 w = math.frac(x);
		float2 u = w*w*w*(w*(w*6.0f-15.0f)+10.0f);

		float a = math.hash(p + new float2(0, 0));
		float b = math.hash(p + new float2(1, 0));
		float c = math.hash(p + new float2(0, 1));
		float d = math.hash(p + new float2(1, 1));

		return -1.0f + 2.0f * (a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y);
	}

	private float fbm(float2 x, int octaves)
	{
		float2x2 m2 = new float2x2(0.80f, 0.60f,
									-0.60f, 0.80f);

		float f = 1.9f;
		float s = 0.55f;
		float a = 0.0f;
		float b = 0.5f;
		for (int i = 0; i < 9; i++)
		{
			float n = noise(x);
			a += b * n;
			b *= s;
			x = f * Mat2ByVec2(m2, x);
		}
		return a;
	}

	private float2 Mat2ByVec2(float2x2 mat, float2 vec)
	{
		return new float2
		(
			(mat.c0 * vec).x + (mat.c0 * vec).y,
			(mat.c1 * vec).x + (mat.c1 * vec).y
		);
	}

	private float3 Mat3ByVec3(float3x3 mat, float3 vec)
	{
		return new float3
		(
			(mat.c0 * vec).x + (mat.c0 * vec).y + (mat.c0 * vec).z,
			(mat.c1 * vec).x + (mat.c1 * vec).y + (mat.c1 * vec).z,
			(mat.c2 * vec).x + (mat.c2 * vec).y + (mat.c2 * vec).z
		);
	}
}

public static class Shared
{
	public static void CoordFromIndex(int index, int size, out int x, out int y, out int z)
	{
		x = index % size;
		y = (index / size) % size;
		z = index / (size * size);
	}
}