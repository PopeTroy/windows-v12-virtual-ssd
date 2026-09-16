/*
============================================================================
SHINOBI HPL3 DIP ENHANCER - STRIKE PROTOTYPE (VSSDHX ENGINE INTEGRATION)
============================================================================
Target Engine: ReShade / HPL3 Custom Post-Processing Pipeline
Features: Dynamic DOF Sampling, Depth-Gated Sharpening, Sobel Edge Boost,
          Time-Animated Chromatic Aberration, Low-End Performance Presets.
============================================================================
*/

#include "ReShade.fxh"

// ============================================================================
// UNIFORMS & USER CONTROLS
// ============================================================================

uniform int Quality_Level <
    ui_type = "combo";
    ui_label = "Performance Preset";
    ui_tooltip = "Scales sampling rates for lower-end systems to preserve frame times.";
    ui_items = "Ultra (Full Sampling)\0Balanced (Medium Sampling)\0Low-End (Reduced Sampling)\0";
> = 0;

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

uniform float ShadowCloneDIPEdgeSharpen <
    ui_type = "slider"; ui_min = 0.0; ui_max = 2.0;
    ui_label = "Shadow Clone DIP Sharpening (512x512 Proxy)";
> = 1.2;

uniform float FocalDepth <
    ui_type = "slider";
    ui_label = "Focal Depth";
    ui_min = 0.0; ui_max = 1.0;
> = 0.5;

uniform float FocalDepthFalloff <
    ui_type = "slider";
    ui_label = "Focal Depth Falloff";
    ui_tooltip = "Controls smoothness of transition between in-focus and blurred regions.";
    ui_min = 0.01; ui_max = 0.5;
> = 0.1;

uniform float MaxBlurRadius <
    ui_type = "slider";
    ui_label = "Max Blur Radius";
    ui_min = 1.0; ui_max = 8.0; // Enforced reduced max value for low-end safeguards
> = 4.0;

uniform float ShadowClone_EdgeBoost <
    ui_type = "slider";
    ui_label = "Shadow Clone Edge Boost";
    ui_tooltip = "Sobel-approximated tactical edge detection intensity.";
    ui_min = 0.0; ui_max = 2.0;
> = 0.8;

uniform float ParticleSharpening <
    ui_type = "slider";
    ui_label = "Particle Sharpen Strength";
    ui_tooltip = "Applied strictly to in-focus pixels.";
    ui_min = 0.0; ui_max = 1.5;
> = 0.5;

uniform float ChromaticAberrationAmount <
    ui_type = "slider";
    ui_label = "Chromatic Aberration";
    ui_min = 0.0; ui_max = 0.01;
> = 0.0025;

uniform float timer < source = "timer"; >;

// ============================================================================
// HELPER FUNCTIONS & SOBEL EDGE DETECTION
// ============================================================================

// Lightweight procedural noise for visual kinetic dynamics
float FastProceduralNoise(float2 coords, float time_val)
{
    return frac(sin(dot(coords + float2(time_val * 0.001, 0.0), float2(12.9898, 78.233))) * 43758.5453);
}

// Sobel-approximated edge detection for ShadowClone_EdgeBoost
float CalculateSobelEdge(float2 texcoord)
{
    float2 s = ReShade::PixelSize;
    
    float t0 = ReShade::GetLinearDepth(texcoord + float2(-s.x, -s.y));
    float t1 = ReShade::GetLinearDepth(texcoord + float2( 0.0, -s.y));
    float t2 = ReShade::GetLinearDepth(texcoord + float2( s.x, -s.y));
    float t3 = ReShade::GetLinearDepth(texcoord + float2(-s.x,  0.0));
    float t5 = ReShade::GetLinearDepth(texcoord + float2( s.x,  0.0));
    float t6 = ReShade::GetLinearDepth(texcoord + float2(-s.x,  s.y));
    float t7 = ReShade::GetLinearDepth(texcoord + float2( 0.0,  s.y));
    float t8 = ReShade::GetLinearDepth(texcoord + float2( s.x,  s.y));

    float gx = (t2 + 2.0 * t5 + t8) - (t0 + 2.0 * t3 + t6);
    float gy = (t0 + 2.0 * t1 + t2) - (t6 + 2.0 * t7 + t8);

    return sqrt(gx * gx + gy * gy);
}

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

// ============================================================================
// PIXEL SHADER PASS
// ============================================================================

