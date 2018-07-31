using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGPU : MonoBehaviour
{
    public Material oceanMat;
    public int resolution = 256;
    public float unitWidth = 1;
    public int detail = 8;

    public float length = 200;
    public float amplitude = 0.0001f;
    public Vector2 wind;
    public float choppiness = 1;
    public float timeSpeed = 1;

    private void Awake()
    {
        GameObject water = new GameObject("Water");

        filter = water.AddComponent<MeshFilter>();
        mesh = new Mesh();
        filter.mesh = mesh;

        water.AddComponent<MeshRenderer>().material = oceanMat;

        SetParams();
        GenerateMesh();
        RenderInitial();
    }

    #region InitMesh

    private MeshFilter filter;
    private Mesh mesh;
    
    void GenerateMesh()
    {
        Vector3[] vertices = new Vector3[resolution * resolution];
        Vector3[] normals = new Vector3[resolution * resolution];
        Vector2[] uvs = new Vector2[resolution * resolution];
        int[] indices = new int[(resolution - 1) * (resolution - 1) * 6];

        int indiceCount = 0;
        float halfResolution = resolution / 2f;

        for (int i = 0; i < resolution; i++)
        {
            float horizontalPosition = (i - halfResolution) * unitWidth;

            for (int j = 0; j < resolution; j++)
            {
                int currentIdx = i * (resolution) + j;
                float verticalPosition = (j - halfResolution) * unitWidth;
                vertices[currentIdx] = new Vector3(horizontalPosition + unitWidth / 2f, 0f, verticalPosition + unitWidth / 2f);
                normals[currentIdx] = new Vector3(0f, 1f, 0f);
                uvs[currentIdx] = new Vector2(i * 1.0f / (resolution - 1), j * 1.0f / (resolution - 1));

                if (i != resolution - 1 && j != resolution - 1)
                {
                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx + 1;
                    indices[indiceCount++] = currentIdx + resolution + 1;

                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx + resolution + 1;
                    indices[indiceCount++] = currentIdx + resolution;
                }
            }
        }

        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.normals = normals;
        mesh.uv = uvs;
        filter.mesh = mesh;
    }

    #endregion

    #region InitShader

    // h0, h0conj
    public Shader initialShader;
    // x, z
    public Shader spectrumShader;
    // h
    public Shader spectrumHeightShader;
    // 
    public Shader fftShader;
    public Shader dispersionShader;
    public Shader normalShader;
    public Shader whiteShader;

    private Material initialMat;
    private Material spectrumMat;
    private Material heightMat;
    private Material fftMat;
    private Material dispersionMat;
    private Material normalMat;
    private Material whiteMat;

    public RenderTexture initialTexture;
    public RenderTexture firstPhaseTexture;
    public RenderTexture secondPhaseTexture;
    public RenderTexture pingTransformTexture;
    public RenderTexture pongTransformTexture;
    public RenderTexture spectrumTexture;
    public RenderTexture heightTexture;
    public RenderTexture displacementTexture;
    public RenderTexture normalTexture;
    public RenderTexture whiteTexture;

    private void SetParams()
    {
        oldLength = length;
        oldChoppiness = choppiness;
        oldAmplitude = amplitude;
        oldWind = wind;

        initialMat = new Material(initialShader);
        spectrumMat = new Material(spectrumShader);
        heightMat = new Material(spectrumHeightShader);
        fftMat = new Material(fftShader);
        dispersionMat = new Material(dispersionShader);
        normalMat = new Material(normalShader);
        whiteMat = new Material(whiteShader);

        resolution *= detail;

        initialTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        firstPhaseTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat);
        secondPhaseTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat);
        pingTransformTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        pongTransformTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        spectrumTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        heightTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        displacementTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        normalTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        whiteTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);

        // 不知道为什么要乘10,反正结果看不出差别
        initialMat.SetFloat("_RandomSeed1", Random.value * 10);
        initialMat.SetFloat("_RandomSeed2", Random.value * 10);
        initialMat.SetFloat("_Amplitude", amplitude);
        initialMat.SetFloat("_Length", length);
        initialMat.SetFloat("_Resolution", resolution);
        initialMat.SetVector("_Wind", wind);

        dispersionMat.SetFloat("_Length", length);
        dispersionMat.SetInt("_Resolution", resolution);

        spectrumMat.SetFloat("_Choppiness", choppiness);
        spectrumMat.SetFloat("_Length", length);
        spectrumMat.SetInt("_Resolution", resolution);
        
        normalMat.SetFloat("_Length", length);
        normalMat.SetFloat("_Resolution", resolution);

        fftMat.SetFloat("_TransformSize", resolution);

        resolution /= detail;
    }

    private void RenderInitial()
    {
        Graphics.Blit(null, initialTexture, initialMat);
        spectrumMat.SetTexture("_Initial", initialTexture);
        heightMat.SetTexture("_Initial", initialTexture);
    }

    #endregion

    private float oldLength;
    private float oldChoppiness;
    private float oldAmplitude;
    private Vector2 oldWind;

    private bool saved = false;
    private bool firstPhase = false;

    private void Update()
    {
        GenerateTexture();
        dispersionMat.SetFloat("_Length", length);

        spectrumMat.SetFloat("_Choppiness", choppiness);
        spectrumMat.SetFloat("_Length", length);
        if (oldLength != length || oldWind != wind || oldAmplitude != amplitude)
        {
            initialMat.SetFloat("_Amplitude", amplitude);
            initialMat.SetFloat("_Length", length);
            initialMat.SetVector("_Wind", wind);
            oldLength = length;
            oldChoppiness = choppiness;
            oldAmplitude = amplitude;
            oldWind.x = wind.x;
            oldWind.y = wind.y;
            RenderInitial();
        }
    }

    /*
    如果在水面渲染过程中修改风向或是波长的话, 将会导致Dispersion发生骤变, 水面也会产生抖动和闪烁. 
    因此将交替使用两张Dispersion Texture来保证在修改参数前后的连续性,计算也是用delta计算变化而不是直接计算t时间的海洋
    */
    private void GenerateTexture()
    {
        firstPhase = !firstPhase;


        // 计算Dispersion Relation 色散关系
        RenderTexture rt = firstPhase ? firstPhaseTexture : secondPhaseTexture;
        // 传入上一帧的Dispersion
        dispersionMat.SetTexture("_Phase", firstPhase ? secondPhaseTexture : firstPhaseTexture);
        // 传入时间间隔
        dispersionMat.SetFloat("_DeltaTime", Time.deltaTime * timeSpeed);
        Graphics.Blit(null, rt, dispersionMat);


        // 计算Displacement vector xz
        // 传入上面计算出来的Dispersion
        spectrumMat.SetTexture("_Phase", firstPhase ? firstPhaseTexture : secondPhaseTexture);
        Graphics.Blit(null, spectrumTexture, spectrumMat);


        // 
        fftMat.EnableKeyword("_HORIZONTAL");
        fftMat.DisableKeyword("_VERTICAL");
        int iterations = Mathf.CeilToInt((float)Mathf.Log(resolution * detail, 2)) * 2;
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture blitTarget;
            fftMat.SetFloat("_SubTransformSize", Mathf.Pow(2, (i % (iterations / 2)) + 1));
            if (i == 0)
            {
                fftMat.SetTexture("_Input", spectrumTexture);
                blitTarget = pingTransformTexture;
            }
            else if (i == iterations - 1)
            {
                fftMat.SetTexture("_Input", (iterations % 2 == 0) ? pingTransformTexture : pongTransformTexture);
                blitTarget = displacementTexture;
            }
            else if (i % 2 == 1)
            {
                fftMat.SetTexture("_Input", pingTransformTexture);
                blitTarget = pongTransformTexture;
            }
            else
            {
                fftMat.SetTexture("_Input", pongTransformTexture);
                blitTarget = pingTransformTexture;
            }
            if (i == iterations / 2)
            {
                fftMat.DisableKeyword("_HORIZONTAL");
                fftMat.EnableKeyword("_VERTICAL");
            }
            Graphics.Blit(null, blitTarget, fftMat);
        }

        heightMat.SetTexture("_Phase", firstPhase ? firstPhaseTexture : secondPhaseTexture);
        Graphics.Blit(null, spectrumTexture, heightMat);
        fftMat.EnableKeyword("_HORIZONTAL");
        fftMat.DisableKeyword("_VERTICAL");
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture blitTarget;
            fftMat.SetFloat("_SubTransformSize", Mathf.Pow(2, (i % (iterations / 2)) + 1));
            if (i == 0)
            {
                fftMat.SetTexture("_Input", spectrumTexture);
                blitTarget = pingTransformTexture;
            }
            else if (i == iterations - 1)
            {
                fftMat.SetTexture("_Input", (iterations % 2 == 0) ? pingTransformTexture : pongTransformTexture);
                blitTarget = heightTexture;
            }
            else if (i % 2 == 1)
            {
                fftMat.SetTexture("_Input", pingTransformTexture);
                blitTarget = pongTransformTexture;
            }
            else
            {
                fftMat.SetTexture("_Input", pongTransformTexture);
                blitTarget = pingTransformTexture;
            }
            if (i == iterations / 2)
            {
                fftMat.DisableKeyword("_HORIZONTAL");
                fftMat.EnableKeyword("_VERTICAL");
            }
            Graphics.Blit(null, blitTarget, fftMat);
        }

        normalMat.SetTexture("_DisplacementMap", displacementTexture);
        normalMat.SetTexture("_HeightMap", heightTexture);
        Graphics.Blit(null, normalTexture, normalMat);
        whiteMat.SetTexture("_Displacement", displacementTexture);
        whiteMat.SetTexture("_Bump", normalTexture);
        whiteMat.SetFloat("_Resolution", resolution * detail);
        whiteMat.SetFloat("_Length", resolution);
        Graphics.Blit(null, whiteTexture, whiteMat);

        if (!saved)
        {
            oceanMat.SetTexture("_Anim", displacementTexture);
            oceanMat.SetTexture("_Bump", normalTexture);
            oceanMat.SetTexture("_White", whiteTexture);
            oceanMat.SetTexture("_Height", heightTexture);
            saved = true;
        }
    }
}