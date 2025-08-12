using System;
using UnityEngine;

[ExecuteInEditMode]
public class NormalMapGenerator : MonoBehaviour
{
    [Header("Shaders")]
    [SerializeField] private ComputeShader _slowGaussianShader;
    [SerializeField] private ComputeShader _slowDistanceShader;
    [SerializeField] private ComputeShader _slowNormalMapShader;
    [SerializeField] private bool S1BlurSource = false;
    [SerializeField] private bool S2DistMap = false;
    [SerializeField] private bool S3NormalMap = false;
    [SerializeField] private bool S4FinalBlur = false;


    [SerializeField] private ComputeShader _fastBlurDistShader;
    [SerializeField] private ComputeShader _fastGaussianShader;
    [SerializeField] private ComputeShader _fastNormalMapShader;

    [SerializeField] public bool useOptimized = false;


    [Header("Manual Debugs")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Texture2D sourceTexture; // Input texture
    [SerializeField] private int normalStrength = 20; // Controls the steepness of slopes
    [SerializeField][Range(0, 10)] private int blurEdges = 5; // Controls the width of the edges (larger value means wider edges)

    [SerializeField] private int bumpHeight = 80; // Controls the height of the bump effect
    [SerializeField][Range(1, 10)] private int blurBump = 5; // Controls the width of the edges (larger value means wider edges)
    [SerializeField][Range(1f, 10f)] private int blurSoften = 1;

    [SerializeField][Range(0f, 1f)] private float slopeDistance = 0.1f; // Controls the distance over which the slope extends inward

    [SerializeField][Range(0, 10)] private int finalBlur = 5; // Controls the width of the edges (larger value means wider edges)


    public float GAUSSIAN_WEIGHT = 10f;


    public bool _update = false;

    private void Update()
    {
        if (_update)
        {
            _update = false;
            NormalMapSettings settings = new NormalMapSettings();
            settings.sourceTexture = sourceTexture;
            settings.strengthEdges = normalStrength;
            settings.blurEdgesRadius = blurEdges;
            settings.strengthBorder = bumpHeight;
            settings.blurBorderRadius = blurBump;
            settings.softenBorder = blurSoften;
            settings.slopePercentageBorder = slopeDistance;
            settings.finalBlurRadius = finalBlur;

            Texture2D test = GenerateNormalMap(settings);
            spriteRenderer.sprite = Sprite.Create(test, new Rect(0, 0, test.width, test.height), new Vector2(0.5f, 0.5f));
        }
    }

    public Texture2D GenerateNormalMap(NormalMapSettings settings)
    {
        Texture2D result;

        //double startTime = Time.realtimeSinceStartupAsDouble;

        if (useOptimized)
        {
            result = CSAllInOne(settings);
        }
        else
        {
            Texture2D lastResult = settings.sourceTexture;

            if (S1BlurSource)
            {
                // Apply Gaussian blur to the normal map
                Texture2D blurredTexture = CSApplyGaussianBlur(settings.sourceTexture, settings.blurEdgesRadius);
                lastResult = blurredTexture;

                if (S2DistMap)
                {
                    // Generate a height map with the "puffed-up" effect
                    Texture2D heightMap = CSGenerateHeightMap(blurredTexture, Mathf.RoundToInt(settings.sourceTexture.width * settings.slopePercentageBorder));
                    //heightMap = CSApplyGaussianBlur(heightMap, settings.blurBorderRadius);
                    lastResult = heightMap;

                    if (S3NormalMap)
                    {
                        // Generate the normal map with the bump effect
                        Texture2D normalMap = CSApplySourceAndHeightMap(blurredTexture, heightMap, settings.strengthEdges, settings.strengthBorder, settings.blurBorderRadius, settings.softenBorder);
                        lastResult = normalMap;

                        if (S4FinalBlur)
                        {
                            // Apply Gaussian blur to the normal map
                            Texture2D finalBlur = CSApplyGaussianBlur(normalMap, settings.finalBlurRadius);
                            lastResult = finalBlur;
                        }
                    }
                }
            }

            result = lastResult;
        }

        //double endTime = Time.realtimeSinceStartupAsDouble;

        //Debug.Log("Took " + (endTime - startTime) * 1000 + " ms");

        return result;
    }


    private Texture2D CSApplySourceAndHeightMap(Texture2D source, Texture2D heightMap,
                                                     float normalStrength, float bumpHeight, int bumpBlur, float softenBump)
    {
        int width = source.width;
        int height = source.height;

        RenderTexture renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        // Pass buffers and textures
        int kernelHandle = _slowNormalMapShader.FindKernel("CSMain");
        _slowNormalMapShader.SetTexture(kernelHandle, "_sourceTex", source);
        _slowNormalMapShader.SetTexture(kernelHandle, "_heightMapTex", heightMap);
        _slowNormalMapShader.SetTexture(kernelHandle, "_resultTex", renderTexture);
        _slowNormalMapShader.SetFloat("_normalStrength", normalStrength);
        _slowNormalMapShader.SetFloat("_bumpStrength", bumpHeight);
        _slowNormalMapShader.SetInt("_bumpBlur", bumpBlur);
        _slowNormalMapShader.SetFloat("_softenBump", softenBump);

        // Start the shader up
        _slowNormalMapShader.GetKernelThreadGroupSizes(kernelHandle, out uint groupSizeX, out uint groupSizeY, out _);
        int threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
        int threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);
        _slowNormalMapShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

        // Get data
        RenderTexture.active = renderTexture;
        Texture2D normalTexture = new Texture2D(width, height);
        normalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        normalTexture.Apply();

        // Cleanup
        RenderTexture.active = null;

        return normalTexture;
    }


