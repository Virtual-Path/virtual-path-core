using System.Runtime.InteropServices;
using Silk.NET.GLFW;

namespace VirtualPathCore.CameraBridge;

/// <summary>
/// GLFW 的最小 P/Invoke 封装（只做"建一个不可见窗口 + 一个 GL 上下文"这件事）。
///
/// 为什么不直接用 Silk.NET 的封装：
/// <c>Glfw.Context</c> / <c>GL.CreateDefaultContext</c> 底层都是
/// <c>DefaultNativeContext</c>，它把 GL 函数当作<em>窗口库自身</em>的导出符号去查，
/// 而 <c>glfw3.dll</c> 并不导出 GL 入口点，于是 <c>glGetString</c> 等一律解析失败。
/// 这里改为显式 P/Invoke，并自己实现"先问 glfwGetProcAddress，再回退 opengl32.dll"
/// 的标准解析顺序（GL 1.1 核心函数 glfwGetProcAddress 可能返回 NULL）。
/// </summary>
internal static unsafe class GlfwNative
{
    private const string Lib = "glfw3";

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int glfwInit();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void glfwTerminate();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void glfwWindowHint(int hint, int value);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern nint glfwCreateWindow(int width, int height, string title, nint monitor, nint share);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void glfwDestroyWindow(nint window);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void glfwMakeContextCurrent(nint window);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern void glfwPollEvents();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint glfwGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint LoadLibraryA(string lpLibFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint GetProcAddress(nint hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    private static nint _opengl32;
    private static nint _window;
    private static bool _initialized;

    /// <summary>创建不可见窗口并把 GL 上下文设为当前，返回窗口句柄。</summary>
    public static nint CreateHiddenContext(int width, int height)
    {
        if (!_initialized)
        {
            if (glfwInit() == 0)
                throw new InvalidOperationException("glfwInit() failed.");
            _initialized = true;
            _opengl32 = LoadLibraryA("opengl32.dll");
        }

        // 用 Silk.NET.GLFW 的枚举取值（其成员值即 GLFW 原始 hint 常量），避免魔法数字
        glfwWindowHint((int)WindowHintBool.Visible, 0);
        glfwWindowHint((int)WindowHintBool.OpenGLForwardCompat, 1);
        glfwWindowHint((int)WindowHintInt.ContextVersionMajor, 3);
        glfwWindowHint((int)WindowHintInt.ContextVersionMinor, 3);
        glfwWindowHint((int)WindowHintOpenGlProfile.OpenGlProfile, (int)OpenGlProfile.Core);
        glfwWindowHint((int)WindowHintInt.DepthBits, 24);

        _window = glfwCreateWindow(width, height, "vp-cam", 0, 0);
        if (_window == 0)
            throw new InvalidOperationException("glfwCreateWindow() failed.");

        glfwMakeContextCurrent(_window);
        return _window;
    }

    /// <summary>GL 入口点解析：先 glfwGetProcAddress，再回退 opengl32.dll 的导出表。</summary>
    public static nint GetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name)
    {
        nint p = glfwGetProcAddress(name);
        if (p == 0 && _opengl32 != 0)
            p = GetProcAddress(_opengl32, name);
        return p;
    }

    public static void PollEvents() => glfwPollEvents();

    public static void Destroy()
    {
        if (_window != 0)
        {
            glfwDestroyWindow(_window);
            _window = 0;
        }

        if (_initialized)
        {
            glfwTerminate();
            _initialized = false;
        }

        _opengl32 = 0;
    }
}
