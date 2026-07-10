#version 300 es

precision highp float;

in vec4 VS_Color;

uniform vec4 GizmoColor;

out vec4 Out_Color;

void main()
{
    Out_Color = VS_Color * GizmoColor;
}
