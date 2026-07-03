using UnityEngine;
struct Particle
{
    public Vector3 position;
    public Vector3 lastPosition;
    public Vector3 velocity;

    public Vector3 acceleration;

    public float density;
    public float preasure;

    public float nearDensity;
    public float nearPreasure;
}
