using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System.Diagnostics;
using System;

public class VoxelTerrain : MonoBehaviour
{
	[SerializeField] int _sizeInChunks = 8;
	[SerializeField] float _isoLevel = 1;
	[SerializeField] Material _voxelMaterial;
	[SerializeField] float _editInterval = .05f;
	[SerializeField] VoxelNetworkSocket voxelSocket;
	[SerializeField] float DEBUG_AMPLITUDE = .5f;
	[SerializeField] float DEBUG_FREQUENCY = .5f;
	[SerializeField] float DEBUG_EDIT_RADIUS = 1f;
	[SerializeField] float DEBUG_EDIT_STRENGTH = 1f;
	[SerializeField] bool DEBUG_DRAW_EDIT_GIZMOS = false;

	private Dictionary<Vector3Int, GameObject> _chunks = new Dictionary<Vector3Int, GameObject>();
	private NativeList<VoxelEdit> _edits;
	private float _timeTilNextEdit = 0f;

	private const int CHUNK_SIZE = 8;

	public int SizeInVoxels { get { return _sizeInChunks * CHUNK_SIZE; } }


	private void Start()
	{
		_edits = new NativeList<VoxelEdit>(Allocator.Persistent);

		if (voxelSocket != null)
			voxelSocket.VoxelEditEvent.AddListener(OnRecieveEdit);

		for (int x = 0; x < _sizeInChunks; x++)
			for (int y = 0; y < _sizeInChunks; y++)
				for (int z = 0; z < _sizeInChunks; z++)
				{
					GameObject chunk = new GameObject();
					chunk.name = "Chunk " + x + y + z;
					chunk.layer = gameObject.layer;
					chunk.AddComponent<MeshFilter>();
					var meshRenderer = chunk.AddComponent<MeshRenderer>();
					meshRenderer.sharedMaterial = _voxelMaterial;
					chunk.AddComponent<MeshCollider>();
					chunk.transform.parent = transform;

					chunk.transform.localPosition = new Vector3
					(
						SizeInVoxels / -2f + x * (CHUNK_SIZE),
						SizeInVoxels / -2f + y * (CHUNK_SIZE),
						SizeInVoxels / -2f + z * (CHUNK_SIZE)
					);

					_chunks.Add(new Vector3Int((int)chunk.transform.localPosition.x, (int)chunk.transform.localPosition.y, (int)chunk.transform.localPosition.z), chunk);
					UpdateChunk(chunk);
				}
	}

	private void OnDestroy()
	{
		_edits.Dispose();
		if (voxelSocket != null)
			voxelSocket.VoxelEditEvent.RemoveListener(OnRecieveEdit);
	}

	private void Update()
	{
		_timeTilNextEdit -= Time.deltaTime;
		if (_timeTilNextEdit > 0f)
			return;

		
		if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
		{
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
			{
				if (voxelSocket != null && voxelSocket.IsConnected)
					voxelSocket.SendEdit(new VoxelEdit { _position = hit.point, _radius = DEBUG_EDIT_RADIUS, _strength = DEBUG_EDIT_STRENGTH, _additive = Input.GetMouseButton(0) });
				else
					AddEdit(hit.point, radius: DEBUG_EDIT_RADIUS, strength: DEBUG_EDIT_STRENGTH, additive: Input.GetMouseButton(0));
			}

			_timeTilNextEdit = _editInterval;
		}
	}

	float DEBUG_GetValue(int x, int y, int z, float frequency, float amplitude)
	{
		float height = noise.snoise(new float2(x * frequency, z * frequency)) * amplitude;
		return y - height;
	}