    private Texture2D CSGenerateHeightMap(Texture2D source, int slopeDistance)
    {
        int width = source.width;
        int height = source.height;

        RenderTexture renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        // Pass the Gaussian size to the compute shader
        _slowDistanceShader.SetInt("_distance", slopeDistance);

        // Pass buffers and textures
        int kernelHandle = _slowDistanceShader.FindKernel("CSMain");
        _slowDistanceShader.SetTexture(kernelHandle, "_sourceTex", source);
        _slowDistanceShader.SetTexture(kernelHandle, "_resultTex", renderTexture);

        // Start the shader up
        _slowDistanceShader.GetKernelThreadGroupSizes(kernelHandle, out uint groupSizeX, out uint groupSizeY, out _);
        int threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
        int threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);
        _slowDistanceShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

        // Get data
        RenderTexture.active = renderTexture;
        Texture2D heightMap = new Texture2D(width, height);
        heightMap.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        heightMap.Apply();

        // Cleanup
        RenderTexture.active = null;

        return heightMap;
    }


    private Texture2D CSApplyGaussianBlur(Texture2D source, int radius)
    {
        int width = source.width;
        int height = source.height;

        if (radius == 0)
        {
            return source;
        }

        RenderTexture renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        // Create gaussian kernel
        float[] kernelAlt = GetGaussianKernel(radius, GAUSSIAN_WEIGHT);

        // Create buffer
        ComputeBuffer gaussianKernelBuffer = new ComputeBuffer(kernelAlt.Length, sizeof(float));
        gaussianKernelBuffer.SetData(kernelAlt);

        // Pass the Gaussian size to the compute shader
        _slowGaussianShader.SetInt("_blurRadius", radius);

        // Pass buffers and textures
        int kernelHandle = _slowGaussianShader.FindKernel("CSMain");
        _slowGaussianShader.SetTexture(kernelHandle, "_sourceTex", source);
        _slowGaussianShader.SetTexture(kernelHandle, "_resultTex", renderTexture);
        _slowGaussianShader.SetBuffer(kernelHandle, "_gaussianKernel", gaussianKernelBuffer);

        // Start the shader up
        _slowGaussianShader.GetKernelThreadGroupSizes(kernelHandle, out uint groupSizeX, out uint groupSizeY, out _);
        int threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
        int threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);
        _slowGaussianShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

        // Get data
        RenderTexture.active = renderTexture;
        Texture2D blurredTexture = new Texture2D(width, height);
        blurredTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        blurredTexture.Apply();

        // Cleanup
        gaussianKernelBuffer.Dispose();
        RenderTexture.active = null;
        renderTexture.Release();

        return blurredTexture;
    }


    private static float[] GetGaussianKernel(int radius, float weight)
    {
        int length = radius * 2 + 1;
        float[] kernel = new float[length * length];
        float sumTotal = 0;

        //int kernelRadius = halfRadius / 2;

        // G(x, y) = (1 / 2πσ²) * exp(-(x² + y²) / 2σ²)
        float calculatedEuler = 1.0f / (2.0f * Mathf.PI * Mathf.Pow(weight, 2));

        for (int filterY = 0; filterY < length; filterY++)
        {
            for (int filterX = 0; filterX < length; filterX++)
            {
                int filterXOffset = filterX - radius;
                int filterYOffset = filterY - radius;

                float distance = ((filterXOffset * filterXOffset) + (filterYOffset * filterYOffset)) / (2f * (weight * weight));

                kernel[filterY * length + filterX] = calculatedEuler * Mathf.Exp(-distance);

                sumTotal += kernel[filterY * length + filterX];
            }
        }

        // Normalize
        for (int y = 0; y < length; y++)
        {
            for (int x = 0; x < length; x++)
            {
                kernel[y * length + x] /= sumTotal;
            }
        }

        return kernel;
    }
    
    private Texture2D CSAllInOne(NormalMapSettings settings)
    {
        int width = settings.sourceTexture.width;
        int height = settings.sourceTexture.height;

        // Render Texture descriptor for render textures
        RenderTextureDescriptor rendTexDesc = new RenderTextureDescriptor(width, height);
        rendTexDesc.colorFormat = RenderTextureFormat.Default;
        rendTexDesc.sRGB = true;
        rendTexDesc.enableRandomWrite = true;

        // Result and temp texture
        RenderTexture resultRendTex = new RenderTexture(rendTexDesc);
        resultRendTex.Create();
        RenderTexture srcBlurRendTex = RenderTexture.GetTemporary(rendTexDesc);
        srcBlurRendTex.Create();
        RenderTexture distRendTex = RenderTexture.GetTemporary(rendTexDesc);
        distRendTex.Create();
        RenderTexture normRendTex = RenderTexture.GetTemporary(rendTexDesc);
        normRendTex.Create();

        // ------------------------------------BLUR-SETUP------------------------------------
        // Create gaussian kernels
        float[] kernelEdge = GetGaussianKernel(settings.blurEdgesRadius, GAUSSIAN_WEIGHT);
        //float[] kernelBorder = GetGaussianKernel(settings.blurBorderRadius, GAUSSIAN_WEIGHT);
        float[] kernelFinal = GetGaussianKernel(settings.finalBlurRadius, GAUSSIAN_WEIGHT);

        // Create gauss kernel buffers and set data
        ComputeBuffer kernelEdgeBlurBuffer = new ComputeBuffer(kernelEdge.Length, sizeof(float));
        kernelEdgeBlurBuffer.SetData(kernelEdge);
        //ComputeBuffer kernelBorderBlurBuffer = new ComputeBuffer(kernelBorder.Length, sizeof(float));
        //kernelBorderBlurBuffer.SetData(kernelBorder);
        ComputeBuffer kernelFinalBlurBuffer = new ComputeBuffer(kernelFinal.Length, sizeof(float));
        kernelFinalBlurBuffer.SetData(kernelFinal);


        // ------------------------------------S1+S2-BLUR-DIST-STAGES------------------------------------
        RenderTexture lastResult = resultRendTex;

        if (S1BlurSource || S2DistMap)
        {
            // Pass params
            _fastBlurDistShader.SetInt("_blurRadius", settings.blurEdgesRadius);
            _fastBlurDistShader.SetFloat("_slopePercentageBorder", settings.slopePercentageBorder);
            _fastBlurDistShader.SetBool("_doSourceBlur", S1BlurSource);
            _fastBlurDistShader.SetBool("_doDistMap", S2DistMap);

            // Pass buffers and textures
            int kernelHandle = _fastBlurDistShader.FindKernel("CSMain");
            _fastBlurDistShader.SetTexture(kernelHandle, "_sourceTex", settings.sourceTexture);
            _fastBlurDistShader.SetTexture(kernelHandle, "_srcBlurTex", srcBlurRendTex);
            _fastBlurDistShader.SetTexture(kernelHandle, "_distTex", distRendTex);
            _fastBlurDistShader.SetBuffer(kernelHandle, "_gaussianKernel", kernelEdgeBlurBuffer);

            // Calculate kernels group size
            _fastBlurDistShader.GetKernelThreadGroupSizes(kernelHandle, out uint groupSizeX, out uint groupSizeY, out _);
            int threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
            int threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);

            _fastBlurDistShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

            if (S1BlurSource) lastResult = srcBlurRendTex;
            if (S2DistMap) lastResult = distRendTex;

            // ------------------------------------S3-NORMAL-MAP-STAGE------------------------------------
            if (S3NormalMap)
            {
                // Pass buffers and textures
                kernelHandle = _fastNormalMapShader.FindKernel("CSMain");
                _fastNormalMapShader.SetTexture(kernelHandle, "_srcBlurTex", srcBlurRendTex);
                _fastNormalMapShader.SetTexture(kernelHandle, "_distMapTex", distRendTex);
                _fastNormalMapShader.SetTexture(kernelHandle, "_resultTex", normRendTex);
                _fastNormalMapShader.SetInt("_edgeStrength", settings.strengthEdges);
                _fastNormalMapShader.SetInt("_borderStrength", settings.strengthBorder);
                _fastNormalMapShader.SetInt("_borderSoften", settings.softenBorder);

                // Start the shader up
                _fastNormalMapShader.GetKernelThreadGroupSizes(kernelHandle, out groupSizeX, out groupSizeY, out _);
                threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
                threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);
                _fastNormalMapShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

                lastResult = normRendTex;

                // ------------------------------------FINAL-BLUR-STAGE------------------------------------
                if (S4FinalBlur)
                {
                    // Pass the Gaussian size to the compute shader
                    _fastGaussianShader.SetInt("_blurRadius", settings.finalBlurRadius);

                    // Pass buffers and textures
                    kernelHandle = _fastGaussianShader.FindKernel("CSMain");
                    _fastGaussianShader.SetTexture(kernelHandle, "_sourceTex", normRendTex);
                    _fastGaussianShader.SetTexture(kernelHandle, "_resultTex", resultRendTex);
                    _fastGaussianShader.SetBuffer(kernelHandle, "_gaussianKernel", kernelFinalBlurBuffer);

                    // Calculate kernels group size
                    _fastGaussianShader.GetKernelThreadGroupSizes(kernelHandle, out groupSizeX, out groupSizeY, out _);
                    threadGroupsX = Mathf.CeilToInt(width / (float)groupSizeX);
                    threadGroupsY = Mathf.CeilToInt(height / (float)groupSizeY);

                    _fastGaussianShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

                    lastResult = resultRendTex;
                }
            }
        }

        // Get data
        RenderTexture.active = lastResult;
        Texture2D finalTexture = new Texture2D(width, height, TextureFormat.RGBA32, true, false);
        finalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        finalTexture.Apply();

        // Cleanup
        RenderTexture.active = null;

        srcBlurRendTex.Release();
        distRendTex.Release();
        resultRendTex.Release();
        kernelEdgeBlurBuffer.Dispose();
        // kernelBorderBlurBuffer.Dispose();
        kernelFinalBlurBuffer.Dispose();


        return finalTexture;
    }
}
