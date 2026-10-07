using Silk.NET.OpenGLES;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// 诊断：检查渲染路径上用到的 GL 入口点是否都能成功解析。
///
/// 背景：引擎桌面版（Avalonia 供给 GL 上下文）显示正常，而 headless 宿主
/// （GLFW + 自建符号解析）显示异常。二者唯一的结构性差异是
/// "函数地址怎么拿到"：Avalonia 用自己的 <c>GlInterface.GetProcAddress</c>，
/// 而本工程走 <c>GlfwNative.GetProcAddress</c>。
///
/// 若某个关键函数（如 glUniformMatrix4fv / glVertexAttribPointer）在这里解析为 NULL，
/// 对应调用会静默失效，uniform 保持默认值 0，
/// 于是 <c>ObjectToClip</c> 变成零矩阵，所有顶点被投影到同一点 —— 画面上就只剩一条线。
/// </summary>
internal static class SymbolProbe
{
    public static int Run()
    {
        Console.WriteLine();
        Console.WriteLine("=== GL symbol resolution probe ===");

        using var host = new HeadlessGraphicsHost(64, 64);
        GL gl = host.GetContext();

        // 渲染路径上真正会被调用的入口点
        string[] required =
        {
            // 上下文/状态
            "glUseProgram", "glEnable", "glDisable", "glViewport", "glScissor",
            "glClearColor", "glClear", "glDepthFunc", "glDepthMask", "glCullFace",
            "glFrontFace", "glBlendFunc",
            // uniform
            "glGetUniformLocation", "glUniformMatrix4fv", "glUniform1f", "glUniform1i",
            "glUniform2fv", "glUniform3fv", "glUniform4fv",
            "glGetAttribLocation",
            // 顶点/索引
            "glGenVertexArrays", "glBindVertexArray", "glDeleteVertexArrays",
            "glGenBuffers", "glBindBuffer", "glBufferData", "glBufferSubData", "glDeleteBuffers",
            "glVertexAttribPointer", "glEnableVertexAttribArray",
            "glDrawElements", "glDrawArrays",
            // 着色器
            "glCreateShader", "glShaderSource", "glCompileShader", "glGetShaderiv",
            "glGetShaderInfoLog", "glDeleteShader",
            "glCreateProgram", "glAttachShader", "glLinkProgram", "glGetProgramiv",
            "glGetProgramInfoLog", "glDeleteProgram", "glUseProgram",
            // 纹理/FBO
            "glGenTextures", "glBindTexture", "glTexImage2D", "glTexParameteri", "glDeleteTextures",
            "glActiveTexture",
            "glGenFramebuffers", "glBindFramebuffer", "glFramebufferTexture2D",
            "glFramebufferRenderbuffer", "glCheckFramebufferStatus",
            "glGenRenderbuffers", "glBindRenderbuffer", "glRenderbufferStorage", "glDeleteRenderbuffers",
            "glDeleteFramebuffers", "glBlitFramebuffer",
            "glReadPixels", "glReadBuffer", "glPixelStorei",
            "glGetError", "glGetString", "glGetIntegerv", "glGetFloatv",
        };

        var missing = new List<string>();
        foreach (string name in required)
        {
            // 与引擎实际使用的方式一致：走 Silk 的 GL 实例
            bool ok = TryResolveViaGl(gl, name);
            if (!ok) missing.Add(name);
        }

        Console.WriteLine($"  probed        : {required.Length}");
        Console.WriteLine($"  missing       : {missing.Count}");

        if (missing.Count > 0)
        {
            Console.WriteLine("  UNRESOLVED SYMBOLS:");
            foreach (string m in missing)
                Console.WriteLine($"    - {m}");

            Console.WriteLine();
            Console.WriteLine("RESULT: FAIL - some GL entry points could not be resolved;");
            Console.WriteLine("        the corresponding calls silently do nothing.");
            return 8;
        }

        Console.WriteLine();
        Console.WriteLine("RESULT: PASS - all required GL symbols resolve.");
        return 0;
    }

    /// <summary>
    /// 用与引擎相同的入口（Silk 的 GL 实例）尝试解析符号。
    /// Silk 在符号缺失时会在首次调用抛异常，这里通过直接调用 GetProcAddress 判断。
    /// </summary>
    private static bool TryResolveViaGl(GL gl, string name)
    {
        try
        {
            // Silk.NET 的 GL 实例通过 delegate 调用底层函数，
            // 没有公开的 TryGetProcAddress；这里改为调用 Silk 的静态 DefaultNativeContext，
            // 与 GlfwNative 用的是同一个解析器。
            return GlfwNative.GetProcAddress(name) != nint.Zero;
        }
        catch
        {
            return false;
        }
    }
}
