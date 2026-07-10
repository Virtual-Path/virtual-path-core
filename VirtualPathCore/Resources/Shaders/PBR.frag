#version 300 es

precision highp float;

in vec3 VS_WorldPos;
in vec3 VS_Normal;
in vec2 VS_UV;

layout(location = 0) out vec4 Out_Color;

uniform vec4 Albedo;
uniform float Metallic;
uniform float Roughness;
uniform float AmbientIntensity;

uniform vec3 Light0Dir;
uniform vec3 Light0Color;
uniform float Light0Intensity;

uniform vec3 CameraPos;

void main()
{
    vec3 N = normalize(VS_Normal);
    vec3 V = normalize(CameraPos - VS_WorldPos);

    vec3 lightDir = normalize(-Light0Dir);
    vec3 lightColor = Light0Color * Light0Intensity;

    vec3 ambient = 0.03 * Albedo.rgb;

    float diff = max(dot(N, lightDir), 0.0);
    vec3 diffuse = diff * lightColor * Albedo.rgb;

    vec3 H = normalize(lightDir + V);
    float spec = pow(max(dot(N, H), 0.0), (1.0 - Roughness) * 128.0 + 1.0);
    vec3 specColor = spec * lightColor * mix(vec3(1.0), Albedo.rgb, Metallic);

    vec3 color = ambient + (1.0 - AmbientIntensity) * (diffuse + specColor);

    Out_Color = vec4(color, Albedo.a);
}
