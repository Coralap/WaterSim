using System;
using UnityEngine;

public class ComputeSimManager : MonoBehaviour
{
    public Camera sceneCamera;
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
    private int subSteps= 3;
    [SerializeField]
    private float gravityForce= -9.81f;
    [SerializeField]
    [Range(0f,1f)]
    private float dampingForce = 0.5f;

    private float smoothingRadius=5f;

    [SerializeField]
    private float smoothingRadiusMultiplier= 3f;
    [SerializeField]
    float targetDensity=10f;

    [SerializeField] float nearPreasureMultiplier = 15f;
    [SerializeField]
    float preasureMultiplier=1500f;

    [SerializeField]
    float viscositySigmoid = 0.2f; 

    [SerializeField]
    float viscosityBeta = 0.2f; 
    [Header("Render Settings")]
    [SerializeField]
    float maxSpeed;
    [SerializeField]

    private Material particleMaterial;
    [SerializeField]
    private Mesh particleMesh;
    [SerializeField] private float particleRadius;

    [Header("Area Settings")]
    private float areaWidth;

    private float aeraHeight;

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
    int viscosityKernel;

    void Start()
    {
        aeraHeight = sceneCamera.orthographicSize*2;
        areaWidth = aeraHeight * 16.0f/9.0f;
        //initialize buffers with size of particles.
        particleBuffer = new ComputeBuffer(particleCount,52);
        positionBuffer = new ComputeBuffer(particleCount,12);

        colorBuffer = new ComputeBuffer(particleCount,16);

        smoothingRadius = particleRadius*smoothingRadiusMultiplier;

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

halfHeight = aeraHeight / 2;
        halfWidth = areaWidth / 2;
        particles = new Particle[particleCount];
        positions = new Vector3[particleCount];
        colors = new Vector4[particleCount];
        
        int i = 0;

        int halfCount = particleCount / 2;

        int blockColumns = Mathf.CeilToInt(Mathf.Sqrt(halfCount));
        int blockRows = Mathf.CeilToInt((float)halfCount / blockColumns);

        float initialSpacing = particleRadius * 2.1f; 

        float blockWidth = blockColumns * initialSpacing;
        float blockHeight = blockRows * initialSpacing;

        float leftStartX = -halfWidth + particleRadius;
        float rightStartX = halfWidth - blockWidth - particleRadius;
        float startY = -halfHeight + particleRadius;

        for (int p = 0; p < particleCount; p++)
        {
            bool isLeftSide = p < halfCount;
            int localIndex = isLeftSide ? p : p - halfCount;

            int col = localIndex % blockColumns;
            int row = localIndex / blockColumns;

            float posX = (isLeftSide ? leftStartX : rightStartX) + (col * initialSpacing);
            float posY = startY + (row * initialSpacing);

            // random movment
            posX += UnityEngine.Random.Range(-0.02f, 0.02f);
            posY += UnityEngine.Random.Range(-0.02f, 0.02f);

            Vector3 newPos = new Vector3(posX, posY, 0f);

            particles[p] = new Particle
            {
                position = newPos,
                velocity = Vector3.zero,
                density = 0f,
                preasure = 0f,
                nearDensity = 0f,
                nearPreasure = 0f,
                lastPosition = newPos
            };
            positions[p] = particles[p].position;

            IdToCellArray[p] = 0;
            flatCellArray[p] = new Vector2Int(0, p);
        }
        i = particleCount;
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
        viscosityKernel = particleComputeShader.FindKernel("ApplyViscosity");



        particleComputeShader.SetBuffer(simKernel, "colors", colorBuffer);



        particleComputeShader.SetBuffer(densityKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(gravityKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(simKernel, "particles", particleBuffer);
        particleComputeShader.SetBuffer(viscosityKernel, "particles", particleBuffer);



        particleComputeShader.SetBuffer(densityKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(densityKernel, "IdToCell", IdToCellBuffer);

        particleComputeShader.SetBuffer(relaxKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "IdToCell", IdToCellBuffer);

        particleComputeShader.SetBuffer(viscosityKernel, "flatCell", flatCellBuffer);
        particleComputeShader.SetBuffer(viscosityKernel, "IdToCell", IdToCellBuffer);

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
        particleComputeShader.SetFloat("_viscositySigmoid",viscositySigmoid);
        particleComputeShader.SetFloat("_viscosityBeta",viscosityBeta);
        particleComputeShader.SetFloat("_preasureMultiplier",preasureMultiplier);

        particleComputeShader.SetFloat("_dampingForce",dampingForce);
        particleComputeShader.SetFloat("_gravityForce",gravityForce);
        particleComputeShader.SetFloat("_particleRadius",particleRadius);
        particleComputeShader.SetFloat("_halfHeight",halfHeight);
        particleComputeShader.SetFloat("_halfWidth",halfWidth);

        particleComputeShader.SetFloat("_maxSpeed",maxSpeed);


        int groups = Mathf.CeilToInt(particleCount / 64f);


        particleComputeShader.SetBuffer(densityKernel, "cellStarts", cellStartsBuffer);
        particleComputeShader.SetBuffer(relaxKernel, "cellStarts", cellStartsBuffer);
        particleComputeShader.SetBuffer(viscosityKernel, "cellStarts", cellStartsBuffer);
        float subDeltaTime = timeStep / subSteps;
        particleComputeShader.SetFloat("_deltaTime", subDeltaTime);

        for (int step = 0; step < subSteps; step++)
        {
            particleComputeShader.Dispatch(gravityKernel, groups, 1, 1);
            particleComputeShader.Dispatch(viscosityKernel, groups, 1, 1);
            
            particleComputeShader.Dispatch(updateCellsKernel, groups, 1, 1);
            OrganzieCells(); 
            
            particleComputeShader.Dispatch(densityKernel, groups, 1, 1);
            particleComputeShader.Dispatch(relaxKernel, groups, 1, 1);
            particleComputeShader.Dispatch(simKernel, groups, 1, 1);
        }

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
