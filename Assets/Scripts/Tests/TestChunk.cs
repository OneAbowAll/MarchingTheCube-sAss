﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestChunk : MonoBehaviour
{
    public float surfaceLevel = 0;
    public Vector3Int chunkSize = Vector3Int.zero;
    public Vector3Int currentPosition = Vector3Int.zero;
    public Vector3Int worldScale = Vector3Int.zero;
    public ComputeShader shader;
    public NoiseSettings noise;

    //Generation values
    float[] voxelValues;

    //Mesh data
    Mesh mesh;
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();

    MeshFilter meshFilter;
    MeshCollider meshCollider;

    //Chunk management
    bool isActive;

    int kernel;
    int numMaxTri;

    #region Setters / Getters

    public Vector3Int CurrentPosition
    {
        get => currentPosition;
        set
        {
            transform.position = currentPosition = value;

            if (voxelValues != null)
                GenerateValueGrid();
        }
    }

    public bool IsActive
    {
        get => isActive;
        set
        {
            isActive = value;
            if (isActive)
            {
                if (mesh != null)
                    GenerateMesh();
            }
            else
                UpdateSharedMesh(null);
        }
    }

    #endregion

    void Awake()
    {
        //Initialize chunk
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
    }

    void Start()
    {
        CurrentPosition = currentPosition;
        IsActive = true;

        mesh = new Mesh();
        voxelValues = new float[chunkSize.x * chunkSize.y * chunkSize.z];
        kernel = shader.FindKernel("MarchCube");


        int numVoxelsPerAxis = chunkSize.x - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        numMaxTri = numVoxels * 5;

        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GenerateValueGrid();
    }

    void Update()
    {
        GenerateMesh();
    }

    public void Initialize(float surfaceLevel, Vector3Int chunkSize, Vector3Int currentPosition, Vector3Int worldScale, NoiseSettings noise, ComputeShader shader)
    {
        this.surfaceLevel = surfaceLevel;
        this.chunkSize = chunkSize;
        this.worldScale = worldScale;
        this.noise = noise;
        this.shader = shader;

        CurrentPosition = currentPosition;
        IsActive = true;

        mesh = new Mesh();
        voxelValues = new float[chunkSize.x * chunkSize.y * chunkSize.z];
        kernel = shader.FindKernel("MarchCube");


        int numVoxelsPerAxis = chunkSize.x - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        numMaxTri = numVoxels * 5;

        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        GenerateValueGrid();
    }

    #region Chunk Generation

    void GenerateValueGrid()
    {
        for (int x = 0; x < chunkSize.x; x++)
        {
            for (int y = 0; y < chunkSize.y; y++)
            {
                for (int z = 0; z < chunkSize.z; z++)
                {
                    //{
                    //    float nX = (x + currentPosition.x) / (worldScale.x * (chunkSize.x - 1f));
                    //    float nY = (y + currentPosition.y) / (worldScale.y * (chunkSize.y - 1f));
                    //    float nZ = (z + currentPosition.z) / (worldScale.z * (chunkSize.z - 1f));

                    //    voxelValues[(int)(x + y * chunkSize.x + z * chunkSize.x * chunkSize.y)] = y + noise.Generate(new Vector3(nX, nY, nZ));
                    voxelValues[(int)(x + y * chunkSize.x + z * chunkSize.x * chunkSize.y)] = y;
                }
            }
        }
    }

    public void GenerateMesh()
    {
        mesh.Clear();
        triangles.Clear();

        int nGroupThread = Mathf.CeilToInt((chunkSize.x ) / 8);

        ComputeBuffer triBuffer = new ComputeBuffer(numMaxTri, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        ComputeBuffer triCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer pointBuffer = new ComputeBuffer(voxelValues.Length, sizeof(float));
        pointBuffer.SetData(voxelValues);

        triBuffer.SetCounterValue(0);
        shader.SetInt("numPointsPerAxis", (int)chunkSize.x);
        shader.SetFloat("isoLevel", surfaceLevel);
        shader.SetBuffer(kernel, "triangles", triBuffer);
        shader.SetBuffer(kernel, "cubeValues", pointBuffer);
        shader.Dispatch(0, nGroupThread, nGroupThread, nGroupThread);

        ComputeBuffer.CopyCount(triBuffer, triCountBuffer, 0);

        int[] triCountArray = { 0 };
        triCountBuffer.GetData(triCountArray);
        int numTris = triCountArray[0];

        Triangle[] tris = new Triangle[numTris];
        triBuffer.GetData(tris, 0, 0, numTris);

        var meshVertices = new Vector3[numTris * 3];
        var meshTriangles = new int[numTris * 3];

        for (int i = 0; i < numTris; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                meshVertices[i * 3 + j] = tris[i][j];
                triangles.Add(i * 3 + j);
            }
        }

        triBuffer.Release();
        triCountBuffer.Release();
        pointBuffer.Release();

        mesh.vertices = meshVertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        UpdateSharedMesh(mesh);
    }

    #endregion

    #region Chunk Management

    public void RefreshChunk()
    {
        GenerateValueGrid();
        GenerateMesh();
    }

    void UpdateSharedMesh(Mesh mesh)
    {
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }

    #endregion

    #region Debug

    void DebugChunk()
    {
        Debug.Log("Vertices in chunk: " + vertices.Count);
        Debug.Log("Voxels in chunk: " + voxelValues.Length);
        Debug.Log("Chunk size: " + chunkSize);
    }

    #endregion 
}