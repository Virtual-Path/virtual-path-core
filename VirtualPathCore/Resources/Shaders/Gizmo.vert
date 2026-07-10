#version 300 es

layout(location = 0) in vec3 In_Position;
layout(location = 4) in vec4 In_Color;

out vec4 VS_Color;

uniform mat4 Model;
uniform mat4 View;
uniform mat4 Projection;

void main()
{
    VS_Color = In_Color;
    gl_Position = Projection * View * Model * vec4(In_Position, 1.0);
}
