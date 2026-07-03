using System;
using UnityEngine;

public class SimManager : MonoBehaviour
{
    
    [Header("Constants")]

    [SerializeField]
    private float gravityForce= -9.81f;
    [SerializeField]
    [Range(0f,1f)]
    private float dampingForce = 0.5f;

    [SerializeField]
    private float smoothingRadius=5f;

    [SerializeField]
    float targetDensity=10f;
    [SerializeField]
    float preasureMultiplier=1500f;

    [SerializeField]
    float viscosityStrength = 0.2f; 
    
    [Header("Render Settings")]
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

    float halfHeight;
    float halfWidth;
    [SerializeField]
    private int particleCount = 1000;

    private Particle[] particles;
    private Matrix4x4[] particleMatrices;
    
    void Start()
    {
        halfHeight = aeraHeight/2;
        halfWidth = areaWidth/2;
        particles = new Particle[particleCount];
        particleMatrices = new Matrix4x4[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            particles[i] = new Particle
            {
                position = new Vector3(
                    UnityEngine.Random.Range(-halfWidth, halfWidth),
                    UnityEngine.Random.Range(-halfHeight, halfHeight),
                    0
                ),
                velocity = Vector3.zero,
                acceleration = Vector3.zero,
                density = 0f,
                preasure =0f
            };
            Vector3 size = Vector3.one;
            size *= 2*particleRadius;
            particleMatrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity,size);
            
        }

    }

    // Update is called once per frame
    void Update()
    {
    halfHeight = aeraHeight / 2;
    halfWidth = areaWidth / 2;

    // dens and preasure
    for (int i = 0; i < particleCount; i++)
    {
        particles[i].density = CalculateDensity(ref particles[i]);
        particles[i].preasure = Math.Max(0, preasureMultiplier * (particles[i].density - targetDensity));
    }

    // forces
    for (int i = 0; i < particleCount; i++)
    {
        Vector3 preasureForce = CalculatePreasureForce(i, ref particles[i]);
        preasureForce = Vector3.ClampMagnitude(preasureForce, 1000f);
        
        
        particles[i].acceleration = preasureForce; 
    }
    Vector3 mousePos = Input.mousePosition;
    // Gravity and movement
    for (int i = 0; i < particleCount; i++)
    {
        particles[i].acceleration.y += gravityForce;
        particles[i].velocity += particles[i].acceleration * Time.deltaTime;
        particles[i].position += particles[i].velocity * Time.deltaTime;

        // Boundary Collisions
        if (Math.Abs(particles[i].position.y) + particleRadius > halfHeight)
        {
            particles[i].position.y = Math.Sign(particles[i].position.y) * (halfHeight - particleRadius);
            particles[i].velocity.y *= -dampingForce;
        }
        if (Math.Abs(particles[i].position.x) + particleRadius > halfWidth)
        {
            particles[i].position.x = Math.Sign(particles[i].position.x) * (halfWidth - particleRadius);
            particles[i].velocity.x *= -dampingForce;
        }

        // Update Matrix
        Vector3 size = Vector3.one * 2 * particleRadius;
        particleMatrices[i] = Matrix4x4.TRS(particles[i].position, Quaternion.identity, size);
    }

    Graphics.DrawMeshInstanced(particleMesh, 0, particleMaterial, particleMatrices, particleCount);
    }



    float CalculateDensity(ref Particle p)
        {
            float densitySum = 0f;

            for (int i = 0; i < particleCount; i++)
            {


                float dist = Vector2.Distance(particles[i].position, p.position);
                
                if (dist > smoothingRadius) 
                    continue;
                densitySum += DensityKernelFunction(dist);
            }
            return densitySum;
        }

    //using the Poly6 Kernel
    float DensityKernelFunction(float distance)
    {

        float h2 = smoothingRadius * smoothingRadius;
        float r2 = distance * distance;
        float diff = h2 - r2;
        float scaleFactor = 4.0f/(((float)Math.PI)*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius);
        return diff * diff * diff*scaleFactor;


    }

    float PreasureKernelFunction(float distance)
    {
        float scaleFactor = 30.0f/(((float)Math.PI)*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius*smoothingRadius);

       return (smoothingRadius-distance)*  (smoothingRadius-distance) * scaleFactor;

    }

    Vector3 CalculatePreasureForce(int particleIndex, ref Particle p)
    {
        Vector2 forceSum = Vector2.zero;
        for (int i = 0; i < particleCount; i++)
            {
                if (i == particleIndex) 
                    continue;

                float dist = Vector2.Distance(particles[i].position, p.position);
                
                if (dist > smoothingRadius) 
                    continue;
                Vector2 dir;
                if (dist == 0)
                {
                    dist = 0.001f;
                    dir = new Vector2(UnityEngine.Random.Range(-1f,1f),UnityEngine.Random.Range(-1f,1f));
                }
                else
                {
                dir = (p.position - particles[i].position)/dist;

                }

                //Pcurr/densityCurr^2 + Pother/DensityOther^2
                float safeDens = Math.Max(p.density,targetDensity);
                float safeOtherDens =Math.Max(particles[i].density,targetDensity);
                float mag = p.preasure/(safeDens*safeDens) + particles[i].preasure/(safeOtherDens*safeOtherDens);
                float pressureKernelValue = PreasureKernelFunction(dist);
                forceSum += mag * pressureKernelValue * dir;
                Vector2 relativeVelocity = particles[i].velocity - p.velocity;
                forceSum += pressureKernelValue * viscosityStrength * relativeVelocity;
            }
        return forceSum;
    }

}
