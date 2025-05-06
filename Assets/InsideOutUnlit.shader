Shader "Custom/InsideOutUnlit"
{
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader {
        Tags { "Queue"="Transparent" }
        Cull Front // <- Inverse le culling (on voit l'intérieur de la sphère)
        Lighting Off
        ZWrite Off
        Fog { Mode Off }
        Pass {
            SetTexture [_MainTex] { combine texture }
        }
    }
}