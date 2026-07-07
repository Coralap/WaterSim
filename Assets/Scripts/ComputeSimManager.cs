using System;
using UnityEngine;

public class ComputeSimManager : MonoBehaviour
{
    [Header("Compute")]
    [SerializeField]
    private ComputeShader particleComputeShader;
    private ComputeBuffer particleBuffer;
    private ComputeBuffer positionBuffer;

    private ComputeBuffer colorBuffer;

    private ComputeBuffer cellStartsBuffer;

    private ComputeBuffer flatCellBuffer;

    private ComputeBuffer IdToCellBuffer;

    [Header("Constants")]
    [SerializeField]
    private float timeStep= 0.3f;

    [SerializeField]
    private float gravityForce= -9.81f;
    [SerializeField]
    [Range(0f,1f)]
    private float dampingForce = 0.5f;

    private float smoothingRadius=5f;

    [SerializeField]
    float targetDensity=10f;

    [SerializeField] float nearPreasureMultiplier = 15f;
    [SerializeField]
    float preasureMultiplier=1500f;

    [SerializeField]
    float viscosityStrength = 0.2f; 

    [Header("Render Settings")]
    [SerializeField]
    float maxSpeed;
    [SerializeField]

    private Material particleMaterial;
    [SerializeField]
    private Mesh particleMesh;
    [SerializeField] private float particleRadius;

    [Header("Area Settings")]
    [SerializeField]
    private float areaWidth = 200f;

    [SerializeField]
    private float aeraHeight = 100f;

    float blockWidth;  
    float blockHeight;  
    float halfHeight;
    float halfWidth;
    [Range(2, 1048576)] 
    [SerializeField] private int particleCount = 1024;

    private Particle[] particles;
    private Vector3[] positions;
    private Vector4[] colors;

    private int[] cellStartsArray;

    private Vector2Int[] flatCellArray;

    private int[] IdToCellArray;

    int densityKernel;
    int gravityKernel;
    
    int relaxKernel;

    int simKernel;
    int updateCellsKernel;


