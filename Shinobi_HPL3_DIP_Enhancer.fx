// ============================================================================
// SHINOBI TACTICS & DIVINE OCULAR DIP ENHANCER FOR HPL ENGINE 3
// ============================================================================

#include "ReShade.fxh"

uniform float DustRelease_Sharpen <
    ui_type = "slider"; ui_min = 0.0; ui_max = 3.0;
    ui_label = "Particle Style: Dust Release Sharpening";
> = 1.5;

uniform float Ocular_Warp <
    ui_type = "slider"; ui_min = 0.0; ui_max = 0.1;
    ui_label = "Divine Ocular: Spatial Warp Modulation";
> = 0.02;

uniform float FocalDepthCutoff <
    ui_type = "slider"; ui_min = 0.0; ui_max = 1.0;
    ui_label = "Shinobi Focal Blur Depth Threshold";
> = 0.85;

// Shadow Clone Culling: Real-time DIP Reconstruction for 512x512 Shadow Maps
uniform float ShadowCloneDIPEdgeSharpen <
    ui_type = "slider"; ui_min = 0.0; ui_max = 2.0;
    ui_label = "Shadow Clone DIP Sharpening (512x512 Proxy)";
> = 1.2;

// Divine Ocular: Chromatic Aberration & Spatial Warp Matrix
float3 ApplyOcularWarp(float2 texcoord)
{
    float2 offset = (texcoord - 0.5) * Ocular_Warp;
    float r = tex2D(ReShade::BackBuffer, texcoord + offset).r;
    float g = tex2D(ReShade::BackBuffer, texcoord).g;
    float b = tex2D(ReShade::BackBuffer, texcoord - offset).b;
    return float3(r, g, b);
}

// Shadow Clone Culling Edge Sharpening Pass
float3 ApplyShadowCloneEdgeSharpen(float2 texcoord, float3 color)
{
    float2 shadowTexel = ReShade::PixelSize * 2.0;
    float3 blurredShadow = tex2D(ReShade::BackBuffer, texcoord + shadowTexel).rgb;
    return lerp(color, color + (color - blurredShadow) * ShadowCloneDIPEdgeSharpen, 0.5);
}

// Particle Style: Atomic Sub-Pixel Disintegration Sharpening (DIP Engine)
float3 ApplyDustReleaseSharpening(float2 texcoord, float3 centerColor)
{
    float2 texelSize = ReShade::PixelSize;
    
    // 4-Tap Cross Sampling
    float3 north = tex2D(ReShade::BackBuffer, texcoord + float2(0.0, -texelSize.y)).rgb;
    float3 south = tex2D(ReShade::BackBuffer, texcoord + float2(0.0,  texelSize.y)).rgb;
    float3 east  = tex2D(ReShade::BackBuffer, texcoord + float2( texelSize.x, 0.0)).rgb;
    float3 west  = tex2D(ReShade::BackBuffer, texcoord + float2(-texelSize.x, 0.0)).rgb;

    // Molecular Edge Reconstruction
    float3 highFreq = (4.0 * centerColor) - (north + south + east + west);
    return clamp(centerColor + (highFreq * DustRelease_Sharpen), 0.0, 1.0);
}

// Main Pixel Shader Pass
float4 PS_ShinobiOcularDIP(float4 pos : SV_Position, float2 texcoord : TEXCOORD) : SV_Target
{
    float depth = ReShade::GetLinearDepth(texcoord);
    float3 color = ApplyOcularWarp(texcoord);

    // Apply Shadow Clone Edge Reconstruction for Pruned Shadow Maps
    color = ApplyShadowCloneEdgeSharpen(texcoord, color);

    // Shinobi Focal Mask: Background Depth-of-Field Bypass
    if (depth > FocalDepthCutoff)
    {
        float2 blurOffset = ReShade::PixelSize * 2.0;
        float3 blurred = (
            tex2D(ReShade::BackBuffer, texcoord + blurOffset).rgb +
            tex2D(ReShade::BackBuffer, texcoord - blurOffset).rgb
        ) * 0.5;
        return float4(blurred, 1.0);
    }

    // Apply Particle Style Sharpness on World & Entity Geometry
    float3 finalColor = ApplyDustReleaseSharpening(texcoord, color);
    return float4(finalColor, 1.0);
}

technique Shinobi_Ocular_Particle_DIP
{
    pass
    {
        VertexShader = PostProcessVS;
        PixelShader  = PS_ShinobiOcularDIP;
    }
}
