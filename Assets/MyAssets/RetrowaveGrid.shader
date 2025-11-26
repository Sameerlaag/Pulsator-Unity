Shader "Custom/RetrowaveGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (1, 0, 1, 1)
        _BackgroundColor ("Background Color", Color) = (0.1, 0.05, 0.2, 1)
        _GridThickness ("Grid Thickness", Range(0, 0.1)) = 0.02
        _GridSpacing ("Grid Spacing", Float) = 1
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        
        float4 _GridColor;
        float4 _BackgroundColor;
        float _GridThickness;
        float _GridSpacing;
        float _EmissionStrength;
        
        struct Input
        {
            float3 worldPos;
        };
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 grid = abs(frac(IN.worldPos.xz / _GridSpacing - 0.5) - 0.5) / fwidth(IN.worldPos.xz) * _GridSpacing;
            float _line = min(grid.x, grid.y);
            float gridMask = 1.0 - min(_line / _GridThickness, 1.0);
            
            float4 finalColor = lerp(_BackgroundColor, _GridColor, gridMask);
            o.Albedo = finalColor.rgb;
            o.Emission = _GridColor.rgb * gridMask * _EmissionStrength;
            o.Metallic = 0;
            o.Smoothness = 0.5;
        }
        ENDCG
    }
    FallBack "Diffuse"
}