    void Start()
    {
        //initialize buffers with size of particles.
        particleBuffer = new ComputeBuffer(particleCount,52);
        positionBuffer = new ComputeBuffer(particleCount,12);

        colorBuffer = new ComputeBuffer(particleCount,16);

        smoothingRadius = particleRadius*6;

        flatCellBuffer = new ComputeBuffer(particleCount,sizeof(int)*2);
        IdToCellBuffer = new ComputeBuffer(particleCount,sizeof(int));

        int Cellcolumns = Mathf.CeilToInt(areaWidth / smoothingRadius);
        int Cellrows = Mathf.CeilToInt(aeraHeight / smoothingRadius);
        int totalCells = Cellcolumns * Cellrows;
        cellStartsBuffer = new ComputeBuffer(totalCells,sizeof(int));

        cellStartsArray = new int[totalCells];
        flatCellArray = new Vector2Int[particleCount];
        IdToCellArray = new int[particleCount];


        particleComputeShader.SetInt("_particleCount",particleCount);

        halfHeight = aeraHeight/2;
        halfWidth = areaWidth/2;
        particles = new Particle[particleCount];
        positions = new Vector3[particleCount];
        colors = new Vector4[particleCount];
        int i = 0;


        int columns = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
        int rows = Mathf.CeilToInt((float)particleCount / columns);

        float initialSpacing = particleRadius * 2f; 
        blockWidth = columns * initialSpacing;
        blockHeight = rows * initialSpacing;

        float spacingX = blockWidth / Mathf.Max(1, columns - 1);
        float spacingY = blockHeight / Mathf.Max(1, rows - 1);

        float startX = -blockWidth / 2f;
        float startY = -blockHeight / 2f;

        for (int col = 0; col < columns; col++)
        {
            for (int row = 0; row < rows; row++)
            {
                if (i >= particleCount) break;

                float posX = startX + (col * spacingX);
                float posY = startY + (row * spacingY);
                Vector3 newPos = new Vector3(posX, posY, 0f);
                particles[i] = new Particle
                {
                    position = newPos,
                    velocity = Vector3.zero,
                    density = 0f,
                    preasure = 0f,
                    nearDensity = 0f,
                    nearPreasure = 0f,
                    lastPosition = newPos
                };
                positions[i] = particles[i].position;

                IdToCellArray[i] =0;
                flatCellArray[i] = new Vector2Int(0,i);


                i++;
            }
        }
        //handle leftovers
        while (i < particleCount)
        {
            IdToCellArray[i] =i;
            flatCellArray[i] = new Vector2Int(0,i);
            particles[i] = particles[0];
            positions[i] = positions[0];
            i++;
        }

        for(int j = 0; j < cellStartsArray.Length; j++)
        {
            cellStartsArray[j] = 0;
        }
        //set buffer data and compute kernel id's
        cellStartsBuffer.SetData(cellStartsArray);
        IdToCellBuffer.SetData(IdToCellArray);
        flatCellBuffer.SetData(flatCellArray);

        particleBuffer.SetData(particles);
        positionBuffer.SetData(positions);
        colorBuffer.SetData(colors);

        densityKernel = particleComputeShader.FindKernel("CalculateDensity");


        gravityKernel = particleComputeShader.FindKernel("ApplyGravity");
        
        relaxKernel = particleComputeShader.FindKernel("DensityRelaxationDisplacement");


        simKernel = particleComputeShader.FindKernel("SimulationStep");

        updateCellsKernel = particleComputeShader.FindKernel("UpdateCells");



        particleComputeShader.SetBuffer(simKernel, "colors", colorBuffer);



        particleComputeShader.SetBuffer(densityKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(gravityKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(simKernel, "particles", particleBuffer);



        particleComputeShader.SetBuffer(densityKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(densityKernel, "IdToCell", IdToCellBuffer);

        particleComputeShader.SetBuffer(relaxKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "IdToCell", IdToCellBuffer);

        particleComputeShader.SetBuffer(simKernel, "positions", positionBuffer);



        particleComputeShader.SetBuffer(updateCellsKernel, "positions", positionBuffer);
        particleComputeShader.SetBuffer(updateCellsKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(updateCellsKernel, "IdToCell", IdToCellBuffer);

        }

    // Update is called once per frame
    void Update()
    {
    halfHeight = aeraHeight / 2;
    halfWidth = areaWidth / 2;
    int Cellcolumns = Mathf.CeilToInt(areaWidth / smoothingRadius);
    int Cellrows = Mathf.CeilToInt(aeraHeight / smoothingRadius);
    particleComputeShader.SetInt("_gridWidth", Cellcolumns);
    particleComputeShader.SetInt("_gridHeight", Cellrows);
    particleComputeShader.SetFloat("_nearPreasureMultiplier", nearPreasureMultiplier);
        particleComputeShader.SetFloat("_smoothingRadius",smoothingRadius);
        particleComputeShader.SetFloat("_targetDensity",targetDensity);
        particleComputeShader.SetFloat("_viscosityStrength",viscosityStrength);
        particleComputeShader.SetFloat("_preasureMultiplier",preasureMultiplier);
        if (timeStep <= 0)
        {
            particleComputeShader.SetFloat("_deltaTime",Time.deltaTime);

        }
        else
        {
            particleComputeShader.SetFloat("_deltaTime",timeStep);

        }
        particleComputeShader.SetFloat("_dampingForce",dampingForce);
        particleComputeShader.SetFloat("_gravityForce",gravityForce);
        particleComputeShader.SetFloat("_particleRadius",particleRadius);
        particleComputeShader.SetFloat("_halfHeight",halfHeight);
        particleComputeShader.SetFloat("_halfWidth",halfWidth);

        particleComputeShader.SetFloat("_maxSpeed",maxSpeed);


        int groups = Mathf.CeilToInt(particleCount / 64f);


        particleComputeShader.SetBuffer(densityKernel, "cellStarts", cellStartsBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "cellStarts", cellStartsBuffer);

        particleComputeShader.Dispatch(gravityKernel,groups,1,1);
        particleComputeShader.Dispatch(updateCellsKernel,groups,1,1);
        OrganzieCells();

        particleComputeShader.Dispatch(densityKernel,groups,1,1);

        particleComputeShader.Dispatch(relaxKernel,groups,1,1);

        particleComputeShader.Dispatch(simKernel,groups,1,1);
        particleMaterial.SetBuffer("colorBuffer", colorBuffer);

        particleMaterial.SetBuffer("particleBuffer", positionBuffer);
        particleMaterial.SetFloat("_Radius", particleRadius);
        Bounds simulationBounds = new Bounds(Vector3.zero, new Vector3(areaWidth, aeraHeight, 10f));
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, simulationBounds, particleCount);
        

    }
    //bitonic sort for the cells
    void OrganzieCells()
    {
    int totalLength = particleCount;
    int sortHandle = particleComputeShader.FindKernel("SortCells");
    
    particleComputeShader.SetBuffer(sortHandle, "flatCell", flatCellBuffer);
    particleComputeShader.SetBuffer(sortHandle, "IdToCell", IdToCellBuffer);

    Vector2Int[] result = new Vector2Int[totalLength];
    particleComputeShader.SetInt("_len", totalLength);
        for (int stage = 2; stage <= totalLength; stage *= 2)
        {
            particleComputeShader.SetInt("_stage", stage);

            for (int k = stage / 2; k > 0; k /= 2)
            {
                particleComputeShader.SetInt("_k", k);                
                int threadGroups = Mathf.CeilToInt(totalLength / 2f / 64f);
                particleComputeShader.Dispatch(sortHandle, threadGroups, 1, 1);
            }
        }
        flatCellBuffer.GetData(result);
        for (int i = 0; i < cellStartsArray.Length; i++) {
        cellStartsArray[i] = -1; 
        }
        int last_seen=-1;

        for(int i = 0; i <totalLength; i++)
        {
            int key = result[i].x;
            if (key == last_seen)
                continue;
            last_seen = key;
            if (key >= 0 && key < cellStartsArray.Length) {
            cellStartsArray[key] = i;
            }
        }

        cellStartsBuffer.SetData(cellStartsArray);
    }




    void OnDestroy()
    {
        particleBuffer?.Dispose();
        colorBuffer?.Dispose(); 
        positionBuffer?.Dispose();

        IdToCellBuffer?.Dispose();
        flatCellBuffer?.Dispose();
        cellStartsBuffer?.Dispose();
    }
    private void OnValidate()
    {
        particleCount = Mathf.ClosestPowerOfTwo(particleCount);
    }
}
