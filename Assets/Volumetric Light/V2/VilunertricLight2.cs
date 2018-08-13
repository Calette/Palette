using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VilunertricLight2 : MonoBehaviour
{
    // VilunertricLight
    public Shader shader;
    [Range(0, 0.999f)]
    public float mieG;
    public float maxLength = 100;
    public int sampleCount = 64;
    public float volumetricIntensity = 0.1f;

    private Camera cam;
    private Material lightMat;

    // Noise
    public Vector2 NoiseVelocity;
    public float NoiseScale = 0.015f;
    public float NoiseIntensity = 1.0f;
    public float NoiseIntensityOffset = 0.3f;
    public TextAsset data;
    private Texture3D _noiseTexture;

    // Blur
    public Shader blurShader;
    [Range(0, 4)]
    public int iterations = 3;
    [Range(0.2f, 3.0f)]
    public float blurSpread = 0.6f;
    [Range(1, 8)]
    public int downSample = 2;

    private Material blurMat;

    public Shader blendShader;
    private Material blendMat;

    // Use this for initialization
    void Awake()
    {
        cam = GetComponent<Camera>();
        lightMat = new Material(shader);
        blurMat = new Material(blurShader);
        blendMat = new Material(blendShader);

        LoadNoise3dTexture();
    }
    
    void LoadNoise3dTexture()
    {
        // basic dds loader for 3d texture - !not very robust!

        //TextAsset data = Resources.Load("NoiseVolume") as TextAsset;

        byte[] bytes = data.bytes;

        //print(bytes.Length);

        uint height = BitConverter.ToUInt32(data.bytes, 12);
        uint width = BitConverter.ToUInt32(data.bytes, 16);
        uint pitch = BitConverter.ToUInt32(data.bytes, 20);
        uint depth = BitConverter.ToUInt32(data.bytes, 24);
        uint formatFlags = BitConverter.ToUInt32(data.bytes, 20 * 4);
        //uint fourCC = BitConverter.ToUInt32(data.bytes, 21 * 4);
        uint bitdepth = BitConverter.ToUInt32(data.bytes, 22 * 4);
        if (bitdepth == 0)
            bitdepth = pitch / width * 8;


        // doesn't work with TextureFormat.Alpha8 for some reason
        _noiseTexture = new Texture3D((int)width, (int)height, (int)depth, TextureFormat.RGBA32, false);
        _noiseTexture.name = "3D Noise";

        Color[] c = new Color[width * height * depth];

        uint index = 128;
        if (data.bytes[21 * 4] == 'D' && data.bytes[21 * 4 + 1] == 'X' && data.bytes[21 * 4 + 2] == '1' && data.bytes[21 * 4 + 3] == '0' &&
            (formatFlags & 0x4) != 0)
        {
            uint format = BitConverter.ToUInt32(data.bytes, (int)index);
            if (format >= 60 && format <= 65)
                bitdepth = 8;
            else if (format >= 48 && format <= 52)
                bitdepth = 16;
            else if (format >= 27 && format <= 32)
                bitdepth = 32;

            //Debug.Log("DXGI format: " + format);
            // dx10 format, skip dx10 header
            //Debug.Log("DX10 format");
            index += 20;
        }

        uint byteDepth = bitdepth / 8;
        pitch = (width * bitdepth + 7) / 8;

        for (int d = 0; d < depth; ++d)
        {
            //index = 128;
            for (int h = 0; h < height; ++h)
            {
                for (int w = 0; w < width; ++w)
                {
                    float v = (bytes[index + w * byteDepth] / 255.0f);
                    c[w + h * width + d * width * height] = new Color(v, v, v, v);
                }

                index += pitch;
            }
        }

        _noiseTexture.SetPixels(c);
        _noiseTexture.Apply();

        Shader.SetGlobalTexture("_NoiseTexture", _noiseTexture);
    }
    
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (lightMat != null && blurMat != null && blendMat != null)
        {
            lightMat.SetVector("_NoiseVelocity", new Vector4(NoiseVelocity.x, NoiseVelocity.y) * NoiseScale);
            lightMat.SetVector("_NoiseData", new Vector4(NoiseScale, NoiseIntensity, NoiseIntensityOffset));

            lightMat.SetVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
            lightMat.SetFloat("_MaxLength", maxLength);
            lightMat.SetFloat("_SampleCount", sampleCount);
            lightMat.SetFloat("_VolumetricIntensity", volumetricIntensity);

            int rtW = src.width / downSample;
            int rtH = src.height / downSample;

            RenderTexture buffer0 = RenderTexture.GetTemporary(rtW, rtH, 0);

            Graphics.Blit(src, buffer0, lightMat);

            //Graphics.Blit(src, dest, lightMat);

            for (int i = 0; i < iterations; i++)
            {
                blurMat.SetFloat("_BlurSize", 1.0f + i * blurSpread);

                RenderTexture buffer1 = RenderTexture.GetTemporary(rtW, rtH, 0);

                // Render the vertical pass
                Graphics.Blit(buffer0, buffer1, blurMat, 0);

                RenderTexture.ReleaseTemporary(buffer0);
                buffer0 = buffer1;
                buffer1 = RenderTexture.GetTemporary(rtW, rtH, 0);

                // Render the horizontal pass
                Graphics.Blit(buffer0, buffer1, blurMat, 1);

                RenderTexture.ReleaseTemporary(buffer0);
                buffer0 = buffer1;
            }

            blendMat.SetTexture("_Src", src);
            Graphics.Blit(buffer0, dest, blendMat);
            RenderTexture.ReleaseTemporary(buffer0);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}