#version 300 es

precision highp float;

uniform vec4 GridColor;
uniform vec3 CameraPos;

out vec4 Out_Color;

void main()
{
    Out_Color = GridColor;
}
