using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterGPU : MonoBehaviour
{
    public Material oceanMat;
    public int resolution = 128;
    public float unitWidth = 1;
    public int detail = 8;

    public float length = 100;
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

    public Shader initialShader;
    public Shader spectrumShader;
    public Shader spectrumHeightShader;
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

    private RenderTexture initialTexture;
    private RenderTexture firstPhaseTexture;
    private RenderTexture secondPhaseTexture;
    private RenderTexture pingTransformTexture;
    private RenderTexture pongTransformTexture;
    private RenderTexture spectrumTexture;
    [HideInInspector]
    public RenderTexture heightTexture;
    [HideInInspector]
    public RenderTexture displacementTexture;
    private RenderTexture normalTexture;
    private RenderTexture whiteTexture;

    private void SetParams()
    {
        oldLength = length;
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
        initialMat.SetFloat("_RandomSeed1", Random.value);
        initialMat.SetFloat("_RandomSeed2", Random.value);
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

    // 初始化h0和h0mk_Conj,并传入spectrumMat和heightMat
    private void RenderInitial()
    {
        Graphics.Blit(null, initialTexture, initialMat);
        spectrumMat.SetTexture("_Initial", initialTexture);
        heightMat.SetTexture("_Initial", initialTexture);
    }

    #endregion

    #region Update

    private float oldLength;
    private float oldAmplitude;
    private Vector2 oldWind;

    private bool saved = false;
    private bool firstPhase = false;

    /*
    测试Stockham FFT
    public int _TransformSize = 128;
    public int _SubTransformSize = 2;
    public float xxx;
    public decimal index;
    public float showIndex;
    public decimal basee;
    public float showFloor;
    public float showBasee;
    public decimal offset;
    public float showOffset;
    public float evenIndex;
    public float oddIndex;
    */
    private void Update()
    {
        /*
        index = (decimal)xxx * _TransformSize;
        showIndex = (float)index;
        showFloor = (float)(index / _SubTransformSize - index / _SubTransformSize % 1);
        basee = (index / _SubTransformSize - index / _SubTransformSize % 1) * (_SubTransformSize * 0.5m);
        showBasee = (float)basee;
        offset = index % (_SubTransformSize * 0.5m);
        showOffset = (float)offset;
        evenIndex = (float)((basee + offset) / _TransformSize);
        oddIndex = evenIndex + 0.5f;
        */

        GenerateTexture();

        // 输入的参数改动时
        spectrumMat.SetFloat("_Choppiness", choppiness);
        
        // 如果海洋的原始数据(h0, h0mk_Conj)需要改动
        if (oldLength != length || oldWind != wind || oldAmplitude != amplitude)
        {
            // 把length从外面移进来
            dispersionMat.SetFloat("_Length", length);
            spectrumMat.SetFloat("_Length", length);
            // 应该忘记添加这两个
            dispersionMat.SetFloat("_Length", length);
            normalMat.SetFloat("_Length", length);

            initialMat.SetFloat("_Amplitude", amplitude);
            initialMat.SetFloat("_Length", length);
            initialMat.SetVector("_Wind", wind);
            oldLength = length;
            oldAmplitude = amplitude;
            oldWind.x = wind.x;
            oldWind.y = wind.y;
            RenderInitial();
        }
    }

    public int FFTTestIterations = 20;

    /*
    如果在水面渲染过程中修改风向或是波长的话, 将会导致Dispersion发生骤变, 水面也会产生抖动和闪烁. 
    因此将交替使用两张Dispersion Texture来保证在修改参数前后的连续性,计算也是用delta计算变化而不是直接计算t时间的海洋
    */
    private void GenerateTexture()
    {
        firstPhase = !firstPhase;
        
        // 计算当前时间的Dispersion Relation 色散关系
        RenderTexture rt = firstPhase ? firstPhaseTexture : secondPhaseTexture;
        // 传入上一帧的Dispersion
        dispersionMat.SetTexture("_Phase", firstPhase ? secondPhaseTexture : firstPhaseTexture);
        // 传入时间间隔
        dispersionMat.SetFloat("_DeltaTime", Time.deltaTime * timeSpeed);
        Graphics.Blit(null, rt, dispersionMat);

      

        // 计算Displacement vector xz
        // -----FFT-----

        // 蝴蝶网络的第一层
        // 传入上面计算出来的Dispersion
        spectrumMat.SetTexture("_Phase", firstPhase ? firstPhaseTexture : secondPhaseTexture);
        Graphics.Blit(null, spectrumTexture, spectrumMat);


        fftMat.EnableKeyword("_HORIZONTAL");
        fftMat.DisableKeyword("_VERTICAL");
        // 蝴蝶算法的次数(resolution和detail都应该是2的幂次方)
        // 因为是二维的所以要乘2
        int iterations = Mathf.CeilToInt((float)Mathf.Log(resolution * detail, 2)) * 2;

        //iterations = FFTTestIterations;

        // 继续算蝴蝶网络iterations次
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



        // 计算Displacement vector h
        // -----FFT-----

        // 蝴蝶网络的第一层
        heightMat.SetTexture("_Phase", firstPhase ? firstPhaseTexture : secondPhaseTexture);
        Graphics.Blit(null, spectrumTexture, heightMat);

        fftMat.EnableKeyword("_HORIZONTAL");
        fftMat.DisableKeyword("_VERTICAL");


        for (int i = 0; i < iterations; i++)
        {
            RenderTexture blitTarget;

            // iterations是2的倍数
            // 前半 2,4,8..
            // 后半 2,4,8..
            fftMat.SetFloat("_SubTransformSize", Mathf.Pow(2, (i % (iterations / 2)) + 1));

            if (i == 0)
            {
                // 第一次把之前算的蝴蝶网络的第一层放进来
                fftMat.SetTexture("_Input", spectrumTexture);
                blitTarget = pingTransformTexture;
            }
            else if (i == iterations - 1)
            {
                // 如果是最后一次,把结果放到heightTexture
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
                // 到中间的时候交换
                fftMat.DisableKeyword("_HORIZONTAL");
                fftMat.EnableKeyword("_VERTICAL");
            }

            Graphics.Blit(null, blitTarget, fftMat);
        }


        // 计算normal
        // 传入计算出来的Displacement vector h和xz
        normalMat.SetTexture("_DisplacementMap", displacementTexture);
        normalMat.SetTexture("_HeightMap", heightTexture);
        Graphics.Blit(null, normalTexture, normalMat);


        // 计算白沫
        // 传入计算出来的normal和xz
        whiteMat.SetTexture("_Displacement", displacementTexture);
        whiteMat.SetTexture("_Bump", normalTexture);
        whiteMat.SetFloat("_Resolution", resolution * detail);
        whiteMat.SetFloat("_Length", resolution);
        Graphics.Blit(null, whiteTexture, whiteMat);


        // 海洋shader需要的参数
        if (!saved)
        {
            oceanMat.SetTexture("_Anim", displacementTexture);
            oceanMat.SetTexture("_Bump", normalTexture);
            oceanMat.SetTexture("_White", whiteTexture);
            oceanMat.SetTexture("_Height", heightTexture);
            saved = true;
        }
    }

    #endregion
}