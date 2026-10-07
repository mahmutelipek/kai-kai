Shader "KaiKai/DaySky"
{
    Properties
    {
        _Zenith ("Upper sky", Color) = (0.075,0.34,0.68,1)
        _Horizon ("Horizon haze", Color) = (0.72,0.84,0.89,1)
        _SunDirection ("Sun direction", Vector) = (-0.34,0.17,0.92,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Zenith, _Horizon, _SunDirection;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.position=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float cloudNoise(float2 p)
            {
                return noise(p)*.52 + noise(p*2.03+13.4)*.28 + noise(p*4.11+29.2)*.14 + noise(p*8.17)*.06;
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 dir=normalize(i.direction);
                float height=saturate(dir.y);
                float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(height,.48));
                float sunDot=dot(dir,normalize(_SunDirection.xyz));
                // A visible solar disc plus a broad warm halo, rather than a faint point above the camera.
                float disc=smoothstep(.99968,.99984,sunDot);
                float halo=pow(saturate(sunDot),42)*.24;
                color=lerp(color,float3(1,.87,.55),halo);
                color=lerp(color,float3(1,.98,.84),disc);
                // Slowly drifting cumulus banks on a sky plane. A second scale breaks up their outline.
                float2 plane=dir.xz/max(dir.y+.12,.12)*1.85 + float2(_Time.y*.006,0);
                float cloud=cloudNoise(plane);
                float body=smoothstep(.46,.63,cloud);
                float fine=cloudNoise(plane+float2(.13,.18));
                float3 cloudColor=lerp(float3(.69,.78,.85),float3(1,.98,.94),smoothstep(.44,.64,fine));
                float cloudMask=body*smoothstep(.025,.13,height)*.92;
                color=lerp(color,cloudColor,cloudMask);
                // Two seamless atmospheric ridge layers fill the distant horizon in every direction.
                float angle=atan2(dir.x,dir.z);
                float farPeak=.045+.03*sin(angle*5+1.7)+.025*sin(angle*9-.8)+.012*sin(angle*17);
                float nearPeak=.025+.027*sin(angle*4-.7)+.022*sin(angle*11+2)+.013*sin(angle*21+.4);
                float farMask=1-smoothstep(farPeak,farPeak+.004,dir.y);
                float nearMask=1-smoothstep(nearPeak,nearPeak+.003,dir.y);
                color=lerp(color,float3(.39,.60,.78),farMask);
                color=lerp(color,float3(.24,.43,.62),nearMask);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
