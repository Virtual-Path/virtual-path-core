#version 300 es

layout(location = 0) in vec3 In_Position;

uniform mat4 View;
uniform mat4 Projection;

void main()
{
    gl_Position = Projection * View * vec4(In_Position, 1.0);
}
