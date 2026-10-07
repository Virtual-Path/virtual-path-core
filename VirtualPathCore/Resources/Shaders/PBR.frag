#version 300 es

precision highp float;

in vec3 VS_WorldPos;
in vec3 VS_Normal;
in vec2 VS_UV;
in mat3 VS_TBN;

layout(location = 0) out vec4 Out_Color;

uniform vec4 Albedo;
uniform float Metallic;
uniform float Roughness;
uniform float AmbientIntensity;

uniform vec3 Light0Dir;
uniform vec3 Light0Color;
uniform float Light0Intensity;

uniform vec3 CameraPos;

uniform int HasAlbedoMap;
uniform sampler2D AlbedoMap;
uniform int HasNormalMap;
uniform sampler2D NormalMap;

void main()
{
    vec3 N = normalize(VS_Normal);

    vec3 V = normalize(CameraPos - VS_WorldPos);

    vec3 lightDir = normalize(-Light0Dir);
    vec3 lightColor = Light0Color * Light0Intensity;

    vec4 albedo = Albedo;
    if (HasAlbedoMap == 1)
        albedo = texture(AlbedoMap, VS_UV);

    if (HasNormalMap == 1)
    {
        vec3 tangentNormal = texture(NormalMap, VS_UV).xyz * 2.0 - 1.0;
        N = normalize(VS_TBN * tangentNormal);
    }

    // Ambient term must be driven by the AmbientIntensity uniform.
    // It used to be hardcoded to 0.03 here, which ignored the uniform entirely
    // (the engine sets 0.3) and made every surface render near-black.
    vec3 ambient = AmbientIntensity * albedo.rgb;

    float diff = max(dot(N, lightDir), 0.0);
    vec3 diffuse = diff * lightColor * albedo.rgb;

    vec3 H = normalize(lightDir + V);
    float spec = pow(max(dot(N, H), 0.0), (1.0 - Roughness) * 128.0 + 1.0);
    vec3 specColor = spec * lightColor * mix(vec3(1.0), albedo.rgb, Metallic);

    // Direct light is scaled by its own intensity only. It used to be multiplied
    // by (1.0 - AmbientIntensity), which dimmed the sun whenever ambient was
    // raised and left nothing to separate the two controls.
    vec3 color = ambient + diffuse + specColor;

    Out_Color = vec4(color, albedo.a);
}
