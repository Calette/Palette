using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterHeightTest : MonoBehaviour
{
    public WaterGPU water;
    public float offsetY;

    public RenderTexture heightTexture;
    public RenderTexture displacementTexture;
    private Texture2D heightMap;
    private Texture2D displacementMap;

    private int resolution = 128;
    private float unitWidth = 1;
    private int detail = 8;

    private float length;
    private int pixelCount;

    private float posX;
    private float posZ;

    // Use this for initialization
    void Start () {
        resolution = water.resolution;
        unitWidth = water.unitWidth;
        detail = water.detail;
        posX = transform.localPosition.x;
        posZ = transform.localPosition.z;

        length = unitWidth * (resolution - 1);
        pixelCount = resolution * detail;

        // GPURenderHeight
        mat = new Material(shader);
        heightTex = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat);
        u = posX / length + 0.5f;
        v = posZ / length + 0.5f;
        mat.SetFloat("_U", u);
        mat.SetFloat("_V", v);
    }

    // Update is called once per frame
    void Update ()
    {
        //GetHeight();
        GPURenderHeight();
    }

    public Shader shader;
    public Material mat;
    public RenderTexture heightTex;

    float u;
    float v;

    private void GPURenderHeight()
    {
        mat.SetTexture("_Anim", water.displacementTexture);
        mat.SetTexture("_Height", water.heightTexture);
        Graphics.Blit(null, heightTex, mat);

        Texture2D heightTex2D = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
        RenderTexture.active = heightTex;
        heightTex2D.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
        heightTex2D.Apply();

        Color pos = heightTex2D.GetPixel(0, 0);

        transform.localPosition = new Vector3(posX + pos.r, pos.g + offsetY, posZ + pos.b);

        DestroyImmediate(heightTex2D);
    }

    private void GetHeight()
    {
        heightTexture = water.heightTexture;
        displacementTexture = water.displacementTexture;

        heightMap = new Texture2D(pixelCount, pixelCount, TextureFormat.RGBAFloat, false);
        RenderTexture.active = heightTexture;
        heightMap.ReadPixels(new Rect(0, 0, pixelCount, pixelCount), 0, 0);
        heightMap.Apply();

        displacementMap = new Texture2D(pixelCount, pixelCount, TextureFormat.RGBAFloat, false);
        RenderTexture.active = displacementTexture;
        displacementMap.ReadPixels(new Rect(0, 0, pixelCount, pixelCount), 0, 0);
        displacementMap.Apply();

        int x = (int)((posX / length + 0.5f) * pixelCount);
        int y = (int)((posZ / length + 0.5f) * pixelCount);

        float h = heightMap.GetPixel(x, y).r / 8;

        float offsetX = displacementMap.GetPixel(x, y).r / 8;
        float offsetZ = displacementMap.GetPixel(x, y).b / 8;

        transform.localPosition = new Vector3(posX + offsetX, h, posZ + offsetZ);

        DestroyImmediate(heightMap);
        DestroyImmediate(displacementMap);
    }
}