float4 PS_ShinobiHPL3Enhancer(float4 pos : SV_Position, float2 texcoord : TEXCOORD) : SV_Target
{
    float depth = ReShade::GetLinearDepth(texcoord);
    
    // 1. REFINED DOF & FOCAL FALLOFF CALCULATIONS
    float depth_diff = abs(depth - FocalDepth);
    float blur_factor = smoothstep(0.0, FocalDepthFalloff, depth_diff);
    
    // Quality_Level dynamic sample rate adjustment
    int sample_count = 8;
    if (Quality_Level == 1) sample_count = 5;      // Balanced
    else if (Quality_Level == 2) sample_count = 3; // Low-End

    float active_radius = MaxBlurRadius * blur_factor;
    float4 color_accum = 0.0;
    float total_weight = 0.0;

    // Dynamic Sampling Loop for Depth of Field
    [unroll]
    for (int i = -sample_count; i <= sample_count; ++i)
    {
        float offset = float(i) * (active_radius / float(sample_count));
        float2 sample_uv = texcoord + float2(offset * ReShade::PixelSize.x, 0.0);
        float weight = 1.0 - abs(float(i) / float(sample_count));
        
        color_accum += tex2D(ReShade::BackBuffer, sample_uv) * weight;
        total_weight += weight;
    }
    
    float4 base_color = color_accum / total_weight;

    // Apply Ocular Warp and Shadow Clone Edge Sharpening Integration
    float3 warpColor = ApplyOcularWarp(texcoord);
    base_color.rgb = lerp(base_color.rgb, warpColor, 0.5);
    base_color.rgb = ApplyShadowCloneEdgeSharpen(texcoord, base_color.rgb);

    // Shinobi Focal Cutoff Mask Bypass
    if (depth > FocalDepthCutoff)
    {
        float2 blurOffset = ReShade::PixelSize * 2.0;
        float3 blurred = (
            tex2D(ReShade::BackBuffer, texcoord + blurOffset).rgb +
            tex2D(ReShade::BackBuffer, texcoord - blurOffset).rgb
        ) * 0.5;
        return float4(blurred, 1.0);
    }

    // 2. ADVANCED SHINOBI TACTICS: TIME-BASED CHROMATIC ABERRATION
    float animated_time = timer * 0.001;
    float2 ca_offset = float2(cos(animated_time), sin(animated_time)) * ChromaticAberrationAmount * blur_factor;
    
    float red_channel   = tex2D(ReShade::BackBuffer, texcoord + ca_offset).r;
    float blue_channel  = tex2D(ReShade::BackBuffer, texcoord - ca_offset).b;
    base_color.r = lerp(base_color.r, red_channel, 0.7);
    base_color.b = lerp(base_color.b, blue_channel, 0.7);

    // 3. LOW-END OPTIMIZATION: CONDITIONAL SHARPENING (IN-FOCUS ONLY)
    if (blur_factor < 0.25 && ParticleSharpening > 0.0)
    {
        float4 center = base_color;
        float4 blur_neighbors = (
            tex2D(ReShade::BackBuffer, texcoord + float2(ReShade::PixelSize.x, 0.0)) +
            tex2D(ReShade::BackBuffer, texcoord - float2(ReShade::PixelSize.x, 0.0)) +
            tex2D(ReShade::BackBuffer, texcoord + float2(0.0, ReShade::PixelSize.y)) +
            tex2D(ReShade::BackBuffer, texcoord - float2(0.0, ReShade::PixelSize.y))
        ) * 0.25;

        float4 sharp_pass = center + (center - blur_neighbors) * ParticleSharpening;
        base_color = lerp(base_color, sharp_pass, (1.0 - blur_factor));
    }

    // Apply Dust Release Particle Sharpening
    base_color.rgb = ApplyDustReleaseSharpening(texcoord, base_color.rgb);

    // 4. SHADOWCLONE_EDGEBOOST (SOBEL EDGE DETECT)
    if (ShadowClone_EdgeBoost > 0.0)
    {
        float edge = CalculateSobelEdge(texcoord);
        float edge_mask = saturate(edge * 50.0 * ShadowClone_EdgeBoost);
        
        // Dynamic noise integration for tactical organic feedback
        float noise = FastProceduralNoise(texcoord, timer);
        float3 edge_color = lerp(base_color.rgb * 1.35, base_color.rgb + float3(noise, noise, noise) * 0.1, 0.2);
        
        base_color.rgb = lerp(base_color.rgb, edge_color, edge_mask);
    }

    return base_color;
}

// ============================================================================
// TECHNIQUES
// ============================================================================

technique ShinobiHPL3_DIP_Enhancer
{
    pass MainPass
    {
        VertexShader = PostProcessVS;
        PixelShader = PS_ShinobiHPL3Enhancer;
    }
}
