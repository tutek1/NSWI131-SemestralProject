// Applies gamma correction from Linear to sRGB color space
float3 LinearToSRGB(float3 linearCol)
{
    float3 srgbLo  = linearCol * 12.92;
    float3 srgbHi = 1.055 * pow(abs(linearCol), 1.0/2.4) - 0.055;
    float3 result = lerp(srgbLo, srgbHi, step(0.0031308, linearCol));
    return result;
}

// Gets the grayscale of the pixel
inline float grayscale(float3 pixel)
{
    return dot(pixel.rgb, float3(0.2126, 0.7152, 0.0722));
}

// Blurs a given srcTex RWTexture2D and writes result to destTex
void GaussianBlurTexture(uint3 id : SV_DispatchThreadID, Texture2D<float4> srcTex, RWTexture2D<float4> destTex,
    Buffer<float> kernel, uint blurRadius, bool gammaCorrect)
{
    // Setup vars
    int2 coords = id.xy;
    float3 color = float3(0, 0, 0);
    float weightSum = 0.0;
    int kernelLineLength = blurRadius + 1;

    // Check if the pixel is even visible
    if (srcTex[coords].a <= 0.01)
    {
        destTex[coords] = float4(0, 0, 0, 0);
        return;
    } 

    // Get bounds
    int2 texSize;
    srcTex.GetDimensions(texSize.x, texSize.y);

    // Calculate bounds
    int yMin = max(0, coords.y - blurRadius);
    int yMax = min(texSize.y - 1, coords.y + blurRadius);
    int xMin = max(0, coords.x - blurRadius);
    int xMax = min(texSize.x - 1, coords.x + blurRadius);

    // Sample neighbors and with Gaussian kernel weight
    for (int x = xMin; x <= xMax; x++)
    {
        for (int y = yMin; y <= yMax; y++)
        {
            int2 texCoord = int2(x, y);

            // Get sample and use it only if high alpha
            float4 sample = srcTex[texCoord];
            if (sample.w <= 0.01) continue;

            // Get coords in gauss kernel
            int offsetY = y - (coords.y - blurRadius);
            int offsetX = x - (coords.x - blurRadius);

            // Get gaussian weight and sum the color and weight
            float weight = kernel[offsetY * kernelLineLength + offsetX];
            color += sample.xyz * weight;
            weightSum += weight;
        }
    }
    color = color / weightSum;

    // Output the final pixel
    if (gammaCorrect)
    {
        color = LinearToSRGB(color);
    }

    destTex[coords] = float4(color, srcTex[coords].w);
}
