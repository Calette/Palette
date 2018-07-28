using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FVParticle
{
    public bool isFree = true;
    public Vector3 oldPos = Vector3.zero;
    public Vector3 curPos = Vector3.zero;

    public FVParticle() { }

    public FVParticle(Vector3 vector, bool isFree = true)
    {
        curPos = oldPos = vector;
        this.isFree = isFree;
    }

    public static Vector3 operator -(FVParticle p1, FVParticle p2)
    {
        return p1.curPos - p2.curPos;
    }
}

public class UVComponent : MonoBehaviour
{
    private List<FVParticle> particles = new List<FVParticle>();

    public Vector3 forcedir = Vector3.down;

    public float parRange = 15;

    public float stickDistance = 2;

    public int boxLength = 15;

    public static float stiffness = 1;

    private float sqrt2;

    // Use this for initialization
    void Start()
    {
        sqrt2 = Mathf.Sqrt(2);
        InitParticles();
    }

    // Update is called once per frame
    void Update()
    {
        Velet();
        PositionConstraint();
        DistanceConstraintTest();
    }

    private void InitParticles()
    {
        for(int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                FVParticle p = new FVParticle(new Vector3(i - 9.5f, 0, j - 4.5f));
                particles.Add(p);
            }
        }
        particles[0].isFree = false;
        particles[9].isFree = false;
        particles[199].isFree = false;
        particles[190].isFree = false;
    }

    private void InitBoxHolder()
    {
        for(int i = 0;i< boxLength; i++)
        {
            FVParticle p = new FVParticle(new Vector3(i, -0.5f, 0.5f));
            particles.Add(p);
            p = new FVParticle(new Vector3(i, -0.5f, -0.5f));
            particles.Add(p);
            p = new FVParticle(new Vector3(i, 0.5f, -0.5f));
            particles.Add(p);
            p = new FVParticle(new Vector3(i, 0.5f, 0.5f));
            particles.Add(p);
        }

        particles[0].isFree = particles[1].isFree = particles[2].isFree = particles[3].isFree = false;
    }

    private void DistanceConstraintTest()
    {
        for(int i = 0; i < 20; i++)
        {
            for(int j = 0; j < 10; j++)
            {
                if (i != 19)
                {
                    DistanceConstraint(particles[i * 10 + j], particles[i * 10 + j + 10], stickDistance);
                }

                if(j != 9)
                    DistanceConstraint(particles[i * 10 + j], particles[i * 10 + j + 1], stickDistance);
            }
        }
    }

    private void DistanceConstraintBox()
    {
        for (int sec = 0; sec < boxLength - 1; sec++)
        {
            DistanceConstraint(particles[4 * sec],     particles[4 * sec + 1], stickDistance);
            DistanceConstraint(particles[4 * sec + 1], particles[4 * sec + 2], stickDistance);
            DistanceConstraint(particles[4 * sec + 2], particles[4 * sec + 3], stickDistance);
            DistanceConstraint(particles[4 * sec + 3], particles[4 * sec],     stickDistance);
                                                                               
            DistanceConstraint(particles[4 * sec + 1], particles[4 * sec + 5], stickDistance);
            DistanceConstraint(particles[4 * sec + 2], particles[4 * sec + 6], stickDistance);
            DistanceConstraint(particles[4 * sec + 3], particles[4 * sec + 7], stickDistance);
            DistanceConstraint(particles[4 * sec],     particles[4 * sec + 4], stickDistance);
                                                      
            DistanceConstraint(particles[4 * sec + 1], particles[4 * sec + 3], sqrt2 * stickDistance);
            DistanceConstraint(particles[4 * sec],     particles[4 * sec + 2], sqrt2 * stickDistance);

            DistanceConstraint(particles[4 * sec + 2], particles[4 * sec + 5], sqrt2 * stickDistance);
            DistanceConstraint(particles[4 * sec + 1], particles[4 * sec + 6], sqrt2 * stickDistance);

            DistanceConstraint(particles[4 * sec],     particles[4 * sec + 7], sqrt2 * stickDistance);
            DistanceConstraint(particles[4 * sec + 4], particles[4 * sec + 3], sqrt2 * stickDistance);

            DistanceConstraint(particles[4 * sec + 3], particles[4 * sec + 6], sqrt2 * stickDistance);
            DistanceConstraint(particles[4 * sec + 2], particles[4 * sec + 7], sqrt2 * stickDistance);

            DistanceConstraint(particles[4 * sec],     particles[4 * sec + 5], sqrt2 * stickDistance);
            DistanceConstraint(particles[4 * sec + 1], particles[4 * sec + 4], sqrt2 * stickDistance);
        }

        DistanceConstraint(particles[boxLength * 4 - 4], particles[boxLength * 4 - 3], stickDistance);
        DistanceConstraint(particles[boxLength * 4 - 3], particles[boxLength * 4 - 2], stickDistance);
        DistanceConstraint(particles[boxLength * 4 - 2], particles[boxLength * 4 - 1], stickDistance);
        DistanceConstraint(particles[boxLength * 4 - 1], particles[boxLength * 4 - 4], stickDistance);
                  
        DistanceConstraint(particles[boxLength * 4 - 4], particles[boxLength * 4 - 2], sqrt2 * stickDistance);
        DistanceConstraint(particles[boxLength * 4 - 3], particles[boxLength * 4 - 1], sqrt2 * stickDistance);
    }

    private void Velet()
    {
        float substepTimeSqr = Time.deltaTime * Time.deltaTime;

        //Vector3 objPositon = Vector3.zero;

        for (int i = 0; i < particles.Count; i++)
        {
            if (!particles[i].isFree)
                continue;
            Vector3 vel = particles[i].curPos - particles[i].oldPos;
            Vector3 newPos = particles[i].curPos + vel + (substepTimeSqr * forcedir);
            particles[i].oldPos = particles[i].curPos;
            particles[i].curPos = newPos;
            //objPositon += newPos;
        }

        //transform.position = objPositon / particles.Count;
    }

    private void PositionConstraint()
    {
        float sqrParRange = parRange * parRange;
        for (int i = 0; i < particles.Count; i++)
        {
            //Vector3 velocity = (particles[i].curPos - particles[i].oldPos) / (particles[i].curPos - particles[i].oldPos).magnitude;

            if (particles[i].curPos.sqrMagnitude > sqrParRange)
            {
                particles[i].curPos = particles[i].oldPos;
            }
        }
    }

    public static void DistanceConstraint(FVParticle p1, FVParticle p2, float distance)
    {
        Vector3 delta = p2.curPos - p1.curPos;
        float currentDis = delta.magnitude;
        float ErrorFactor = (currentDis - distance) / currentDis;
        if (p1.isFree && p2.isFree)
        {
            p1.curPos += ErrorFactor * 0.5f * delta * stiffness;
            p2.curPos -= ErrorFactor * 0.5f * delta * stiffness;
        }
        else if (p1.isFree)
        {
            p1.curPos += ErrorFactor * delta * stiffness;
        }
        else if (p2.isFree)
        {
            p2.curPos -= ErrorFactor * delta * stiffness;
        }
    }

    public static void AngleConstraint(FVParticle p0, FVParticle p1, FVParticle p2, float angle)
    {
        float a = (p1 - p0).magnitude;
        float b = (p2 - p0).magnitude;
        float distance = Mathf.Sqrt(a * a + b * b - 2 * a * b * Mathf.Cos(angle));
        if ((p1 - p2).magnitude < distance)
            DistanceConstraint(p1, p2, distance);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;
        Gizmos.DrawWireSphere(Vector3.zero, parRange);
        for (int i = 0; i < particles.Count; i++)
        {
            Gizmos.DrawSphere(particles[i].curPos, 0.2f);
        }

        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                if (i != 19)
                    Gizmos.DrawLine(particles[i * 10 + j].curPos, particles[i * 10 + j + 10].curPos);

                if (j != 9)
                    Gizmos.DrawLine(particles[i * 10 + j].curPos, particles[i * 10 + j + 1].curPos);
            }
        }

        /*
        for (int i = 0; i < boxLength - 1; i++)
        {
            Gizmos.DrawLine(particles[i * 4].curPos, particles[i * 4 + 1].curPos);
            Gizmos.DrawLine(particles[i * 4 + 1].curPos, particles[i * 4 + 2].curPos);
            Gizmos.DrawLine(particles[i * 4 + 2].curPos, particles[i * 4 + 3].curPos);
            Gizmos.DrawLine(particles[i * 4 + 3].curPos, particles[i * 4].curPos);
        }

        Gizmos.DrawLine(particles[boxLength * 4 - 4].curPos, particles[boxLength * 4 - 3].curPos);
        Gizmos.DrawLine(particles[boxLength * 4 - 3].curPos, particles[boxLength * 4 - 2].curPos);
        Gizmos.DrawLine(particles[boxLength * 4 - 2].curPos, particles[boxLength * 4 - 1].curPos);
        Gizmos.DrawLine(particles[boxLength * 4 - 1].curPos, particles[boxLength * 4 - 4].curPos);
        //*/
    }
}
