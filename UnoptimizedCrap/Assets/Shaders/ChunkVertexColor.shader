Shader "Custom/ChunkVertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normalWS : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.normalWS = normalize(worldNormal);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Simple directional light + ambient
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(i.normalWS, lightDir));
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz + 0.15; // small constant to prevent black faces
                float3 lit = tex.rgb * i.color.rgb * (ambient + ndotl * 0.85 + 0.15);

                fixed4 col = fixed4(lit, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
