using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterProduct : MonoBehaviour {

    public Shader waterShader;
    public float unitWidth = 1f;
    public int resolution = 256;

    public float choppiness = 1f;
    public float length = 1f;

    private Mesh mesh;
    private MeshFilter filter;
    private Material waterMat;

    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uvs;
    private int[] indices;

    private Vector3[] newVertices;

    public float timer = 0f;

    private const float PI = 3.1415926536f;

    private const float G = 9.81f;

    private const float EPSILON = 0.0001f;

    // Use this for initialization
    void Start () {
        GameObject water = new GameObject("water");
        filter = water.AddComponent<MeshFilter>();
        waterMat = new Material(waterShader);
        water.AddComponent<MeshRenderer>().material = waterMat;
        mesh = new Mesh();
        filter.mesh = mesh;
        SetParams();
        GenerateMesh();
    }

    void SetParams()
    {
        vertices = new Vector3[resolution * resolution];
        indices = new int[(resolution - 1) * (resolution - 1) * 6];
        normals = new Vector3[resolution * resolution];
        uvs = new Vector2[resolution * resolution];

        vertConj = new Vector2[resolution * resolution];
        verttilde = new Vector2[resolution * resolution];
        newVertices = new Vector3[resolution * resolution];
    }
	
    void GenerateMesh()
    {
        int indiceCount = 0;
        int halfResolution = resolution / 2;
        for (int i = 0; i < resolution; i++)
        {
            float horizontalPosition = (i - halfResolution) * unitWidth;
            for (int j = 0; j < resolution; j++)
            {
                int currentIdx = i * (resolution) + j;
                float verticalPosition = (j - halfResolution) * unitWidth;
                vertices[currentIdx] = new Vector3(horizontalPosition + (resolution % 2 == 0 ? unitWidth / 2f : 0f), 0f, verticalPosition + (resolution % 2 == 0 ? unitWidth / 2f : 0f));
                normals[currentIdx] = new Vector3(0f, 1f, 0f);
                uvs[currentIdx] = new Vector2(i * 1.0f / (resolution - 1), j * 1.0f / (resolution - 1));

                if (j == resolution - 1)
                    continue;

                if (i != resolution - 1)
                {
                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx + 1;
                    indices[indiceCount++] = currentIdx + resolution;
                }
                if (i != 0)
                {
                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx - resolution + 1;
                    indices[indiceCount++] = currentIdx + 1;
                }


                verttilde[currentIdx] = htilde0(i, j);
                Vector2 temp = htilde0(resolution - i, resolution - j);
                vertConj[currentIdx] = new Vector2(temp.x, -temp.y);

            }
        }
        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.normals = normals;
        mesh.uv = uvs;
        filter.mesh = mesh;
    }

	void Update () {
        EvaluateWaves(timer);
    }

    // 色散关系 Dispersion Relation
    // w = ±√gk
    private float Dispersion(int n, int m)
    {
        float w = 2 * PI / length;

        // k的值(kx, ky) = (2PI * n / Lx, 2PI * n / Lz)
        // kx和kz ∈ (-PI * N / L, PI * N / L)
        float kx = PI * (2 * n - resolution) / length;
        float kz = PI * (2 * m - resolution) / length;

        // k的模 = Mathf.Sqrt(kx * kx + kz * kz)
        // √gk  = Mathf.Sqrt(G * Mathf.Sqrt(kx * kx + kz * kz))
        // Floor(w' / w) * w 是为了让w取值在(-w, w)

        return Mathf.Floor(Mathf.Sqrt(G * Mathf.Sqrt(kx * kx + kz * kz)) / w) * w;
    }

    public Vector2 wind = new Vector2(1f, 1f);
    public float amplitude = 1f;

    private Vector2[] verttilde;
    private Vector2[] vertConj;


    private float Phillips(int n, int m)
    {
        //k的值(kx, ky) = (2PI* n / Lx, 2PI* n / Lz)
        Vector2 k = new Vector2((2 * n - resolution) / length * PI, (2 * m - resolution) / length * PI);
        float k_length = k.magnitude;
        if (k_length < EPSILON)
            return 0.0f;
        float k_length2 = k_length * k_length;
        float k_length4 = k_length2 * k_length2;


        float kDotW = Vector2.Dot(k.normalized, wind.normalized);
        float kDotW2 = kDotW * kDotW;
        float w_length = wind.magnitude;
        float l = w_length * w_length / G;
        float l2 = l * l;
        float damping = 0.001f;
        float L2 = l2 * damping * damping;
        return amplitude * Mathf.Exp(-1f / (k_length2 * l2)) / k_length4 * kDotW2 * Mathf.Exp(-k_length2 * L2);
    }

    // h0 = (random1 + i * random2) * √(P(k) / 2))
    private Vector2 htilde0(int n, int m)
    {
        Vector2 r = new Vector2();
        float z1 = Random.value;
        float z2 = Random.value;
        r.x = Mathf.Sqrt(-2f * Mathf.Log(z1)) * Mathf.Cos(2 * PI * z2);
        r.y = Mathf.Sqrt(-2f * Mathf.Log(z1)) * Mathf.Sin(2 * PI * z2);
        return r * Mathf.Sqrt(Phillips(n, m) / 2f);
    }

    // ▽h = 

    // h(k, t) = h0 * exp{i * w(k) * t} + h0 * exp{-i * w(k) * t}
    private Vector2 htilde(float t, int n, int m)
    {
        int index = n * resolution + m;
        Vector2 h0 = verttilde[index];
        Vector2 h0conj = vertConj[index];
        float omegat = Dispersion(n, m) * t;
        float _cos = Mathf.Cos(omegat);
        float _sin = Mathf.Sin(omegat);
        Vector2 c0 = new Vector2(_cos, _sin);
        Vector2 c1 = new Vector2(_cos, -_sin);
        Vector2 res = new Vector2(h0.x * c0.x - h0.y * c0.y + h0conj.x * c1.x - h0conj.y * c1.y, h0.x * c0.y + h0.y * c0.x + h0conj.x * c1.y + h0conj.y * c1.x);
        return res;
    }

    private Vector3 Displacement(Vector2 x, float t, out Vector3 nor)
    {
        Vector2 h = new Vector2(0f, 0f);
        Vector2 d = new Vector2(0f, 0f);
        Vector3 n = Vector3.zero;
        Vector2 c, htilde_c, k;
        float kx, kz, k_length, kDotX;
        for (int i = 0; i < resolution; i++)
        {
            kx = 2 * PI * (i - resolution / 2.0f) / length;
            for (int j = 0; j < resolution; j++)
            {
                kz = 2 * PI * (j - resolution / 2.0f) / length;
                k = new Vector2(kx, kz);
                k_length = k.magnitude;
                kDotX = Vector2.Dot(k, x);
                c = new Vector2(Mathf.Cos(kDotX), Mathf.Sin(kDotX));
                Vector2 temp = htilde(t, i, j);
                htilde_c = new Vector2(temp.x * c.x - temp.y * c.y, temp.x * c.y + temp.y * c.x);
                h += htilde_c;
                n += new Vector3(-kx * htilde_c.y, 0f, -kz * htilde_c.y);
                if (k_length < EPSILON)
                    continue;
                d += new Vector2(kx / k_length * htilde_c.y, -kz / k_length * htilde_c.y);
            }
        }
        nor = Vector3.Normalize(Vector3.up - n);
        return new Vector3(d.x, h.x, d.y);
    }

    private void EvaluateWaves(float t)
    {
        timer += Time.deltaTime;

        Vector2[] hds = new Vector2[resolution * resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                newVertices[index] = vertices[index];
            }
        }
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                Vector3 nor = new Vector3(0f, 0f, 0f);
                Vector3 hd = Displacement(new Vector2(newVertices[index].x, newVertices[index].z), t, out nor);
                newVertices[index].y = hd.y;
                newVertices[index].z = vertices[index].z - hd.z * choppiness;
                newVertices[index].x = vertices[index].x - hd.x * choppiness;
                normals[index] = nor;
                hds[index] = new Vector2(hd.x, hd.z);
            }
        }

        Color[] colors = new Color[resolution * resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                Vector2 dDdx = Vector2.zero;
                Vector2 dDdy = Vector2.zero;
                if (i != resolution - 1)
                {
                    dDdx = 0.5f * (hds[index] - hds[index + resolution]);
                }
                if (j != resolution - 1)
                {
                    dDdy = 0.5f * (hds[index] - hds[index + 1]);
                }
                float jacobian = (1 + dDdx.x) * (1 + dDdy.y) - dDdx.y * dDdy.x;
                Vector2 noise = new Vector2(Mathf.Abs(normals[index].x), Mathf.Abs(normals[index].z)) * 0.3f;
                float turb = Mathf.Max(1f - jacobian + noise.magnitude, 0f);
                float xx = 1f + 3f * Mathf.SmoothStep(1.2f, 1.8f, turb);
                xx = Mathf.Min(turb, 1.0f);
                xx = Mathf.SmoothStep(0f, 1f, turb);
                colors[index] = new Color(xx, xx, xx, xx);
            }
        }
        mesh.vertices = newVertices;
        mesh.normals = normals;
        mesh.colors = colors;
    }
}