	void UpdateChunk(GameObject chunk)
	{
		var filter = chunk.GetComponent<MeshFilter>();
		if (!filter) 
			return;

		Vector3 offset = new Vector3(chunk.transform.localPosition.x, chunk.transform.localPosition.y, chunk.transform.localPosition.z);

		//March
		NativeArray<Triangle> triangles = new NativeArray<Triangle>(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE * 5, Allocator.Persistent);
		NativeArray<int> triangleIndex = new NativeArray<int>(1, Allocator.Persistent);

		MarchingCubesJob job = new MarchingCubesJob 
		{ 
			_size = CHUNK_SIZE, 
			_offset = new float4(chunk.transform.localPosition.x, chunk.transform.localPosition.y, chunk.transform.localPosition.z, 0f), 
			_editsIn = _edits,
			_trianglesOut = triangles, 
			_triangleIndex = triangleIndex 
		};

		job.Schedule(CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE, 2).Complete();


		if (filter.mesh == null)
			filter.mesh = new Mesh();

		filter.mesh.Clear();

		var vertices = new Vector3[triangleIndex[0] * 3];
		var normals = new Vector3[triangleIndex[0] * 3];
		var meshTriangles = new int[triangleIndex[0] * 3];
		var uvs = new Vector2[triangleIndex[0] * 3];

		for (int i = 0; i < triangleIndex[0]; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				meshTriangles[i * 3 + j] = i * 3 + j;
				vertices[i * 3 + j] = triangles[i][j];
				normals[i * 3 + j] = triangles[i].Normals(j);
				uvs[i * 3 + j] = new Vector2(vertices[i * 3 + j].x, vertices[i * 3 + j].z);
			}
		}

		filter.mesh.SetVertices(vertices);
		filter.mesh.SetTriangles(meshTriangles, 0);
		filter.mesh.SetNormals(normals);
		filter.mesh.SetUVs(1, uvs);

		MeshCollider col = chunk.GetComponent<MeshCollider>();
		if (col)
			col.sharedMesh = filter.mesh;


		//points.Dispose();
		triangles.Dispose();
		triangleIndex.Dispose();
	}

	public void AddEdit(Vector3 position, float radius = 1f, float strength = 1f, bool additive = false)
	{
		AddEdit(new VoxelEdit
		{
			_position = position,
			_radius = radius,
			_strength = strength,
			_additive = additive
		});
	}

	public void AddEdit(VoxelEdit edit)
	{
		_edits.Add(edit);
		Vector3 chunkPos = transform.InverseTransformPoint(edit._position);
		chunkPos.x = Mathf.Round(chunkPos.x / CHUNK_SIZE) * CHUNK_SIZE;
		chunkPos.y = Mathf.Round(chunkPos.y / CHUNK_SIZE) * CHUNK_SIZE;
		chunkPos.z = Mathf.Round(chunkPos.z / CHUNK_SIZE) * CHUNK_SIZE;

		Vector3Int chunkPosInt = new Vector3Int((int)chunkPos.x, (int)chunkPos.y, (int)chunkPos.z);
		for (int x = -1; x <= 1; x++)
			for (int y = -1; y <= 1; y++)
				for (int z = -1; z <= 1; z++)
				{
					Vector3Int vi = chunkPosInt + new Vector3Int(x, y, z) * CHUNK_SIZE;

					if (_chunks.TryGetValue(vi, out GameObject chunk))
						UpdateChunk(chunk);
				}
		UnityEngine.Debug.Log("Edit at: " + edit._position + " with radius: " + edit._radius + ". Additive: " + edit._additive);
	}

	public void OnRecieveEdit(VoxelEdit edit)
	{
		AddEdit(edit);
	}

	public void OnDrawGizmos()
	{
		if (!DEBUG_DRAW_EDIT_GIZMOS)
			return;

		if (!_edits.IsCreated)
			return;

		for (int i = 0; i < _edits.Length; i++)
			Gizmos.DrawWireSphere(_edits[i]._position, _edits[i]._radius);
	}
}

[System.Serializable]
public struct VoxelEdit
{
	public Vector3 _position;
	public float _radius;
	public float _strength;
	public bool _additive;
}
